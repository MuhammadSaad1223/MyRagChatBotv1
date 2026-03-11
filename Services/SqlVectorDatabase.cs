using Microsoft.EntityFrameworkCore;
using MyRagChatBot.Data;
using MyRagChatBot.Models;

namespace MyRagChatBot.Services
{
    public class SqlVectorDatabase : IVectorDatabase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<SqlVectorDatabase> _logger;

        public SqlVectorDatabase(AppDbContext context, ILogger<SqlVectorDatabase> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task StoreDocumentChunk(DocumentChunk chunk)
        {
            try
            {
                _context.DocumentChunks.Add(chunk);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Stored chunk {chunk.Id}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error storing document chunk");
                throw;
            }
        }

        public async Task<List<DocumentChunk>> SearchSimilarChunks(float[] queryEmbedding, int topK = 5)
        {
            try
            {
                //var allChunks = await _context.DocumentChunks.ToListAsync();
                var allChunks = await _context.DocumentChunks
                .OrderByDescending(c => c.UploadedDate)
                .Take(200)
                .ToListAsync();

                if (allChunks.Count == 0)
                {
                    _logger.LogWarning("No document chunks found in database.");
                    return new List<DocumentChunk>();
                }

                var chunksWithSimilarity = new List<DocumentChunk>();

                foreach (var chunk in allChunks)
                {
                    var chunkEmbedding = chunk.GetEmbedding();

                    if (chunkEmbedding == null || chunkEmbedding.Length == 0)
                    {
                        _logger.LogWarning($"Chunk {chunk.Id} has empty embedding.");
                        continue;
                    }

                    if (chunkEmbedding.Length != queryEmbedding.Length)
                    {
                        _logger.LogWarning($"Embedding size mismatch for chunk {chunk.Id}");
                        continue;
                    }

                    var similarity = CalculateCosineSimilarity(queryEmbedding, chunkEmbedding);

                    _logger.LogInformation($"Chunk {chunk.Id} similarity: {similarity}");

                    chunk.SimilarityScore = similarity;
                    chunksWithSimilarity.Add(chunk);
                }


                var topChunks = chunksWithSimilarity
                .OrderByDescending(c => c.SimilarityScore)
                .Take(topK)
                .ToList();

                _logger.LogInformation($"Returning {topChunks.Count} similar chunks.");

                return topChunks;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching similar chunks");
                return new List<DocumentChunk>();
            }
        }

        public async Task<List<DocumentChunk>> GetAllChunks()
        {
            return await _context.DocumentChunks.ToListAsync();
        }

        public async Task ClearAllChunks()
        {
            _context.DocumentChunks.RemoveRange(_context.DocumentChunks);
            await _context.SaveChangesAsync();
        }

        // Helper method to calculate cosine similarity
        private double CalculateCosineSimilarity(float[] vectorA, float[] vectorB)
        {
            if (vectorA.Length != vectorB.Length)
                return 0.0;

            double dotProduct = 0.0;
            double magnitudeA = 0.0;
            double magnitudeB = 0.0;

            for (int i = 0; i < vectorA.Length; i++)
            {
                dotProduct += vectorA[i] * vectorB[i];
                magnitudeA += Math.Pow(vectorA[i], 2);
                magnitudeB += Math.Pow(vectorB[i], 2);
            }

            magnitudeA = Math.Sqrt(magnitudeA);
            magnitudeB = Math.Sqrt(magnitudeB);

            if (magnitudeA == 0 || magnitudeB == 0)
                return 0.0;

            return dotProduct / (magnitudeA * magnitudeB);
        }
    }
}
