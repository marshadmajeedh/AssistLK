using System;
using System.Collections.Generic;

namespace AssistLK.Domain.Entities;

public enum ServiceJobStatus
{
    Assigned = 1,
    OnTheWay = 2,
    Arrived = 3,
    InProgress = 4,
    Completed = 5,
    Cancelled = 6
}

public class ServiceJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ServiceRequestId { get; set; }
    public Guid? BookingId { get; set; }
    public ServiceJobStatus Status { get; set; } = ServiceJobStatus.Assigned;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ServiceStatusHistory> StatusHistories { get; set; } = new List<ServiceStatusHistory>();
    public CompletionRecord? CompletionRecord { get; set; }
    public Feedback? Feedback { get; set; }
    public ICollection<Complaint> Complaints { get; set; } = new List<Complaint>();
}

public class ServiceStatusHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ServiceJobId { get; set; }
    public ServiceJobStatus? OldStatus { get; set; }
    public ServiceJobStatus NewStatus { get; set; }
    public Guid? ChangedByUserId { get; set; }
    public string? Note { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}

public class CompletionRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ServiceJobId { get; set; }
    public string WorkSummary { get; set; } = string.Empty;
    public string? ProofOfWorkImageUrl { get; set; }
    public decimal AdditionalCost { get; set; }
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
}

public class Feedback
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ServiceJobId { get; set; }
    public Guid CustomerId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Complaint
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ServiceJobId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid ComplainantId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "Open";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}