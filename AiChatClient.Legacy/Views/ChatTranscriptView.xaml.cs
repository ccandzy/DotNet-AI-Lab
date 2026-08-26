using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using AiChatClient.Models;
using AiChatClient.Services;
using Microsoft.Web.WebView2.Core;

namespace AiChatClient.Views;

/// <summary>
/// 在单个 WebView2 中显示整个会话记录，并将集合与流式消息更新同步到 HTML DOM。
/// </summary>
public partial class ChatTranscriptView : UserControl
{
    public static readonly DependencyProperty MessagesProperty = DependencyProperty.Register(
        nameof(Messages),
        typeof(ObservableCollection<ChatMessage>),
        typeof(ChatTranscriptView),
        new PropertyMetadata(null, OnMessagesChanged));

    private readonly IMarkdownRendererService _markdownRenderer;
    private readonly SemaphoreSlim _scriptGate = new(1, 1);
    private readonly Dictionary<ChatMessage, string> _messageKeys = new();
    private readonly HashSet<ChatMessage> _observedMessages = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    private bool _pageReady;

    public ChatTranscriptView()
    {
        InitializeComponent();

        if (DesignerProperties.GetIsInDesignMode(this))
        {
            _markdownRenderer = null!;
            return;
        }

        _markdownRenderer = App.Services?.GetService(typeof(IMarkdownRendererService)) as IMarkdownRendererService
            ?? throw new InvalidOperationException("IMarkdownRendererService is not registered in the DI container.");

        _ = EnsureWebViewInitializedAsync();
    }

    public ObservableCollection<ChatMessage>? Messages
    {
        get => (ObservableCollection<ChatMessage>?)GetValue(MessagesProperty);
        set => SetValue(MessagesProperty, value);
    }

