namespace AssistLK.Domain.Entities;

public class ServiceRequestAttachment : BaseEntity
{
    public Guid ServiceRequestId { get; set; }
    public ServiceRequest ServiceRequest { get; set; } = null!;
    public string StorageKey { get; set; } = string.Empty;
    public string ContentType { get; set; } = "image/jpeg";
    public long FileSizeBytes { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int Slot { get; set; }
    public string ContentHash { get; set; } = string.Empty;
}
