namespace CoffeeNChill.API.DTOs.Documents
{
    public class StaffDocumentResponse
    {
        public string FileName { get; set; } = string.Empty;

        public string FileExtension { get; set; } = string.Empty;

        public string ContentType { get; set; } = string.Empty;

        public long FileSize { get; set; }

        public DateTime UploadedOn { get; set; }

        public string ContainerName { get; set; } = string.Empty;
    }
}
