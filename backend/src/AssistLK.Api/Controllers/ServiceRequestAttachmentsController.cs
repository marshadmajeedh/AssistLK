using System.Security.Claims;
using AssistLK.Application.Attachments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssistLK.Api.Controllers;

[ApiController]
[Route("api/service-requests/{id:guid}/attachments")]
[Authorize(Roles = "Customer")]
public sealed class ServiceRequestAttachmentsController(ServiceRequestAttachmentService service) : ControllerBase
{
    private Guid CustomerId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id : throw new UnauthorizedAccessException("Customer identifier is missing.");

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(ServiceRequestAttachmentService.MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = ServiceRequestAttachmentService.MaxRequestBytes)]
    public async Task<ActionResult<AttachmentResponse>> Upload(Guid id, IFormFile file, CancellationToken ct)
    {
        if (Request.Form.Files.Count != 1) throw new ArgumentException("Upload exactly one photo per request.");
        if (file.Length is <= 0 or > ServiceRequestAttachmentService.MaxFileBytes)
            throw new ArgumentException("Photo size must be between 1 byte and 5 MiB (5242880 bytes).");
        await using var stream = file.OpenReadStream();
        var result = await service.UploadAsync(id, CustomerId, stream, file.FileName, file.ContentType, ct);
        return CreatedAtAction(nameof(Content), new { id, attachmentId = result.Id }, result);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AttachmentResponse>>> List(Guid id, CancellationToken ct)
        => Ok(await service.ListAsync(id, CustomerId, ct));

    [HttpGet("{attachmentId:guid}/content")]
    public async Task<IActionResult> Content(Guid id, Guid attachmentId, CancellationToken ct)
    {
        var stream = await service.OpenAsync(id, CustomerId, attachmentId, ct);
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.ContentDisposition = "inline; filename=problem-photo.jpg";
        return File(stream, "image/jpeg");
    }

    [HttpDelete("{attachmentId:guid}")]
    public async Task<IActionResult> Delete(Guid id, Guid attachmentId, CancellationToken ct)
    {
        await service.DeleteAsync(id, CustomerId, attachmentId, ct);
        return NoContent();
    }
}
