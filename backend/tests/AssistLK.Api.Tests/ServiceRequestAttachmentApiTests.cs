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

