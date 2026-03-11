using System.Text.Json;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyRagChatBot.Models
{
    public class DocumentChunk
    {
        public int Id { get; set; }
        public string DocumentName { get; set; } = "";
        public string Content { get; set; } = "";
        public string EmbeddingJson { get; set; } = "";
        public DateTime UploadedDate { get; set; } = DateTime.Now;
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime LastModified { get; set; } = DateTime.Now;
       
        ///\\\/////  For PDF/File Upload feature \\\//////////
        public float[] Embedding { get; set; } = Array.Empty<float>();
        public string FileName { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; }
       
        [NotMapped]
        public double SimilarityScore { get; set; }

        // Helper method to get embedding as array
        public float[] GetEmbedding()
        {
            try
            {
                if (string.IsNullOrEmpty(EmbeddingJson))
                    return Array.Empty<float>();

                return JsonSerializer.Deserialize<float[]>(EmbeddingJson)
                       ?? Array.Empty<float>();
            }
            catch
            {
                return Array.Empty<float>();
            }
        }

        // Helper method to set embedding
        public void SetEmbedding(float[] embedding)
        {
            EmbeddingJson = JsonSerializer.Serialize(embedding);
        }
    }
}