    private static void OnMessagesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ChatTranscriptView view)
        {
            return;
        }

        view.DetachMessages(e.OldValue as ObservableCollection<ChatMessage>);
        view.AttachMessages(e.NewValue as ObservableCollection<ChatMessage>);
        _ = view.RenderAllAsync();
    }

    private void AttachMessages(ObservableCollection<ChatMessage>? messages)
    {
        _messageKeys.Clear();

        if (messages is null)
        {
            return;
        }

        messages.CollectionChanged += Messages_CollectionChanged;
        foreach (var message in messages)
        {
            ObserveMessage(message);
        }
    }

    private void DetachMessages(ObservableCollection<ChatMessage>? messages)
    {
        if (messages is not null)
        {
            messages.CollectionChanged -= Messages_CollectionChanged;
        }

        foreach (var message in _observedMessages)
        {
            message.PropertyChanged -= Message_PropertyChanged;
        }

        _observedMessages.Clear();
        _messageKeys.Clear();
    }

    private void Messages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add when e.NewItems is not null:
                foreach (var item in e.NewItems.OfType<ChatMessage>())
                {
                    ObserveMessage(item);
                    _ = AppendMessageAsync(item);
                }
                break;

            case NotifyCollectionChangedAction.Remove when e.OldItems is not null:
                foreach (var item in e.OldItems.OfType<ChatMessage>())
                {
                    StopObservingMessage(item);
                    _ = RemoveMessageAsync(item);
                }
                break;

            default:
                RewireMessageSubscriptions();
                _ = RenderAllAsync();
                break;
        }
    }

    private void RewireMessageSubscriptions()
    {
        foreach (var message in _observedMessages)
        {
            message.PropertyChanged -= Message_PropertyChanged;
        }

        _observedMessages.Clear();

        if (Messages is null)
        {
            return;
        }

        foreach (var message in Messages)
        {
            ObserveMessage(message);
        }
    }

    private void ObserveMessage(ChatMessage message)
    {
        if (_observedMessages.Add(message))
        {
            message.PropertyChanged += Message_PropertyChanged;
        }

        _ = GetMessageKey(message);
    }

    private void StopObservingMessage(ChatMessage message)
    {
        if (_observedMessages.Remove(message))
        {
            message.PropertyChanged -= Message_PropertyChanged;
        }
    }

    private void Message_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is ChatMessage message &&
            (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(ChatMessage.Content)))
        {
            _ = UpdateMessageAsync(message);
        }
    }

    private async Task EnsureWebViewInitializedAsync()
    {
        try
        {
            var environment = await CoreWebView2Environment.CreateAsync();
            await WebView.EnsureCoreWebView2Async(environment);

            var assetsPath = Path.Combine(AppContext.BaseDirectory, "Assets");
            var transcriptPath = Path.Combine(assetsPath, "chat-transcript.html");
            if (!File.Exists(transcriptPath))
            {
                throw new FileNotFoundException("Chat transcript HTML was not found.", transcriptPath);
            }

            WebView.DefaultBackgroundColor = System.Drawing.Color.Transparent;
            WebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "chat.local",
                assetsPath,
                CoreWebView2HostResourceAccessKind.DenyCors);
            WebView.CoreWebView2.NavigationCompleted += WebView_NavigationCompleted;
            WebView.CoreWebView2.Navigate("https://chat.local/chat-transcript.html");
        }
        catch (Exception ex)
        {
            FallbackText.Text = "聊天视图初始化失败。";
            System.Diagnostics.Debug.WriteLine($"[ChatTranscriptView] WebView2 init failed: {ex.Message}");
        }
    }

    private void WebView_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!e.IsSuccess)
        {
            FallbackText.Text = "聊天视图加载失败。";
            return;
        }

        _pageReady = true;
        FallbackText.Visibility = Visibility.Collapsed;
        WebView.Visibility = Visibility.Visible;
        _ = RenderAllAsync();
    }

    private Task RenderAllAsync() => ExecuteScriptAsync(() =>
        $"window.chatTranscript.setMessages({JsonSerializer.Serialize(BuildPayloads(), JsonOptions)});");

    private Task AppendMessageAsync(ChatMessage message) => ExecuteScriptAsync(() =>
        $"window.chatTranscript.appendMessage({JsonSerializer.Serialize(CreatePayload(message), JsonOptions)});");

    private Task RemoveMessageAsync(ChatMessage message)
    {
        var key = GetMessageKey(message);
        _messageKeys.Remove(message);
        return ExecuteScriptAsync(() =>
            $"window.chatTranscript.removeMessage({JsonSerializer.Serialize(key)});");
    }

    private Task UpdateMessageAsync(ChatMessage message)
    {
        var payload = CreatePayload(message);
        return ExecuteScriptAsync(() =>
            $"window.chatTranscript.updateMessageContent({JsonSerializer.Serialize(payload.Id)}, {JsonSerializer.Serialize(payload.ContentHtml)});");
    }

    private async Task ExecuteScriptAsync(Func<string> createScript)
    {
        await _scriptGate.WaitAsync();
        try
        {
            if (!_pageReady || WebView.CoreWebView2 is null)
            {
                return;
            }

            await WebView.CoreWebView2.ExecuteScriptAsync(createScript());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ChatTranscriptView] JavaScript sync failed: {ex.Message}");
        }
        finally
        {
            _scriptGate.Release();
        }
    }

    private IReadOnlyList<TranscriptMessage> BuildPayloads() => (Messages ?? [])
        .Select(CreatePayload)
        .ToList();

    private TranscriptMessage CreatePayload(ChatMessage message) => new(
        GetMessageKey(message),
        message.IsUser ? "user" : "assistant",
        RenderMessageContent(message),
        message.Timestamp.ToString("HH:mm:ss", CultureInfo.InvariantCulture));

    private string RenderMessageContent(ChatMessage message)
    {
        if (!message.IsUser)
        {
            return _markdownRenderer.RenderBodyToHtml(message.Content);
        }

        return HtmlEncoder.Default.Encode(message.Content ?? string.Empty)
            .Replace("\r\n", "<br>", StringComparison.Ordinal)
            .Replace("\n", "<br>", StringComparison.Ordinal)
            .Replace("\r", "<br>", StringComparison.Ordinal);
    }

    private string GetMessageKey(ChatMessage message)
    {
        if (!_messageKeys.TryGetValue(message, out var key))
        {
            key = Guid.NewGuid().ToString("N");
            _messageKeys[message] = key;
        }

        return key;
    }

    private sealed record TranscriptMessage(string Id, string Role, string ContentHtml, string Timestamp);
}
