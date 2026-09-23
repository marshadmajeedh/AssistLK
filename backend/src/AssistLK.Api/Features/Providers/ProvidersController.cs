using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using AssistLK.Application.Interfaces;
using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;
using AssistLK.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

namespace AssistLK.Api.Features.Providers;

public static class ProviderCategories
{
    public const string Plumbing = "Plumbing";
    public const string Electrical = "Electrical";
    public const string VehicleAssistance = "Vehicle Assistance";
    public const string ApplianceRepair = "Appliance Repair";
    public static readonly string[] All = { Plumbing, Electrical, VehicleAssistance, ApplianceRepair };
}

[ApiController]
[Route("api/providers")]
public class ProvidersController : ControllerBase
{
    private readonly AssistLKDbContext _dbContext;
    private readonly IServiceRequestRepository _serviceRequestRepository;
    private readonly ILogger<ProvidersController> _logger;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IWebHostEnvironment _environment;

    public ProvidersController(
        AssistLKDbContext dbContext,
        IServiceRequestRepository serviceRequestRepository,
        ILogger<ProvidersController> logger,
        IPasswordHasher<User> passwordHasher,
        IWebHostEnvironment environment)
    {
        _dbContext = dbContext;
        _serviceRequestRepository = serviceRequestRepository;
        _logger = logger;
        _passwordHasher = passwordHasher;
        _environment = environment;
    }

