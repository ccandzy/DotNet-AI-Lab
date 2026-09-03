using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using AiChatClient.Services;
using Microsoft.Web.WebView2.Core;

namespace AiChatClient.Views
{
    public partial class MarkdownView : UserControl
    {
        public static readonly DependencyProperty MarkdownProperty = DependencyProperty.Register(
            nameof(Markdown), typeof(string), typeof(MarkdownView), new PropertyMetadata(string.Empty, OnMarkdownChanged));

        private readonly IMarkdownRendererService _renderer;

        // Latest markdown successfully pushed to WebView2
        private string _lastRenderedMarkdown = string.Empty;

        private bool _webViewReady;
        private bool _initialPageLoaded;
        private bool _firstNavPending; // NavigateToString started, waiting for OnNavigationCompleted

        // Queued content before WebView2 is ready
        private string _pendingMarkdown = string.Empty;

        public event EventHandler? ContentRendered;

        public MarkdownView()
        {
            InitializeComponent();

            if (DesignerProperties.GetIsInDesignMode(this))
            {
                _renderer = null!;
                return;
            }

            _renderer = App.Services?.GetService(typeof(IMarkdownRendererService)) as IMarkdownRendererService
                        ?? throw new InvalidOperationException("IMarkdownRendererService is not registered in the DI container.");

            _ = EnsureWebViewInitializedAsync();
        }

        public string Markdown
        {
            get => (string)GetValue(MarkdownProperty);
            set => SetValue(MarkdownProperty, value);
        }

        // ----------------------------------------------------------------
        // Dependency property callback — kick off an async render pass
        // ----------------------------------------------------------------

        /// <summary>
        /// Dependency property callback.  Always kicks off a render loop;
        /// the loop itself deduplicates via <see cref="_lastRenderedMarkdown"/>.
        /// No re-entrance guard here — the loop's own checks prevent wasted work.
        /// </summary>
        private static void OnMarkdownChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MarkdownView mv)
            {
                _ = mv.RenderLoopAsync();
            }
        }

        /// <summary>
        /// Render loop: read DP, render, repeat until stable.
        /// If a render is already in-flight we bail early; the in-flight loop
        /// already reads the latest DP value inside its while(true) body.
        /// </summary>
        private async Task RenderLoopAsync()
        {
            while (true)
            {
                var current = Markdown;
                if (current == _lastRenderedMarkdown)
                    break;

                if (!_webViewReady)
                {
                    _pendingMarkdown = current;
                    break;
                }

                // First navigation is in-flight — store latest and let
                // OnNavigationCompleted resume.
                if (_firstNavPending)
                {
                    _pendingMarkdown = current;
                    break;
                }

                _lastRenderedMarkdown = current;
                await RenderOnceAsync(current);
            }
        }

        // ----------------------------------------------------------------
        // Single render: NavigateToString for first load, JS innerHTML thereafter
        // ----------------------------------------------------------------

        private async Task RenderOnceAsync(string markdown)
        {
            try
            {
                if (!_initialPageLoaded)
                {
                    // First time: NavigateToString with the full HTML page.
                    // Set the flag BEFORE the actual navigation call so subsequent
                    // PropertyChanged events don't trigger a second NavigateToString.
                    _firstNavPending = true;

                    var fullHtml = _renderer.RenderToHtml(markdown);
                    await Dispatcher.InvokeAsync(() =>
                    {
                        try
                        {
                            WebView.CoreWebView2?.NavigateToString(fullHtml);
                        }
                        catch (Exception ex)
                        {
                            _firstNavPending = false;
                            System.Diagnostics.Debug.WriteLine($"[MarkdownView] NavigateToString failed: {ex.Message}");
                        }
                    });

                    // NavigationCompleted will set _initialPageLoaded and trigger
                    // a render for any content queued during the navigation.
                    return;
                }

                // Subsequent updates: use JS to swap innerHTML (no flicker)
                var bodyHtml = _renderer.RenderBodyToHtml(markdown);
                var json = System.Text.Json.JsonSerializer.Serialize(bodyHtml);
                var script = $"refreshContent({json})";

                await Dispatcher.InvokeAsync(async () =>
                {
                    try
                    {
                        if (WebView.CoreWebView2 is not null)
                        {
                            var result = await WebView.CoreWebView2.ExecuteScriptAsync(script);
                            System.Diagnostics.Debug.WriteLine($"[MarkdownView] JS result: {result?.Trim() ?? "null"}");

                            // Use the scrollHeight returned by refreshContent to resize
                            if (double.TryParse(result?.Trim('"'), out var scrollHeight) && scrollHeight > 0)
                            {
                                WebView.Height = scrollHeight + 8;
                                System.Diagnostics.Debug.WriteLine($"[MarkdownView] Adjusted height to {WebView.Height}");
                            }
                        }
                        ContentRendered?.Invoke(this, EventArgs.Empty);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[MarkdownView] JS update failed: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MarkdownView] Render error: {ex.Message}");
            }
        }

        // ----------------------------------------------------------------
        // WebView2 initialization
        // ----------------------------------------------------------------

        private async Task EnsureWebViewInitializedAsync()
        {
            try
            {
                if (WebView.CoreWebView2 == null)
                {
                    var env = await CoreWebView2Environment.CreateAsync();
                    await WebView.EnsureCoreWebView2Async(env);
                    WebView.DefaultBackgroundColor = System.Drawing.Color.Transparent;
                }

                WebView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;

                _webViewReady = true;

                // Switch from fallback to WebView2
                FallbackText.Visibility = Visibility.Collapsed;
                WebView.Visibility = Visibility.Visible;

                // Trigger render for any content that arrived during init
                _lastRenderedMarkdown = string.Empty;
                _ = RenderLoopAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MarkdownView] WebView2 init failed: {ex.Message}");
                // Fallback TextBlock stays visible
            }
        }

        // ----------------------------------------------------------------
        // Navigation completed — first page is loaded, switch to JS mode
        // ----------------------------------------------------------------

        private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            _firstNavPending = false;
            _initialPageLoaded = true;

            System.Diagnostics.Debug.WriteLine($"[MarkdownView] NavigationCompleted, isSuccess={e.IsSuccess}, httpStatus={e.HttpStatusCode}");

            // Adjust WebView2 height to fit content after first page load
            _ = AdjustWebViewHeightAsync();

            // Content is now visible; notify parent for scroll
            ContentRendered?.Invoke(this, EventArgs.Empty);

            // If content changed during the initial navigation, re-render now.
            _ = RenderLoopAsync();
        }

        // ----------------------------------------------------------------
        // Dynamic height: query scrollHeight from the HTML page and apply
        // ----------------------------------------------------------------

        /// <summary>
        /// Reads <c>document.body.scrollHeight</c> from the WebView2 page
        /// and sets <see cref="WebView2.Height"/> to match (plus a small padding).
        /// </summary>
        private async Task AdjustWebViewHeightAsync()
        {
            try
            {
                if (WebView.CoreWebView2 is null)
                    return;

                // Short delay to ensure the DOM layout is complete
                await Task.Delay(50);

                var heightStr = await WebView.CoreWebView2.ExecuteScriptAsync(
                    "document.body.scrollHeight.toString()");

                System.Diagnostics.Debug.WriteLine($"[MarkdownView] scrollHeight={heightStr}");

                if (double.TryParse(heightStr?.Trim('"'), out var height) && height > 0)
                {
                    WebView.Height = height + 8;
                    System.Diagnostics.Debug.WriteLine($"[MarkdownView] Adjusted height to {WebView.Height}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MarkdownView] Height adjustment failed: {ex.Message}");
            }
        }
    }
}
