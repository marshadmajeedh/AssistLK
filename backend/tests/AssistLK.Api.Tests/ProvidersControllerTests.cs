using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AssistLK.Api.Features.Providers;
using AssistLK.Application.Interfaces;
using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;
using AssistLK.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AssistLK.Api.Tests;

public class ProvidersControllerTests : IDisposable
{
    private readonly AssistLKDbContext _dbContext;
    private readonly Mock<IServiceRequestRepository> _repoMock = new();
    private readonly Mock<ILogger<ProvidersController>> _loggerMock = new();
    private readonly PasswordHasher<User> _passwordHasher = new();
    private readonly Mock<IWebHostEnvironment> _envMock = new();
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock = new();
    private readonly string _tempWebRoot;

    public ProvidersControllerTests()
    {
        var options = new DbContextOptionsBuilder<AssistLKDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _dbContext = new AssistLKDbContext(options);

        _tempWebRoot = Path.Combine(Path.GetTempPath(), "AssistLK_Test_Uploads_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempWebRoot);
        _envMock.Setup(e => e.WebRootPath).Returns(_tempWebRoot);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        try
        {
            if (Directory.Exists(_tempWebRoot))
            {
                Directory.Delete(_tempWebRoot, true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    private ProvidersController CreateController(Guid? authenticatedUserId = null)
    {
        var controller = new ProvidersController(
            _dbContext,
            _repoMock.Object,
            _loggerMock.Object,
            _passwordHasher,
            _envMock.Object,
            _scopeFactoryMock.Object);

        if (authenticatedUserId.HasValue)
        {
            var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, authenticatedUserId.Value.ToString()),
                new Claim(ClaimTypes.Role, "Provider"),
                new Claim(ClaimTypes.Email, "provider@assistlk.com")
            }, "TestAuth"));

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };
        }

        return controller;
    }

    [Fact]
    public async Task Register_ValidMultiSkillPayload_CreatesUserAndProviderWithSkills()
    {
        // Arrange
        var controller = CreateController();
        var request = new ProviderRegistrationRequest
        {
            FullName = "Kamal Gunaratne",
            Email = "kamal.plumbing@gmail.com",
            Password = "Password@123",
            PhoneNumber = "0771234567",
            BusinessName = "Kamal Plumbing & Electric",
            Latitude = 6.9271m,
            Longitude = 79.8612m,
            OperatingRadiusKm = 12.5m,
            Skills = new List<SkillDto>
            {
                new() { Category = "Plumbing", SkillName = "Pipe Fitting", CertificationUrl = "/uploads/cert1.pdf" },
                new() { Category = "Electrical", SkillName = "House Wiring", CertificationUrl = "/uploads/cert2.pdf" }
            }
        };

        // Act
        var result = await controller.Register(request, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);

        var savedUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == "kamal.plumbing@gmail.com");
        Assert.NotNull(savedUser);
        Assert.Equal(UserRole.Provider, savedUser.Role);
        Assert.NotEmpty(savedUser.PasswordHash);

        var profile = await _dbContext.ProviderProfiles
            .Include(p => p.Skills)
            .Include(p => p.Locations)
            .FirstOrDefaultAsync(p => p.UserId == savedUser.Id);

        Assert.NotNull(profile);
        Assert.Equal("Kamal Plumbing & Electric", profile.BusinessName);
        Assert.Equal(ProviderVerificationStatus.Pending, profile.VerificationStatus);
        Assert.Equal(2, profile.Skills.Count);
        Assert.Single(profile.Locations);
        Assert.Equal(12.5m, profile.Locations.First().OperatingRadiusKm);
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsBadRequest()
    {
        // Arrange
        var controller = CreateController();
        _dbContext.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = "existing@example.com",
            FullName = "Existing User",
            PhoneNumber = "0770000000",
            Role = UserRole.Provider,
            PasswordHash = "hashed",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        var request = new ProviderRegistrationRequest
        {
            FullName = "Duplicate Plumber",
            Email = "existing@example.com",
            Password = "Password@123",
            PhoneNumber = "0771234567",
            BusinessName = "Duplicate Business",
            Latitude = 6.9m,
            Longitude = 79.8m,
            OperatingRadiusKm = 10m,
            Skills = new List<SkillDto> { new() { Category = "Plumbing", SkillName = "Plumbing" } }
        };

        // Act
        var result = await controller.Register(request, CancellationToken.None);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("already in use", badRequest.Value?.ToString() ?? "");
    }

    [Fact]
    public async Task Register_InvalidCategory_ReturnsBadRequest()
    {
        // Arrange
        var controller = CreateController();
        var request = new ProviderRegistrationRequest
        {
            FullName = "Alien Mechanic",
            Email = "alien@example.com",
            Password = "Password@123",
            PhoneNumber = "0771234567",
            BusinessName = "Alien Shop",
            Latitude = 6.9m,
            Longitude = 79.8m,
            OperatingRadiusKm = 10m,
            Skills = new List<SkillDto> { new() { Category = "AlienRepair", SkillName = "UFO Fix" } }
        };

        // Act
        var result = await controller.Register(request, CancellationToken.None);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Invalid category", badRequest.Value?.ToString() ?? "");
    }

