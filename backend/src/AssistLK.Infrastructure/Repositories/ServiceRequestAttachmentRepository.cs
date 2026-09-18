using AssistLK.Application.Attachments;
using AssistLK.Application.Common.Exceptions;
using AssistLK.Domain.Entities;
using AssistLK.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AssistLK.Infrastructure.Repositories;

public sealed class ServiceRequestAttachmentRepository(AssistLKDbContext db) : IServiceRequestAttachmentRepository
{
    public Task<ServiceRequest?> GetOwnedAsync(Guid requestId, Guid customerId, CancellationToken ct)
        => db.ServiceRequests.Include(r => r.Attachments)
            .SingleOrDefaultAsync(r => r.Id == requestId && r.CustomerId == customerId, ct);
    public void Add(ServiceRequestAttachment attachment) => db.ServiceRequestAttachments.Add(attachment);
    public void Remove(ServiceRequestAttachment attachment) => db.ServiceRequestAttachments.Remove(attachment);
    public async Task SaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        { throw new ConflictException("Request evidence or status changed. Refresh and retry."); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { throw new ConflictException("Photo slots changed. Refresh and retry."); }
    }
    public Task<bool> IsReferencedAsync(string key, CancellationToken ct)
        => db.ServiceRequestAttachments.AsNoTracking().AnyAsync(a => a.StorageKey == key, ct);
}
