using AssistLK.Application.Attachments;

namespace AssistLK.Infrastructure.Attachments;

public sealed class PrivateFileAttachmentStorage : IServiceRequestAttachmentStorage
{
    private readonly string _root;
    public PrivateFileAttachmentStorage(string root)
    {
        _root = Path.GetFullPath(root);
        Directory.CreateDirectory(_root);
        EnsurePrivateDirectory();
    }

    private static bool ValidKey(string key)
        => key.Length == 36 && key.EndsWith(".jpg", StringComparison.Ordinal)
            && Guid.TryParseExact(key[..32], "N", out _);

    private void EnsurePrivateDirectory()
    {
        for (var current = new DirectoryInfo(_root); current != null; current = current.Parent)
            if (current.Exists && current.Attributes.HasFlag(FileAttributes.ReparsePoint))
                throw new IOException("Attachment storage cannot traverse symbolic links.");
    }

    private string FilePath(string key, bool staged = false)
    {
        EnsurePrivateDirectory();
        if (!ValidKey(key)) throw new ArgumentException("Invalid attachment storage key.");
        var path = Path.Combine(_root, key + (staged ? ".stage" : ""));
        if (File.Exists(path) && File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint))
            throw new IOException("Attachment objects cannot be symbolic links.");
        return path;
    }

    public async Task WriteAsync(string key, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default)
    {
        var staged = FilePath(key, true);
        try
        {
            await using (var stream = new FileStream(staged, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                81920, FileOptions.Asynchronous))
            {
                await stream.WriteAsync(content, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }
            File.Move(staged, FilePath(key), overwrite: false);
        }
        catch
        {
            // A residual stage file is also discoverable by reconciliation.
            try { File.Delete(staged); } catch (IOException) { }
            throw;
        }
    }

    public Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<Stream>(new FileStream(FilePath(key), FileMode.Open, FileAccess.Read,
            FileShare.Read | FileShare.Delete, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan));
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        File.Delete(FilePath(key));
        File.Delete(FilePath(key, true));
        return Task.CompletedTask;
    }

    public IReadOnlyList<StoredAttachmentFile> GetCleanupCandidates(DateTime olderThanUtc)
    {
        EnsurePrivateDirectory();
        return new DirectoryInfo(_root).EnumerateFiles().Where(f => !f.Attributes.HasFlag(FileAttributes.ReparsePoint))
            .Where(f => f.LastWriteTimeUtc < olderThanUtc)
            .Select(f => new StoredAttachmentFile(f.Name.EndsWith(".stage", StringComparison.Ordinal)
                ? f.Name[..^6] : f.Name, f.LastWriteTimeUtc))
            .Where(f => ValidKey(f.Key)).DistinctBy(f => f.Key).ToArray();
    }
}