    [Fact]
    public async Task UploadCertificate_ValidPdf_ReturnsOkWithFileUrl()
    {
        // Arrange
        var controller = CreateController();
        var pdfBytes = Encoding.ASCII.GetBytes("%PDF-1.4 Test PDF content here");
        var stream = new MemoryStream(pdfBytes);
        var fileMock = new FormFile(stream, 0, pdfBytes.Length, "file", "cert.pdf")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };

        // Act
        var result = await controller.UploadCertificate(fileMock);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
        var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        string fileUrl = doc.RootElement.GetProperty("fileUrl").GetString()!;
        Assert.StartsWith("/uploads/certificates/", fileUrl);
        Assert.EndsWith(".pdf", fileUrl);

        var fileName = Path.GetFileName(fileUrl);
        var savedFilePath = Path.Combine(_tempWebRoot, "uploads", "certificates", fileName);
        Assert.True(File.Exists(savedFilePath));
    }

    [Fact]
    public async Task UploadCertificate_DisallowedContentType_ReturnsBadRequest()
    {
        // Arrange
        var controller = CreateController();
        var bytes = Encoding.ASCII.GetBytes("echo virus");
        var stream = new MemoryStream(bytes);
        var fileMock = new FormFile(stream, 0, bytes.Length, "file", "malware.exe")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/x-msdownload"
        };

        // Act
        var result = await controller.UploadCertificate(fileMock);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Invalid file type. Only PDF is allowed.", badRequest.Value);
    }

    [Fact]
    public async Task UploadCertificate_InvalidMagicBytes_ReturnsBadRequest()
    {
        // Arrange
        var controller = CreateController();
        var bytes = Encoding.ASCII.GetBytes("THIS_IS_NOT_A_VALID_PDF_FILE");
        var stream = new MemoryStream(bytes);
        var fileMock = new FormFile(stream, 0, bytes.Length, "file", "fake.pdf")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };

        // Act
        var result = await controller.UploadCertificate(fileMock);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Invalid PDF file.", badRequest.Value);
    }

    [Fact]
    public void GetCertificate_PathTraversalAttempt_ReturnsBadRequest()
    {
        // Arrange
        var controller = CreateController();

        // Act
        var result = controller.GetCertificate("../../secret.txt");

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Invalid file name.", badRequest.Value);
    }

    [Fact]
    public async Task GetProfile_AuthenticatedProvider_ReturnsProfileDetails()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "provider1@test.com",
            FullName = "Samantha Perera",
            PhoneNumber = "0779998888",
            Role = UserRole.Provider,
            PasswordHash = "hash"
        };
        _dbContext.Users.Add(user);

        var profile = new ProviderProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            BusinessName = "Samantha Services",
            VerificationStatus = ProviderVerificationStatus.Verified,
            Rating = 4.7m,
            IsOnline = true,
            Skills = new List<ProviderSkill>
            {
                new() { Id = Guid.NewGuid(), Category = "Electrical", SkillName = "Rewiring", IsVerified = true }
            },
            Locations = new List<ProviderLocation>
            {
                new() { Id = Guid.NewGuid(), Latitude = 6.9270m, Longitude = 79.8610m, OperatingRadiusKm = 15.0m, UpdatedAt = DateTime.UtcNow }
            }
        };
        _dbContext.ProviderProfiles.Add(profile);
        await _dbContext.SaveChangesAsync();

        var controller = CreateController(authenticatedUserId: userId);

        // Act
        var result = await controller.GetProfile(CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        Assert.Equal(profile.Id.ToString(), doc.RootElement.GetProperty("providerId").GetString());
        Assert.Equal("Samantha Services", doc.RootElement.GetProperty("businessName").GetString());
        Assert.Equal("Verified", doc.RootElement.GetProperty("verificationStatus").GetString());
        Assert.Equal(4.7m, doc.RootElement.GetProperty("rating").GetDecimal());
        Assert.Equal(15.0m, doc.RootElement.GetProperty("operatingRadiusKm").GetDecimal());
        Assert.Equal("Electrical", doc.RootElement.GetProperty("category").GetString());
    }

    [Fact]
    public async Task UpdateProfile_UpdatesBusinessNameAndRadius()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "update@test.com",
            FullName = "Update User",
            PhoneNumber = "0770001111",
            Role = UserRole.Provider,
            PasswordHash = "hash"
        };
        _dbContext.Users.Add(user);

        var profile = new ProviderProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            BusinessName = "Old Business Name",
            Locations = new List<ProviderLocation>
            {
                new() { Id = Guid.NewGuid(), Latitude = 6.9m, Longitude = 79.8m, OperatingRadiusKm = 5.0m, UpdatedAt = DateTime.UtcNow }
            }
        };
        _dbContext.ProviderProfiles.Add(profile);
        await _dbContext.SaveChangesAsync();

        var controller = CreateController(authenticatedUserId: userId);
        var updateRequest = new UpdateProviderProfileRequest
        {
            BusinessName = "New Upgraded Business",
            OperatingRadiusKm = 20.0m
        };

        // Act
        var result = await controller.UpdateProfile(updateRequest, CancellationToken.None);

        // Assert
        Assert.IsType<OkObjectResult>(result);

        var updated = await _dbContext.ProviderProfiles.Include(p => p.Locations).FirstOrDefaultAsync(p => p.Id == profile.Id);
        Assert.NotNull(updated);
        Assert.Equal("New Upgraded Business", updated.BusinessName);
        Assert.Equal(20.0m, updated.Locations.First().OperatingRadiusKm);
    }
}
