using AssistLK.Infrastructure.Attachments;

namespace AssistLK.IntegrationTests;

public class AttachmentStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "AssistLK-attachment-tests-" + Guid.NewGuid().ToString("N"));
    private static string Key() => Guid.NewGuid().ToString("N") + ".jpg";

    [Fact]
    public async Task PrivateStorageRoundtripAndCleanupCandidates()
    {
        var storage = new PrivateFileAttachmentStorage(_root);
        var key = Key();
        await storage.WriteAsync(key, new byte[] { 1, 2, 3 });
        Assert.False(File.Exists(Path.Combine(_root, key + ".stage")));
        await using (var stream = await storage.OpenReadAsync(key)) Assert.Equal(3, stream.Length);
        Assert.Empty(storage.GetCleanupCandidates(DateTime.UtcNow.AddDays(-1)));
        File.SetLastWriteTimeUtc(Path.Combine(_root, key), DateTime.UtcNow.AddDays(-2));
        Assert.Equal(key, Assert.Single(storage.GetCleanupCandidates(DateTime.UtcNow.AddDays(-1))).Key);
        await storage.DeleteAsync(key);
        await Assert.ThrowsAsync<FileNotFoundException>(() => storage.OpenReadAsync(key));
    }

    [Theory]
    [InlineData("../outside.jpg")][InlineData("..\\outside.jpg")]
    [InlineData("C:/outside.jpg")][InlineData("abc.jpg")][InlineData("/tmp/a.jpg")]
    public async Task RejectsTraversalAndNonOpaqueKeys(string key)
    {
        var storage = new PrivateFileAttachmentStorage(_root);
        await Assert.ThrowsAsync<ArgumentException>(() => storage.WriteAsync(key, new byte[] { 1 }));
        await Assert.ThrowsAsync<ArgumentException>(() => storage.OpenReadAsync(key));
        await Assert.ThrowsAsync<ArgumentException>(() => storage.DeleteAsync(key));
        Assert.Empty(Directory.GetFiles(_root));
    }

    [Fact]
    public void OptionsRejectWebRootAndBlankConfiguration()
    {
        Assert.Throws<InvalidOperationException>(() => new AttachmentStorageOptions { RootPath = "wwwroot/photos" }.Resolve(_root));
        Assert.Throws<InvalidOperationException>(() => new AttachmentStorageOptions { RootPath = "wwwroot/photos" }
            .Resolve(_root, Path.Combine(_root, "wwwroot") + Path.DirectorySeparatorChar));
        Assert.Throws<InvalidOperationException>(() => new AttachmentStorageOptions { RootPath = " " }.Resolve(_root));
        var resolved = new AttachmentStorageOptions().Resolve(_root);
        Assert.Equal(Path.Combine(_root, "private-attachments"), resolved);
    }

    [Fact]
    public async Task FailedPromotionDoesNotLeaveStagingFile()
    {
        var storage = new PrivateFileAttachmentStorage(_root); var key = Key();
        await storage.WriteAsync(key, new byte[] { 1 });
        await Assert.ThrowsAsync<IOException>(() => storage.WriteAsync(key, new byte[] { 2 }));
        Assert.False(File.Exists(Path.Combine(_root, key + ".stage")));
        Assert.Equal(new byte[] { 1 }, await File.ReadAllBytesAsync(Path.Combine(_root, key)));
    }

    public void Dispose()
    {
        // Only the exact unique temporary directory created by this fixture.
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
