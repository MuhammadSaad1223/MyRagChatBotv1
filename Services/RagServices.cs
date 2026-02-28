using Microsoft.AspNetCore.Components.Forms;
using MyRagChatBot.Components.Pages;
using MyRagChatBot.Models;
using System.Text;

namespace MyRagChatBot.Services
{
    public class RagService
    {
        private readonly IGeminiAIService _geminiAIService;
        private readonly IVectorDatabase _vectorDatabase;
        private readonly IDocumentService _documentService;
        private readonly ILogger<RagService> _logger;

        public RagService(
            IGeminiAIService geminiAIService,
            IVectorDatabase vectorDatabase,
            IDocumentService documentService,
            ILogger<RagService> logger)
        {
            _geminiAIService = geminiAIService;
            _vectorDatabase = vectorDatabase;
            _documentService = documentService;
            _logger = logger;

            _logger.LogInformation("RagService initialized with Gemini AI");
        }

        //  Main RAG pipeline for processing user queries
        public async Task<string> ProcessQuery(string query)
        {
            _logger.LogInformation($"Processing query: {query}");

            try
            {
                // Step 1: Get embedding for the query text
                var queryEmbedding = await _geminiAIService.CreateEmbeddingAsync(query);

                if (queryEmbedding.Length == 0)
                {
                    _logger.LogWarning("Failed to get query embedding, falling back to simple chat");
                    return await _geminiAIService.SimpleChat(query);
                }

                // Step 2: Search the vector DB for the most similar document chunks
                var similarChunks = await _vectorDatabase.SearchSimilarChunks(queryEmbedding, topK: 5);

                if (similarChunks == null || similarChunks.Count == 0)
                {
                    _logger.LogInformation("No relevant documents found, using simple chat");
                    return await _geminiAIService.SimpleChat(query);
                }

                // Step 3: Build a contextual prompt from retrieved chunks
                string context = BuildContextFromChunks(similarChunks);

                // Step 4: Query Gemini with context and user query to get the final answer
                var answer = await _geminiAIService.GetChatResponse(query, context);

                _logger.LogInformation("Successfully generated RAG response");
                return answer;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in RAG pipeline");
                return $"Error: {ex.Message}. Please try again.";
            }
        }

        //  Process an uploaded document file and store embeddings in vector DB
        public async Task<string> ProcessDocument(IBrowserFile file)
        {
            try
            {
                var text = await _documentService.ExtractTextFromFile(file);

                if (string.IsNullOrEmpty(text))
                    return "Could not extract text from file.";

                var chunks = _documentService.SplitIntoChunks(text, 800);
                int processedCount = 0;

                foreach (var chunk in chunks)
                {
                    if (string.IsNullOrWhiteSpace(chunk))
                        continue;

                    var embedding = await _geminiAIService.CreateEmbeddingAsync(chunk);

                    if (embedding.Length == 0)
                        continue;

                    var documentChunk = new DocumentChunk
                    {
                        DocumentName = file.Name,
                        Content = chunk,
                        UploadedDate = DateTime.Now,
                        CreatedDate = DateTime.Now,
                        LastModified = DateTime.Now
                    };

                    documentChunk.SetEmbedding(embedding);

                    await _vectorDatabase.StoreDocumentChunk(documentChunk);
                    processedCount++;

                    _logger.LogInformation($"Processed chunk {processedCount} of {chunks.Count}");
                }

                return $"Successfully processed {file.Name}";
               //return $"Successfully processed {processedCount} Chunks from{file.Name}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing document");
                return $"Error: {ex.Message}";
            }
        }

        // Helper method to create a single context string from document chunks
        private string BuildContextFromChunks(List<DocumentChunk> chunks)
        {
            var context = new StringBuilder();
            int chunkNumber = 1;

            context.AppendLine("Based on the following document content:");
            context.AppendLine();

            foreach (var chunk in chunks)
            {
                context.AppendLine($"--- Section {chunkNumber} from '{chunk.DocumentName}' ---");
                context.AppendLine(chunk.Content);
                context.AppendLine();
                chunkNumber++;
            }

            context.AppendLine("Please answer the user's question based on the above context.");
            return context.ToString().Trim();
        }

        // Optional helpers
        public async Task<List<DocumentChunk>> GetAllStoredChunks() => await _vectorDatabase.GetAllChunks();

        public async Task ClearAllDocuments()
        {
            await _vectorDatabase.ClearAllChunks();
            _logger.LogInformation("All documents cleared from vector database");
        }

        public async Task<string> TestGeminiConnection()
        {
            try
            {
                var response = await _geminiAIService.SimpleChat("Hello, are you working?");
                return $"Gemini Connection Test: {response.Substring(0, Math.Min(50, response.Length))}...";
            }
            catch (Exception ex)
            {
                return $"Gemini Connection Failed: {ex.Message}";
            }
        }
    }
}
