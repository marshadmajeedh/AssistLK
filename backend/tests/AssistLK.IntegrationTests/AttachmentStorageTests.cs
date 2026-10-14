using AssistLK.Infrastructure.Attachments;

namespace AssistLK.IntegrationTests;

/// <summary>
/// What this class tests:
///   File-system physical storage operations, path traversal protections, atomic staging file promotions, cleanup candidate detection, and root directory security rules for customer problem photo attachments.
/// Why these tests are needed:
///   Ensures that customer problem photos are kept in private directories (never inside public wwwroot), immune to path traversal attacks (e.g. ../outside.jpg or absolute paths), stored atomically using .stage temp files, and cleanly deleted.
/// What is real and what is mocked/faked:
///   - Real: Local file system I/O using a temporary folder in Path.GetTempPath() (PrivateFileAttachmentStorage, AttachmentStorageOptions).
///   - Mocked/Faked: None.
/// Prerequisites:
///   Local file system write permissions in Path.GetTempPath().
/// Run from repository root:
///   dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.AttachmentStorageTests"
/// </summary>
public class AttachmentStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "AssistLK-attachment-tests-" + Guid.NewGuid().ToString("N"));
    private static string Key() => Guid.NewGuid().ToString("N") + ".jpg";

    /// <summary>
    /// Purpose: Checks write, read, cleanup candidate identification, and deletion roundtrip.
    /// Why: Verifies the complete lifecycle of private attachment disk storage.
    /// Scenario: Writes 3 bytes, verifies staging file is cleaned up, reads stream, sets file modification time back 2 days, verifies cleanup candidate, and deletes.
    /// Pass condition: Stream length is 3, cleanup candidate matches key after timestamp manipulation, and OpenReadAsync throws FileNotFoundException after deletion.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.AttachmentStorageTests.PrivateStorageRoundtripAndCleanupCandidates"
    /// </summary>
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

    /// <summary>
    /// Purpose: Checks that path traversal attacks (../, ..\, absolute paths, non-32-char hex keys) throw ArgumentException.
    /// Why: Security protection against arbitrary file overwrite or read attacks via maliciously formatted storage keys.
    /// Scenario: Attempts WriteAsync, OpenReadAsync, and DeleteAsync with traversal and non-opaque keys.
    /// Pass condition: Throws ArgumentException for each operation and writes zero files to the root directory.
    /// Note: Parameterized test (Theory). The command below executes all 5 InlineData cases.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.IntegrationTests.AttachmentStorageTests.RejectsTraversalAndNonOpaqueKeys"
    /// </summary>
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

    /// <summary>
    /// Purpose: Checks that configuring attachments inside wwwroot or with whitespace throws InvalidOperationException.
    /// Why: Prevents catastrophic security misconfiguration where private customer photos become publicly accessible via static web file serving.
    /// Scenario: Attempts to resolve AttachmentStorageOptions pointing into wwwroot or blank.
    /// Pass condition: Throws InvalidOperationException and defaults safely to private-attachments.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.AttachmentStorageTests.OptionsRejectWebRootAndBlankConfiguration"
    /// </summary>
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

    /// <summary>
    /// Purpose: Checks that failing to overwrite an existing key does not leave orphaned .stage files behind.
    /// Why: Atomic writes use staging files before move; failed promotions must not pollute disk with abandoned stage files.
    /// Scenario: Attempts to overwrite an existing storage key with new data.
    /// Pass condition: Throws IOException, .stage file does not exist, and original byte content is unchanged.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.IntegrationTests.AttachmentStorageTests.FailedPromotionDoesNotLeaveStagingFile"
    /// </summary>
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
