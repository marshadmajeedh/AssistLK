namespace AssistLK.Infrastructure.Attachments;

public sealed class AttachmentStorageOptions
{
    public string RootPath { get; set; } = "private-attachments";

    public string Resolve(string contentRoot, string? webRoot = null)
    {
        if (string.IsNullOrWhiteSpace(RootPath)) throw new InvalidOperationException("AttachmentStorage:RootPath is required.");
        var root = Path.GetFullPath(RootPath, Path.GetFullPath(contentRoot));
        var publicRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(webRoot ?? Path.Combine(contentRoot, "wwwroot")));
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (root.Equals(publicRoot, comparison) || root.StartsWith(publicRoot + Path.DirectorySeparatorChar, comparison))
            throw new InvalidOperationException("Attachment storage must be outside the web root.");
        for (var current = new DirectoryInfo(root); current != null; current = current.Parent)
            if (current.Exists && current.Attributes.HasFlag(FileAttributes.ReparsePoint))
                throw new InvalidOperationException("Attachment storage cannot traverse a symbolic link or junction.");
        Directory.CreateDirectory(root);
        return root;
    }
}
