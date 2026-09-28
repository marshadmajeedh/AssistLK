using System.Net.Http.Headers;
using AssistLK.Application.Attachments;

namespace AssistLK.Infrastructure.Attachments;

public sealed class SupabaseProofOfWorkStorage : IProofOfWorkStorage
{
    private readonly HttpClient _httpClient;
    private readonly SupabaseStorageOptions _options;

    public SupabaseProofOfWorkStorage(
        HttpClient httpClient,
        SupabaseStorageOptions options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public async Task<string> UploadAsync(
        Stream content,
        string fileName,
        string? contentType,
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        _options.Validate();
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var path = $"jobs/{jobId:N}/{Guid.NewGuid():N}{extension}";
        var encodedPath = string.Join(
            "/",
            path.Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Select(Uri.EscapeDataString));
        var endpoint = $"{_options.Url.TrimEnd('/')}/storage/v1/object/{Uri.EscapeDataString(_options.ProofOfWorkBucket)}/{encodedPath}";

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ServiceRoleKey);
        request.Headers.Add("apikey", _options.ServiceRoleKey);

        var streamContent = new StreamContent(content);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);
        request.Content = streamContent;

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Supabase proof-of-work upload failed ({(int)response.StatusCode}): {error}");
        }

        return $"{_options.Url.TrimEnd('/')}/storage/v1/object/public/{Uri.EscapeDataString(_options.ProofOfWorkBucket)}/{encodedPath}";
    }
}