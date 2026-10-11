namespace AssistLK.Application.Attachments;

public interface IProofOfWorkStorage
{
    Task<string> UploadAsync(
        Stream content,
        string fileName,
        string? contentType,
        Guid jobId,
        CancellationToken cancellationToken = default);
}