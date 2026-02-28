using MyRagChatBot.Models;

namespace MyRagChatBot.Services
{
    public interface IVectorDatabase
    {
        Task StoreDocumentChunk(DocumentChunk chunk);
        Task<List<DocumentChunk>> SearchSimilarChunks(float[] queryEmbedding, int topK = 5);
        Task<List<DocumentChunk>> GetAllChunks();
        Task ClearAllChunks();
    }
}