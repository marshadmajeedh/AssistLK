using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AssistLK.Domain.Entities;

namespace AssistLK.Application.Interfaces;

public interface IQuotationRepository
{
    Task<Quotation?> GetByIdAsync(int id, bool includeItems = false, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Quotation>> GetByServiceRequestIdAsync(
        Guid serviceRequestId,                                // ✅ Guid now
        CancellationToken cancellationToken = default);

    Task AddAsync(Quotation quotation, CancellationToken cancellationToken = default);

    void Update(Quotation quotation);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}