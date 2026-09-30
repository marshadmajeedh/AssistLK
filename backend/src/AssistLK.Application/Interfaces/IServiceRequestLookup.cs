using System;
using System.Threading;
using System.Threading.Tasks;
using AssistLK.Domain.Entities;

namespace AssistLK.Application.Interfaces;

/// <summary>
/// Read-only lookup interface for ServiceRequest data needed by Component 3.
///
/// Component 3 (Quotation &amp; Booking) reads the ServiceRequest at booking
/// time to capture a location snapshot (LocationText, Latitude, Longitude)
/// into the Booking entity. This decouples Component 3 from Component 1's
/// full <c>IServiceRequestRepository</c> — the agent needs only a single
/// read-only query, and this interface expresses exactly that.
///
/// No mutation of ServiceRequest is possible through this interface.
/// </summary>
public interface IServiceRequestLookup
{
    /// <summary>
    /// Read-only lookup of a ServiceRequest by its id.
    /// Returns null when the request does not exist.
    /// </summary>
    Task<ServiceRequest?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}