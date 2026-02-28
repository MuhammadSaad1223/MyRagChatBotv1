using Markdig;

namespace MyRagChatBot.Services
{
    public class MarkdownService
    {
        private readonly MarkdownPipeline _pipeline;

        public MarkdownService()
        {
            _pipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .Build();
        }

        public string ConvertToHtml(string markdown)
        {
            if (string.IsNullOrEmpty(markdown))
                return string.Empty;

            return Markdig.Markdown.ToHtml(markdown, _pipeline);
        }

        public string ConvertToPlainText(string markdown)
        {
            if (string.IsNullOrEmpty(markdown))
                return string.Empty;

          
            var html = Markdig.Markdown.ToHtml(markdown, _pipeline);
            // Convert HTML to plain text (simplified)
            return System.Net.WebUtility.HtmlDecode(
                System.Text.RegularExpressions.Regex.Replace(
                    html, "<[^>]*>", " "));
        }
    }
}