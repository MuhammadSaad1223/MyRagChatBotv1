using Microsoft.AspNetCore.Components.Forms;

namespace MyRagChatBot.Services
{
    public interface IDocumentService
    {
        Task<string> ProcessTextFile(IBrowserFile file);
        Task<string> ExtractTextFromFile(IBrowserFile file);
        List<string> SplitIntoChunks(string text, int chunkSize = 1000);
    }
}
