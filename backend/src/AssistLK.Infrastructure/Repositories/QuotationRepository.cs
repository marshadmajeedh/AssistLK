using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssistLK.Application.Interfaces;
using AssistLK.Domain.Entities;
using AssistLK.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AssistLK.Infrastructure.Repositories;

public class QuotationRepository : IQuotationRepository
{
    private readonly AssistLKDbContext _context;

    public QuotationRepository(AssistLKDbContext context)
    {
        _context = context;
    }

    public async Task<Quotation?> GetByIdAsync(
        int id, bool includeItems = false, CancellationToken cancellationToken = default)
    {
        IQueryable<Quotation> query = _context.Quotations;
        if (includeItems)
            query = query.Include(q => q.Items);

        return await query.FirstOrDefaultAsync(q => q.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Quotation>> GetByServiceRequestIdAsync(
        Guid serviceRequestId, CancellationToken cancellationToken = default)
    {
        return await _context.Quotations
            .Include(q => q.Items)
            .Where(q => q.ServiceRequestId == serviceRequestId)
            .OrderByDescending(q => q.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Quotation quotation, CancellationToken cancellationToken = default)
    {
        await _context.Quotations.AddAsync(quotation, cancellationToken);
    }

    public void Update(Quotation quotation)
    {
        var entry = _context.Entry(quotation);
        if (entry.State == EntityState.Detached)
        {
            _context.Quotations.Attach(quotation);
            entry.State = EntityState.Modified;
        }
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}