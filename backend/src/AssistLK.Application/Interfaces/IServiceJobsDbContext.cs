using AssistLK.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AssistLK.Application.Interfaces;

public interface IServiceJobsDbContext
{
    DbSet<ServiceJob> ServiceJobs { get; }

    DbSet<Feedback> Feedbacks { get; }

    DbSet<Complaint> Complaints { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}