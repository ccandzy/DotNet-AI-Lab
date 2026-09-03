namespace AiChatClient.Services
{
    public interface IMarkdownRendererService
    {
        /// <summary>
        /// Convert markdown to full HTML page ready to be displayed in WebView2
        /// </summary>
        string RenderToHtml(string markdown);

        /// <summary>
        /// Convert markdown to HTML body fragment (no &lt;html&gt;/&lt;head&gt;/&lt;body&gt; wrapper).
        /// Used for incremental JS updates in WebView2.
        /// </summary>
        string RenderBodyToHtml(string markdown);
    }
}
