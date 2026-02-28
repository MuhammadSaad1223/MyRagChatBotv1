using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MyRagChatBot.Services
{
    public class GeminiAIService : IGeminiAIService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<GeminiAIService> _logger;
        private readonly string _apiKey;
        private readonly string _model;
        private readonly string _embeddingModel;

        public GeminiAIService(
            HttpClient httpClient,
            ILogger<GeminiAIService> logger,
            IConfiguration configuration)
        {
            _httpClient = httpClient;
            _logger = logger;

            _apiKey = configuration["Gemini:ApiKey"]
                ?? throw new InvalidOperationException("Gemini API Key is missing.");

            _model = configuration["Gemini:Model"] ?? "gemini-2.5-flash";
            _embeddingModel = configuration["Gemini:EmbeddingModel"] ?? "embedding-001";

            var baseUrl = configuration["Gemini:BaseUrl"]
                          ?? "https://generativelanguage.googleapis.com/v1beta/";

            if (!baseUrl.EndsWith("/"))
                baseUrl += "/";

            _httpClient.BaseAddress = new Uri(baseUrl);
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "MyRagChatBot/1.0");

            _logger.LogInformation("GeminiAIService initialized successfully.");
        }

        // -----------------------------
        // SIMPLE CHAT
        // -----------------------------
        public async Task<string> SimpleChat(string message)
        {
            var endpoint = $"models/{_model}:generateContent?key={_apiKey}";

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = message }
                        }
                    }
                }
            };

            return await SendGenerateRequest(endpoint, requestBody);
        }

        // -----------------------------
        // RAG CHAT RESPONSE
        // -----------------------------
        public async Task<string> GetChatResponse(string userQuestion, string context = "")
        {
            string prompt = string.IsNullOrEmpty(context)
                ? userQuestion
                : $"Context:\n{context}\n\nQuestion: {userQuestion}\n\nAnswer based only on the context:";

            var endpoint = $"models/{_model}:generateContent?key={_apiKey}";

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = prompt }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature = 0.3,
                    maxOutputTokens = 2048
                }
            };

            return await SendGenerateRequest(endpoint, requestBody);
        }

        // -----------------------------
        // CREATE EMBEDDING
        // -----------------------------
        public async Task<float[]> CreateEmbeddingAsync(string text)
        {
            try
            {
                var endpoint = $"models/{_embeddingModel}:embedContent?key={_apiKey}";

                var requestBody = new
                {
                    model = $"models/{_embeddingModel}",
                    content = new
                    {
                        parts = new[]
                        {
                            new { text = text }
                        }
                    }
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(endpoint, content);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Embedding API error: {Error}", error);
                    return Array.Empty<float>();
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseJson);

                if (doc.RootElement.TryGetProperty("embedding", out var embeddingObj) &&
                    embeddingObj.TryGetProperty("values", out var values))
                {
                    return values.EnumerateArray()
                                 .Select(x => x.GetSingle())
                                 .ToArray();
                }

                return Array.Empty<float>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating embedding");
                return Array.Empty<float>();
            }
        }

        // -----------------------------
        // DUMMY (Optional – Remove if not needed)
        // -----------------------------
        public async Task<float[]> GetEmbedding(string text)
        {
            return await CreateEmbeddingAsync(text);
        }

        // -----------------------------
        // SHARED GENERATE METHOD
        // -----------------------------
        private async Task<string> SendGenerateRequest(string endpoint, object requestBody)
        {
            try
            {
                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(endpoint, content);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Gemini API error: {Error}", error);
                    return "AI service error occurred.";
                }

                var responseJson = await response.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(responseJson);
                var root = doc.RootElement;

                if (root.TryGetProperty("candidates", out var candidates) &&
                    candidates.GetArrayLength() > 0 &&
                    candidates[0].TryGetProperty("content", out var contentObj) &&
                    contentObj.TryGetProperty("parts", out var parts) &&
                    parts.GetArrayLength() > 0 &&
                    parts[0].TryGetProperty("text", out var text))
                {
                    return CleanResponse(text.GetString() ?? "");
                }

                return "Unexpected AI response format.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Gemini API");
                return "AI service exception occurred.";
            }
        }

        // -----------------------------
        // RESPONSE CLEANER
        // -----------------------------
        private string CleanResponse(string response)
        {
            if (string.IsNullOrEmpty(response))
                return response;

            response = response.Replace("## ", "")
                               .Replace("### ", "")
                               .Replace("# ", "")
                               .Replace("***", "")
                               .Replace("---", "")
                               .Replace("*", "");

            response = Regex.Replace(response, @"\s+", " ");
            response = Regex.Replace(response, @"\n{3,}", "\n\n");

            return response.Trim();
        }
    }

    // -----------------------------
    // INTERFACE
    // -----------------------------
    public interface IGeminiAIService
    {
        Task<string> SimpleChat(string message);
        Task<string> GetChatResponse(string userQuestion, string context = "");
        Task<float[]> GetEmbedding(string text);
        Task<float[]> CreateEmbeddingAsync(string text);
    }
}