    /// <summary>
    /// Authenticated Provider: Get profile details including user name, business name, verification status, primary skill, and operating location.
    /// </summary>
    [HttpGet("profile")]
    [Authorize(Roles = "Provider")]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdString, out var userId)) return Unauthorized("Invalid user claim.");

        var profile = await _dbContext.ProviderProfiles
            .Include(p => p.User)
            .Include(p => p.Skills)
            .Include(p => p.Locations)
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        if (profile == null) return NotFound("Provider profile not found.");

        var location = profile.Locations.OrderByDescending(l => l.UpdatedAt).FirstOrDefault();
        var primarySkill = profile.Skills.FirstOrDefault();

        return Ok(new
        {
            providerId = profile.Id,
            fullName = profile.User?.FullName ?? string.Empty,
            email = profile.User?.Email ?? string.Empty,
            phoneNumber = profile.User?.PhoneNumber ?? string.Empty,
            businessName = profile.BusinessName,
            verificationStatus = profile.VerificationStatus.ToString(),
            rating = profile.Rating > 0 ? profile.Rating : 5.0m,
            totalCompletedJobs = profile.TotalCompletedJobs,
            isOnline = profile.IsOnline,
            category = primarySkill?.Category ?? "Plumbing",
            skillName = primarySkill?.SkillName ?? string.Empty,
            operatingRadiusKm = location?.OperatingRadiusKm ?? 10.0m,
            latitude = location?.Latitude,
            longitude = location?.Longitude,
            skills = profile.Skills.Select(s => new
            {
                s.Id,
                s.Category,
                s.SkillName,
                s.IsVerified,
                s.CertificationUrl
            })
        });
    }

    /// <summary>
    /// Authenticated Provider: Update business name, operating radius, and GPS location.
    /// </summary>
    [HttpPut("profile")]
    [Authorize(Roles = "Provider")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProviderProfileRequest request, CancellationToken cancellationToken)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdString, out var userId)) return Unauthorized("Invalid user claim.");

        var profile = await _dbContext.ProviderProfiles
            .Include(p => p.User)
            .Include(p => p.Locations)
            .Include(p => p.Skills)
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        if (profile == null) return NotFound("Provider profile not found.");

        if (!string.IsNullOrWhiteSpace(request.BusinessName))
        {
            profile.BusinessName = request.BusinessName.Trim();
        }

        profile.UpdatedAt = DateTime.UtcNow;

        var location = profile.Locations.OrderByDescending(l => l.UpdatedAt).FirstOrDefault();
        if (location == null)
        {
            location = new ProviderLocation
            {
                Id = Guid.NewGuid(),
                ProviderId = profile.Id,
                Latitude = request.Latitude ?? 6.9271m,
                Longitude = request.Longitude ?? 79.8612m,
                OperatingRadiusKm = request.OperatingRadiusKm ?? 10.0m,
                LastLocationUpdate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _dbContext.ProviderLocations.Add(location);
        }
        else
        {
            if (request.Latitude.HasValue && request.Longitude.HasValue)
            {
                location.Latitude = request.Latitude.Value;
                location.Longitude = request.Longitude.Value;
            }
            if (request.OperatingRadiusKm.HasValue)
            {
                location.OperatingRadiusKm = request.OperatingRadiusKm.Value;
            }
            location.LastLocationUpdate = DateTime.UtcNow;
            location.UpdatedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var primarySkill = profile.Skills.FirstOrDefault();

        return Ok(new
        {
            message = "Profile updated successfully.",
            providerId = profile.Id,
            fullName = profile.User?.FullName ?? string.Empty,
            businessName = profile.BusinessName,
            verificationStatus = profile.VerificationStatus.ToString(),
            rating = profile.Rating > 0 ? profile.Rating : 5.0m,
            isOnline = profile.IsOnline,
            category = primarySkill?.Category ?? "Plumbing",
            skillName = primarySkill?.SkillName ?? string.Empty,
            operatingRadiusKm = location.OperatingRadiusKm,
            latitude = location.Latitude,
            longitude = location.Longitude
        });
    }

    [HttpPost("upload-certificate")]
    public async Task<IActionResult> UploadCertificate(IFormFile file)
    {
        if (file == null || file.Length == 0) return BadRequest("No file uploaded.");
        if (file.Length > 5 * 1024 * 1024) return BadRequest("File size exceeds 5MB limit.");
        if (file.ContentType != "application/pdf") return BadRequest("Invalid file type. Only PDF is allowed.");

        using var stream = file.OpenReadStream();
        var buffer = new byte[4];
        await stream.ReadAsync(buffer, 0, 4);
        var magicBytes = System.Text.Encoding.ASCII.GetString(buffer);
        if (magicBytes != "%PDF") return BadRequest("Invalid PDF file.");

        var fileName = $"{Guid.NewGuid()}.pdf";
        var uploadsFolder = Path.Combine(_environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "uploads", "certificates");
        Directory.CreateDirectory(uploadsFolder);
        var filePath = Path.Combine(uploadsFolder, fileName);

        using var fileStream = new FileStream(filePath, FileMode.Create);
        stream.Position = 0;
        await stream.CopyToAsync(fileStream);

        return Ok(new { fileUrl = $"/uploads/certificates/{fileName}" });
    }

    [HttpGet("certificates/{fileName}")]
    [Authorize(Roles = "Admin,Provider")]
    public IActionResult GetCertificate(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Contains("..") || fileName.Contains("/") || fileName.Contains("\\"))
        {
            return BadRequest("Invalid file name.");
        }

        var uploadsFolder = Path.Combine(_environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "uploads", "certificates");
        var filePath = Path.Combine(uploadsFolder, fileName);

        if (!System.IO.File.Exists(filePath)) return NotFound();

        return PhysicalFile(filePath, "application/pdf", true);
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] ProviderRegistrationRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (await _dbContext.Users.AnyAsync(u => u.Email.ToLower() == request.Email.ToLower(), cancellationToken))
        {
            return BadRequest(new { message = "Email already in use." });
        }

        foreach (var skill in request.Skills)
        {
            if (!ProviderCategories.All.Contains(skill.Category))
            {
                return BadRequest(new { message = $"Invalid category: {skill.Category}. Must be one of {string.Join(", ", ProviderCategories.All)}" });
            }
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = request.FullName,
            Email = request.Email,
            PhoneNumber = request.PhoneNumber,
            Role = UserRole.Provider,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        var profile = new ProviderProfile
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            BusinessName = request.BusinessName,
            VerificationStatus = ProviderVerificationStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var location = new ProviderLocation
        {
            Id = Guid.NewGuid(),
            ProviderId = profile.Id,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            OperatingRadiusKm = request.OperatingRadiusKm,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var skills = request.Skills.Select(s => new ProviderSkill
        {
            Id = Guid.NewGuid(),
            ProviderId = profile.Id,
            Category = s.Category,
            SkillName = s.SkillName,
            CertificationUrl = s.CertificationUrl,
            IsVerified = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        }).ToList();

        using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        _dbContext.Users.Add(user);
        _dbContext.ProviderProfiles.Add(profile);
        _dbContext.ProviderLocations.Add(location);
        _dbContext.ProviderSkills.AddRange(skills);
        
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Ok(new { message = "Provider registered successfully." });
    }

    [HttpPut("{providerId:guid}/verify")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> VerifyProvider(Guid providerId, [FromBody] VerifyProviderRequest request, CancellationToken cancellationToken)
    {
        var profile = await _dbContext.ProviderProfiles
            .Include(p => p.Skills)
            .FirstOrDefaultAsync(p => p.Id == providerId, cancellationToken);
            
        if (profile == null) return NotFound();

        profile.VerificationStatus = request.Status;
        if (request.Status == ProviderVerificationStatus.Verified)
        {
            foreach(var skill in profile.Skills)
            {
                skill.IsVerified = true;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Ok();
    }


    [HttpGet("active-dispatch")]
    [Authorize(Roles = "Provider")]
    public async Task<IActionResult> GetActiveDispatch(CancellationToken cancellationToken)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdString, out var userId)) return Unauthorized("Invalid user claim.");

        var profile = await _dbContext.ProviderProfiles
            .Include(p => p.Skills)
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        if (profile == null) return NotFound("Provider profile not found.");

        var latestMatch = await _dbContext.MatchedCandidates
            .Include(m => m.MatchingExecution)
            .Where(m => m.ProviderId == profile.Id 
                     && m.Status == MatchedCandidateStatus.Recommended
                     && m.MatchingExecution != null
                     && m.MatchingExecution.Status == MatchingExecutionStatus.Completed)
            .OrderByDescending(m => m.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (latestMatch == null) return NoContent(); 

        ServiceRequest? serviceRequest = null;
        string? component1Review = null;
        if (latestMatch.MatchingExecution != null)
        {
            var srId = latestMatch.MatchingExecution.ServiceRequestId;
            serviceRequest = await _serviceRequestRepository.GetByIdAsync(
                srId,
                cancellationToken: cancellationToken);

            var analysis = await _dbContext.ProblemAnalyses
                .Where(p => p.ServiceRequestId == srId)
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (analysis != null && !string.IsNullOrWhiteSpace(analysis.DetectedProblem))
            {
                component1Review = analysis.DetectedProblem;
            }
            else if (!string.IsNullOrWhiteSpace(serviceRequest?.Description))
            {
                component1Review = serviceRequest.Description;
            }
        }

        var category = !string.IsNullOrWhiteSpace(serviceRequest?.Category) && serviceRequest.Category != "Unclassified"
            ? serviceRequest.Category
            : (profile.Skills.FirstOrDefault()?.Category ?? "Plumbing");

        var urgency = serviceRequest != null && serviceRequest.Urgency != ServiceRequestUrgency.Unknown
            ? serviceRequest.Urgency.ToString()
            : "High";

        return Ok(new
        {
            jobId = latestMatch.Id,
            category = category,
            distanceKm = latestMatch.DistanceKm,
            urgency = urgency,
            description = serviceRequest?.Description,
            detectedProblem = component1Review,
            aiRationale = component1Review ?? serviceRequest?.Description ?? latestMatch.MatchRationale,
            rationale = component1Review ?? serviceRequest?.Description ?? latestMatch.MatchRationale,
            matchingRationale = latestMatch.MatchRationale,
            score = latestMatch.Score,
            customerLatitude = serviceRequest?.Latitude,
            customerLongitude = serviceRequest?.Longitude
        });
    }

    [HttpPost("active-dispatch/{jobId:guid}/accept")]
    [HttpPost("active-dispatch/accept")]
    [Authorize(Roles = "Provider")]
    public async Task<IActionResult> AcceptActiveDispatch(CancellationToken cancellationToken)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdString, out var userId)) return Unauthorized("Invalid user claim.");

        var profile = await _dbContext.ProviderProfiles.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        if (profile == null) return NotFound("Provider profile not found.");

        var candidate = await _dbContext.MatchedCandidates
            .Include(m => m.MatchingExecution)
            .Where(m => m.ProviderId == profile.Id 
                     && m.Status == MatchedCandidateStatus.Recommended
                     && m.MatchingExecution != null
                     && m.MatchingExecution.Status == MatchingExecutionStatus.Completed)
            .OrderByDescending(m => m.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (candidate == null) return NotFound("No active recommended match found.");

        candidate.Status = MatchedCandidateStatus.Accepted;
        candidate.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Match accepted successfully.", candidateId = candidate.Id, status = candidate.Status.ToString() });
    }

    [HttpPost("active-dispatch/{jobId:guid}/decline")]
    [HttpPost("active-dispatch/decline")]
    [Authorize(Roles = "Provider")]
    public async Task<IActionResult> DeclineActiveDispatch(CancellationToken cancellationToken)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdString, out var userId)) return Unauthorized("Invalid user claim.");

        var profile = await _dbContext.ProviderProfiles.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        if (profile == null) return NotFound("Provider profile not found.");

        var candidate = await _dbContext.MatchedCandidates
            .Include(m => m.MatchingExecution)
            .Where(m => m.ProviderId == profile.Id 
                     && m.Status == MatchedCandidateStatus.Recommended
                     && m.MatchingExecution != null
                     && m.MatchingExecution.Status == MatchingExecutionStatus.Completed)
            .OrderByDescending(m => m.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (candidate == null) return NotFound("No active recommended match found.");

        candidate.Status = MatchedCandidateStatus.Declined;
        candidate.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Match declined successfully.", candidateId = candidate.Id, status = candidate.Status.ToString() });
    }

    [HttpPut("availability")]
    [Authorize(Roles = "Provider")]
    public async Task<IActionResult> UpdateAvailability([FromBody] UpdateAvailabilityRequest request, CancellationToken cancellationToken)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdString, out var userId)) return Unauthorized("Invalid user claim.");

        var profile = await _dbContext.ProviderProfiles
            .Include(p => p.Locations)
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        if (profile == null) return NotFound("Provider profile not found.");

        if (profile.VerificationStatus != ProviderVerificationStatus.Verified)
        {
            return StatusCode(403, "Provider account is not yet verified.");
        }

        profile.IsOnline = request.IsOnline;
        profile.UpdatedAt = DateTime.UtcNow;

        if (request.Latitude.HasValue && request.Longitude.HasValue)
        {
            var location = profile.Locations.FirstOrDefault();
            if (location == null)
            {
                location = new ProviderLocation
                {
                    Id = Guid.NewGuid(),
                    ProviderId = profile.Id,
                    Latitude = request.Latitude.Value,
                    Longitude = request.Longitude.Value,
                    OperatingRadiusKm = request.OperatingRadiusKm ?? 15.0m,
                    LastLocationUpdate = DateTime.UtcNow
                };
                _dbContext.ProviderLocations.Add(location);
            }
            else
            {
                location.Latitude = request.Latitude.Value;
                location.Longitude = request.Longitude.Value;
                if (request.OperatingRadiusKm.HasValue) location.OperatingRadiusKm = request.OperatingRadiusKm.Value;
                location.LastLocationUpdate = DateTime.UtcNow;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Availability updated successfully.", isOnline = profile.IsOnline });
    }

    [HttpGet("verification-queue")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetVerificationQueue(CancellationToken cancellationToken)
    {
        var pendingProviders = await _dbContext.ProviderProfiles
            .Include(p => p.Skills)
            .Include(p => p.User)
            .Where(p => p.VerificationStatus == ProviderVerificationStatus.Pending)
            .Select(p => new
            {
                ProviderId = p.Id,
                FullName = p.User.FullName,
                BusinessName = p.BusinessName,
                Email = p.User.Email,
                PhoneNumber = p.User.PhoneNumber,
                CreatedAt = p.CreatedAt,
                Skills = p.Skills.Select(s => new { s.SkillName, s.Category, s.CertificationUrl })
            })
            .ToListAsync(cancellationToken);
        return Ok(pendingProviders);
    }
}

public class ProviderRegistrationRequest
{
    [Required, MinLength(3), MaxLength(150), RegularExpression(@"^[a-zA-Z\s]+$")]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(8), RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[!@#$%^&*()_+\-=\[\]{};':""\\|,.<>\/?]).{8,}$")]
    public string Password { get; set; } = string.Empty;

    [Required, RegularExpression(@"^(?:\+94|0)[7][0-9]{8}$")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required, MinLength(3), MaxLength(200)]
    public string BusinessName { get; set; } = string.Empty;

    [Required, Range(-90.0, 90.0)]
    public decimal Latitude { get; set; }

    [Required, Range(-180.0, 180.0)]
    public decimal Longitude { get; set; }

    [Required, Range(1.0, 100.0)]
    public decimal OperatingRadiusKm { get; set; }

    [Required, MinLength(1)]
    public List<SkillDto> Skills { get; set; } = new();
}

public class SkillDto
{
    [Required]
    public string Category { get; set; } = string.Empty;

    [Required, MaxLength(150)]
    public string SkillName { get; set; } = string.Empty;

    public string? CertificationUrl { get; set; }
}

public class FlexibleProviderVerificationStatusConverter : JsonConverter<ProviderVerificationStatus>
{
    public override ProviderVerificationStatus Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            var intVal = reader.GetInt32();
            if (Enum.IsDefined(typeof(ProviderVerificationStatus), intVal))
            {
                return (ProviderVerificationStatus)intVal;
            }
        }
        else if (reader.TokenType == JsonTokenType.String)
        {
            var strVal = reader.GetString();
            if (!string.IsNullOrWhiteSpace(strVal))
            {
                if (int.TryParse(strVal, out var parsedInt) && Enum.IsDefined(typeof(ProviderVerificationStatus), parsedInt))
                {
                    return (ProviderVerificationStatus)parsedInt;
                }
                if (Enum.TryParse<ProviderVerificationStatus>(strVal, true, out var parsedEnum))
                {
                    return parsedEnum;
                }
            }
        }
        throw new JsonException("Unable to convert value to ProviderVerificationStatus.");
    }

    public override void Write(Utf8JsonWriter writer, ProviderVerificationStatus value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}

public class VerifyProviderRequest
{
    [JsonConverter(typeof(FlexibleProviderVerificationStatusConverter))]
    public ProviderVerificationStatus Status { get; set; }
}

public class UpdateAvailabilityRequest
{
    public bool IsOnline { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public decimal? OperatingRadiusKm { get; set; }
}

public class UpdateProviderProfileRequest
{
    [MinLength(3), MaxLength(200)]
    public string? BusinessName { get; set; }

    [Range(1.0, 100.0)]
    public decimal? OperatingRadiusKm { get; set; }

    [Range(-90.0, 90.0)]
    public decimal? Latitude { get; set; }

    [Range(-180.0, 180.0)]
    public decimal? Longitude { get; set; }
}