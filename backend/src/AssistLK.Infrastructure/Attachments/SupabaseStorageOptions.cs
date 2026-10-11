namespace AssistLK.Infrastructure.Attachments;

public sealed class SupabaseStorageOptions
{
    public string Url { get; set; } = string.Empty;
    public string ServiceRoleKey { get; set; } = string.Empty;
    public string ProofOfWorkBucket { get; set; } = "proof_of_work_images";

    public void Validate()
    {
        if (!Uri.TryCreate(Url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException("Supabase:Url must be an absolute HTTP or HTTPS URL.");
        }

        if (string.IsNullOrWhiteSpace(ServiceRoleKey))
        {
            throw new InvalidOperationException("Supabase:ServiceRoleKey is required.");
        }

        if (string.IsNullOrWhiteSpace(ProofOfWorkBucket))
        {
            throw new InvalidOperationException("Supabase:ProofOfWorkBucket is required.");
        }
    }
}