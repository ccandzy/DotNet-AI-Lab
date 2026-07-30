using System.Text.Json;
using Markdig;

namespace AiChatClient.Services.Impl
{
    public class MarkdownRendererService : AiChatClient.Services.IMarkdownRendererService
    {
        private readonly MarkdownPipeline _pipeline;

        public MarkdownRendererService()
        {
            _pipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .Build();
        }

        /// <summary>
        /// Returns just the inner body HTML (no &lt;html&gt;/&lt;head&gt;/&lt;body&gt; wrapper).
        /// Used for incremental JS-based updates in WebView2.
        /// </summary>
        public string RenderBodyToHtml(string markdown)
        {
            if (markdown is null) markdown = string.Empty;
            return Markdig.Markdown.ToHtml(markdown, _pipeline);
        }

        /// <summary>
        /// Returns a full standalone HTML page with styles, highlight.js and the
        /// <c>refreshContent()</c> function for incremental updates.
        /// </summary>
        public string RenderToHtml(string markdown)
        {
            if (markdown is null) markdown = string.Empty;

            var body = Markdig.Markdown.ToHtml(markdown, _pipeline);

            var html =
                "<!doctype html>\n" +
                "<html>\n" +
                "<head>\n" +
                "<meta charset=\"utf-8\" />\n" +
                "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />\n" +
                "<link rel=\"stylesheet\" href=\"https://cdnjs.cloudflare.com/ajax/libs/highlight.js/11.8.0/styles/github.min.css\">\n" +
                "<style>\n" +
                "    body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial; padding:12px 16px; color:#1f2937; background:transparent; word-wrap:break-word; font-size:14px; margin:0; }\n" +
                "    pre { overflow:auto; border-radius:6px; padding:12px; margin:8px 0; }\n" +
                "    code { font-family: Consolas, 'Courier New', monospace; font-size:0.9em; }\n" +
                "    p { margin:4px 0; line-height:1.6; }\n" +
                "    h1, h2, h3, h4 { margin:12px 0 6px 0; color:#111827; }\n" +
                "    h1 { font-size:1.25em; }\n" +
                "    h2 { font-size:1.15em; }\n" +
                "    h3 { font-size:1.08em; }\n" +
                "    ul, ol { padding-left:20px; margin:4px 0; }\n" +
                "    li { margin:2px 0; }\n" +
                "    blockquote { border-left:3px solid #d1d5db; padding-left:12px; margin:8px 0; color:#6b7280; }\n" +
                "    img { max-width:100%; height:auto; border-radius:4px; }\n" +
                "    table { border-collapse: collapse; width:100%; margin:8px 0; }\n" +
                "    table, th, td { border: 1px solid #d1d5db; padding:6px; }\n" +
                "    th { background:#f3f4f6; }\n" +
                "    hr { border:none; border-top:1px solid #e5e7eb; margin:12px 0; }\n" +
                "    a { color:#2563eb; text-decoration:none; }\n" +
                "    a:hover { text-decoration:underline; }\n" +
                "    strong { color:#111827; }\n" +
                "</style>\n" +
                "</head>\n" +
                "<body>\n" +
                "<div id=\"content\">" + body + "</div>\n" +
                "<script src=\"https://cdnjs.cloudflare.com/ajax/libs/highlight.js/11.8.0/highlight.min.js\"></script>\n" +
                "<script>\n" +
                "hljs.highlightAll();\n" +
                "window.scrollTo(0, document.body.scrollHeight);\n" +
                "\n" +
                "// Public function: called from C# via ExecuteScriptAsync to refresh content without full page reload\n" +
                "// Returns document.body.scrollHeight so C# can resize the WebView2 control.\n" +
                "function refreshContent(html) {\n" +
                "    document.getElementById('content').innerHTML = html;\n" +
                "    hljs.highlightAll();\n" +
                "    window.scrollTo(0, document.body.scrollHeight);\n" +
                "    return document.body.scrollHeight;\n" +
                "}\n" +
                "</script>\n" +
                "</body>\n" +
                "</html>";

            return html;
        }
    }
}
