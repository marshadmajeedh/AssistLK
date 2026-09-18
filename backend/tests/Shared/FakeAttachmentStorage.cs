using System.Collections.Concurrent;
using AssistLK.Application.Attachments;

namespace AssistLK.Tests.Shared;

public sealed class FakeAttachmentStorage : IServiceRequestAttachmentStorage
{
    public ConcurrentDictionary<string, byte[]> Files { get; } = new();
    public bool FailWrite { get; set; }
    public bool FailDelete { get; set; }
    public Task WriteAsync(string key, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default)
    {
        if (FailWrite) throw new IOException("Simulated storage unavailable");
        Files[key] = content.ToArray();
        return Task.CompletedTask;
    }
    public Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken = default)
        => Task.FromResult<Stream>(Files.TryGetValue(key, out var bytes)
            ? new MemoryStream(bytes) : throw new FileNotFoundException());
    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        if (FailDelete) throw new IOException("Simulated delete failure");
        Files.TryRemove(key, out _);
        return Task.CompletedTask;
    }
    public IReadOnlyList<StoredAttachmentFile> GetCleanupCandidates(DateTime olderThanUtc)
        => Files.Keys.Select(k => new StoredAttachmentFile(k, DateTime.UtcNow.AddDays(-2))).ToArray();
}
