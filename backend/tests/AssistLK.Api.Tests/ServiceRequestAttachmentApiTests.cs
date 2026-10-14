using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using AssistLK.Application.Attachments;
using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;
using ImageMagick;
using Microsoft.EntityFrameworkCore;

namespace AssistLK.Api.Tests;

/// <summary>
/// ServiceRequestAttachmentApiTests verifies authorization, security, lifecycle rules,
/// and image validation for the Service Request Attachment endpoints (/api/service-requests/{id}/attachments).
///
/// Viva Preparation Notes:
/// - What this class tests: Attachment upload, listing, download, and deletion APIs. Covers RBAC
///   (Customer-only access), multi-tenant ownership enforcement, lifecycle state mutability rules,
///   maximum upload limits (3 photos max, 5MB max), magic-byte image validation, and security headers.
/// - Why these tests are needed: Customer-uploaded photos provide visual evidence for AI problem understanding.
///   The system must ensure that files are valid images, cannot exceed storage quotas, cannot leak internal
///   storage paths, and cannot be tampered with by unauthorized users.
/// - What is real vs mocked:
///   * Real: ASP.NET Core test server, multipart form parser, ImageMagick image decoding,
///     JWT authentication and RBAC policies, and EF Core InMemory database.
///   * Mocked/Faked: Storage runs local/in-memory test storage instead of cloud blob storage.
/// - Prerequisites: None (runs in-memory with .NET 8 SDK and Magick.NET).
/// - PowerShell command to run class from repository root:
///   dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.Api.Tests.ServiceRequestAttachmentApiTests"
/// </summary>
public class ServiceRequestAttachmentApiTests
{
    private static byte[] Photo()
    {
        using var image = new MagickImage(MagickColors.Blue, 24, 16);
        return image.ToByteArray(MagickFormat.Jpeg);
    }
    private static MultipartFormDataContent Body(byte[]? bytes = null, string name = "photo.jpg", string type = "image/jpeg")
    {
        var body = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes ?? Photo());
        file.Headers.ContentType = new MediaTypeHeaderValue(type);
        body.Add(file, "file", name);
        return body;
    }
    private static async Task<Guid> Seed(AssistLKApiTestFactory f, Guid owner, ServiceRequestStatus status = ServiceRequestStatus.Created)
    {
        var request = new ServiceRequest { CustomerId = owner, Description = "Leaking pipe", LocationText = "Colombo", Status = status };
        await f.SeedAsync(db => { db.ServiceRequests.Add(request); return Task.CompletedTask; });
        return request.Id;
    }

    /// <summary>
    /// Purpose: Checks that only authenticated users with the Customer role can upload attachments.
    /// Why: Service providers and administrators should not be able to upload photos to a customer's personal request.
    /// Scenario: Anonymous user, Provider, and Admin attempt to post a photo to the attachments endpoint.
    /// Pass condition: Anonymous returns 401 Unauthorized; Provider and Admin return 403 Forbidden.
    /// Note: Parameterized test (Theory). The command below executes all 3 InlineData cases.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.Api.Tests.ServiceRequestAttachmentApiTests.OnlyCustomersCanUpload"
    /// </summary>
    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData(UserRole.Provider, HttpStatusCode.Forbidden)]
    [InlineData(UserRole.Admin, HttpStatusCode.Forbidden)]
    public async Task OnlyCustomersCanUpload(UserRole? role, HttpStatusCode expected)
    {
        await using var f = new AssistLKApiTestFactory();
        using var client = role.HasValue ? f.CreateAuthenticatedClient(Guid.NewGuid(), role.Value) : f.CreateClient();
        using var body = Body();
        Assert.Equal(expected, (await client.PostAsync($"/api/service-requests/{Guid.NewGuid()}/attachments", body)).StatusCode);
    }

    /// <summary>
    /// Purpose: Checks that a customer cannot view, upload to, download content from, or delete attachments on another customer's request.
    /// Why: Protects multi-tenant isolation and prevents unauthorized users from inspecting or modifying private photos.
    /// Scenario: Customer B calls POST, GET, GET /content, and DELETE against Customer A's service request ID.
    /// Pass condition: The API responds with HTTP 404 Not Found for every operation.
    /// Note: Parameterized test (Theory). The command below executes all 4 InlineData cases.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.Api.Tests.ServiceRequestAttachmentApiTests.OwnershipCannotBeBypassed"
    /// </summary>
    [Theory]
    [InlineData("POST")][InlineData("GET")][InlineData("CONTENT")][InlineData("DELETE")]
    public async Task OwnershipCannotBeBypassed(string method)
    {
        await using var f = new AssistLKApiTestFactory();
        var id = await Seed(f, Guid.NewGuid());
        using var client = f.CreateAuthenticatedClient(Guid.NewGuid(), UserRole.Customer);
        var url = $"/api/service-requests/{id}/attachments";
        using var body = Body();
        var response = method switch
        {
            "POST" => await client.PostAsync(url, body),
            "GET" => await client.GetAsync(url),
            "CONTENT" => await client.GetAsync(url + $"/{Guid.NewGuid()}/content"),
            _ => await client.DeleteAsync(url + $"/{Guid.NewGuid()}")
        };
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Purpose: Checks that attachment upload and deletion follow the service request lifecycle rules.
    /// Why: Ensures photos can only be added/removed when the request is editable (Created, AwaitingInformation), not while locked.
    /// Scenario: Customer attempts upload and delete in Created, AwaitingInformation, Analyzing, Analyzed, ReadyForMatching, and Cancelled states.
    /// Pass condition: Created and AwaitingInformation return 201/204; all locked states return 409 Conflict.
    /// Note: Parameterized test (Theory). The command below executes all 6 InlineData cases.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.Api.Tests.ServiceRequestAttachmentApiTests.MutationUsesExistingLifecycle"
    /// </summary>
    [Theory]
    [InlineData(ServiceRequestStatus.Created, true)]
    [InlineData(ServiceRequestStatus.AwaitingInformation, true)]
    [InlineData(ServiceRequestStatus.Analyzing, false)]
    [InlineData(ServiceRequestStatus.Analyzed, false)]
    [InlineData(ServiceRequestStatus.ReadyForMatching, false)]
    [InlineData(ServiceRequestStatus.Cancelled, false)]
    public async Task MutationUsesExistingLifecycle(ServiceRequestStatus status, bool allowed)
    {
        await using var f = new AssistLKApiTestFactory();
        var owner = Guid.NewGuid();
        var id = await Seed(f, owner);
        using var client = f.CreateAuthenticatedClient(owner, UserRole.Customer);
        using var firstBody = Body();
        var first = await client.PostAsync($"/api/service-requests/{id}/attachments", firstBody);
        var photo = await first.Content.ReadFromJsonAsync<AttachmentResponse>();
        await f.SeedAsync(async db => { (await db.ServiceRequests.SingleAsync(r => r.Id == id)).Status = status; });
        using var body = Body();
        Assert.Equal(allowed ? HttpStatusCode.Created : HttpStatusCode.Conflict,
            (await client.PostAsync($"/api/service-requests/{id}/attachments", body)).StatusCode);
        Assert.Equal(allowed ? HttpStatusCode.NoContent : HttpStatusCode.Conflict,
            (await client.DeleteAsync($"/api/service-requests/{id}/attachments/{photo!.Id}")).StatusCode);
    }

    /// <summary>
    /// Purpose: Checks slot ordering (1,2,3), max 3 photos limit, metadata redaction, security headers, and read retention after cancellation.
    /// Why: Guarantees that attachment quotas, metadata privacy, and safe content delivery are enforced in an integrated flow.
    /// Scenario: Customer uploads 3 photos with path-traversal filenames, attempts a 4th photo, inspects headers, and verifies after cancellation.
    /// Pass condition: 4th photo rejected with 409 Conflict; metadata omits storageKey and contentHash; download headers have nosniff and no-store; cancelled request still serves photos.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName=AssistLK.Api.Tests.ServiceRequestAttachmentApiTests.MetadataContentCountOrderAndCancellation"
    /// </summary>
    [Fact]
    public async Task MetadataContentCountOrderAndCancellation()
    {
        await using var f = new AssistLKApiTestFactory();
        var owner = Guid.NewGuid();
        var id = await Seed(f, owner);
        using var client = f.CreateAuthenticatedClient(owner, UserRole.Customer);
        var url = $"/api/service-requests/{id}/attachments";
        for (var i = 0; i < 3; i++)
        {
            using var body = Body(name: "../../private-address.jpg");
            var response = await client.PostAsync(url, body);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var json = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("storageKey", json); Assert.DoesNotContain("contentHash", json);
            Assert.DoesNotContain("private-address", json);
        }
        using var fourth = Body();
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync(url, fourth)).StatusCode);
        var photos = await client.GetFromJsonAsync<AttachmentResponse[]>(url);
        Assert.NotNull(photos);
        Assert.Equal(new[] { 1, 2, 3 }, photos.Select(p => p.Slot));
        var content = await client.GetAsync(url + $"/{photos[0].Id}/content");
        Assert.Equal("image/jpeg", content.Content.Headers.ContentType!.MediaType);
        Assert.Equal("nosniff", content.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.True(content.Headers.CacheControl!.NoStore);
        using var decoded = new MagickImage(await content.Content.ReadAsByteArrayAsync());
        Assert.Equal(24u, decoded.Width);
        var other = await Seed(f, owner);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/service-requests/{other}/attachments/{photos[0].Id}/content")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/service-requests/{other}/attachments/{photos[0].Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/private-attachments/" + photos[0].Id + ".jpg")).StatusCode);
        await client.PostAsync($"/api/service-requests/{id}/cancel", null);
        Assert.Equal(3, (await client.GetFromJsonAsync<AttachmentResponse[]>(url))!.Length);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(url + $"/{photos[0].Id}/content")).StatusCode);
    }

    /// <summary>
    /// Purpose: Checks that non-image payloads (fake SVG, PDF, fake GIF, text pretending to be JPEG) are rejected with 400 Bad Request.
    /// Why: Prevents malicious script uploads, arbitrary binary files, and invalid formats from entering the system.
    /// Scenario: Non-image bytes uploaded under various image MIME types and extensions.
    /// Pass condition: The API responds with HTTP 400 Bad Request for all invalid files.
    /// Note: Parameterized test (Theory). The command below executes all 4 InlineData cases.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.Api.Tests.ServiceRequestAttachmentApiTests.RejectsInvalidContent"
    /// </summary>
    [Theory]
    [InlineData("bad.svg", "image/svg+xml")][InlineData("bad.pdf", "application/pdf")]
    [InlineData("bad.gif", "image/gif")][InlineData("bad.jpg", "image/jpeg")]
    public async Task RejectsInvalidContent(string name, string type)
    {
        await using var f = new AssistLKApiTestFactory(); var owner = Guid.NewGuid(); var id = await Seed(f, owner);
        using var client = f.CreateAuthenticatedClient(owner, UserRole.Customer);
        using var body = Body(Encoding.UTF8.GetBytes("not an image"), name, type);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync($"/api/service-requests/{id}/attachments", body)).StatusCode);
    }

    /// <summary>
    /// Purpose: Checks that files exceeding 5MB, empty files, or multiple files in a single request are rejected with 400 Bad Request.
    /// Why: Enforces system storage constraints and single-file payload contracts on the upload boundary.
    /// Scenario: Upload attempts with oversized payload (5MB+1 byte), empty byte array, or multiple file parts.
    /// Pass condition: HTTP 400 Bad Request and zero attachments are persisted.
    /// Note: Parameterized test (Theory). The command below executes all 3 InlineData cases.
    /// Run from repository root: dotnet test backend/AssistLK.sln --filter "FullyQualifiedName~AssistLK.Api.Tests.ServiceRequestAttachmentApiTests.MultipartBoundaryRejectsInvalidFileCountOrSize"
    /// </summary>
    [Theory]
    [InlineData("large")][InlineData("empty")][InlineData("multiple")]
    public async Task MultipartBoundaryRejectsInvalidFileCountOrSize(string scenario)
    {
        await using var f = new AssistLKApiTestFactory(); var owner = Guid.NewGuid(); var id = await Seed(f, owner);
        using var client = f.CreateAuthenticatedClient(owner, UserRole.Customer);
        using var body = Body(scenario == "large" ? new byte[5 * 1024 * 1024 + 1] : scenario == "empty" ? Array.Empty<byte>() : Photo());
        if (scenario == "multiple") body.Add(new ByteArrayContent(Photo()), "file", "second.jpg");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync($"/api/service-requests/{id}/attachments", body)).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<AttachmentResponse[]>($"/api/service-requests/{id}/attachments"))!);
    }
}


