namespace CoffeeNChill.API.DTOs.Documents
{
    public class DocumentUploadResponse
    {
        public string Message { get; set; } = string.Empty;

        public string DownloadUrl { get; set; } = string.Empty;

        public StaffDocumentResponse? Document { get; set; }
    }
}
