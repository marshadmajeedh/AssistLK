using AssistLK.Application.Interfaces;
using AssistLK.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AssistLK.Application.Services;

public sealed record SubmitFeedbackCommand(
    Guid CustomerId,
    int Rating,
    string Comment,
    bool AutoEscalateToComplaint,
    string Sentiment);

public sealed record FeedbackApplicationResult(
    Guid FeedbackId,
    bool AutoEscalatedToComplaint,
    Guid? ComplaintId,
    string Sentiment,
    string Message);

public sealed class DuplicateFeedbackException : InvalidOperationException
{
    public DuplicateFeedbackException()
        : base("Feedback has already been submitted for this service job.")
    {
    }
}

public class FeedbackApplicationService
{
    private readonly IServiceJobsDbContext _serviceJobsDbContext;
    private readonly IProviderProfileDbContext _providerProfileDbContext;

    public FeedbackApplicationService(
        IServiceJobsDbContext serviceJobsDbContext,
        IProviderProfileDbContext providerProfileDbContext)
    {
        _serviceJobsDbContext = serviceJobsDbContext;
        _providerProfileDbContext = providerProfileDbContext;
    }

    public async Task<FeedbackApplicationResult> SubmitAsync(
        Guid serviceJobId,
        SubmitFeedbackCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.Rating is < 1 or > 5)
        {
            throw new ArgumentException("Rating must be between 1 and 5.");
        }

        var job = await _serviceJobsDbContext.ServiceJobs
            .FirstOrDefaultAsync(item => item.Id == serviceJobId, cancellationToken);

        if (job == null)
        {
            throw new KeyNotFoundException("Service job was not found.");
        }

        if (!job.ProviderId.HasValue)
        {
            throw new InvalidOperationException(
                "Feedback cannot be submitted until a provider is assigned to this service job.");
        }

        if (job.Status != ServiceJobStatus.Completed)
        {
            throw new InvalidOperationException(
                "Feedback can only be submitted after the service job is completed.");
        }

        var feedbackAlreadyExists = await _serviceJobsDbContext.Feedbacks
            .AnyAsync(feedback => feedback.ServiceJobId == serviceJobId, cancellationToken);

        if (feedbackAlreadyExists)
        {
            throw new DuplicateFeedbackException();
        }

        var feedback = new Feedback
        {
            ServiceJobId = serviceJobId,
            CustomerId = command.CustomerId,
            Rating = command.Rating,
            Comment = command.Comment,
            CreatedAt = DateTime.UtcNow
        };

        _serviceJobsDbContext.Feedbacks.Add(feedback);

        Guid? complaintId = null;
        if (command.AutoEscalateToComplaint)
        {
            var complaint = new Complaint
            {
                ServiceJobId = serviceJobId,
                CustomerId = command.CustomerId,
                Type = "Negative Feedback Auto-Escalation",
                CustomerComment = command.Comment,
                AiSentiment = command.Sentiment,
                Description = "Automatically escalated from customer feedback.",
                Status = "Open",
                CreatedAt = DateTime.UtcNow
            };

            _serviceJobsDbContext.Complaints.Add(complaint);
            complaintId = complaint.Id;
        }

        await _serviceJobsDbContext.SaveChangesAsync(cancellationToken);

        var provider = await _providerProfileDbContext.ProviderProfiles
            .FirstOrDefaultAsync(
                profile => profile.Id == job.ProviderId.Value,
                cancellationToken);

        if (provider == null)
        {
            throw new KeyNotFoundException(
                "Provider assigned to this service job was not found.");
        }

        var providerFeedback = _serviceJobsDbContext.Feedbacks
            .Join(
                _serviceJobsDbContext.ServiceJobs,
                feedbackItem => feedbackItem.ServiceJobId,
                serviceJob => serviceJob.Id,
                (feedbackItem, serviceJob) => new { feedbackItem, serviceJob })
            .Where(item => item.serviceJob.ProviderId == job.ProviderId.Value)
            .Select(item => item.feedbackItem.Rating);

        var totalReviews = await providerFeedback.CountAsync(cancellationToken);
        var averageRating = await providerFeedback
            .Select(rating => (decimal)rating)
            .AverageAsync(cancellationToken);

        provider.Rating = Math.Round(averageRating, 2);
        provider.TotalReviews = totalReviews;
        await _providerProfileDbContext.SaveChangesAsync(cancellationToken);

        return new FeedbackApplicationResult(
            feedback.Id,
            command.AutoEscalateToComplaint,
            complaintId,
            command.Sentiment,
            command.AutoEscalateToComplaint
                ? "Your feedback has been saved, and a support ticket has been automatically created to address your concerns."
                : "Thank you for your valuable feedback!");
    }
}