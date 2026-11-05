using Atlas.Application.Abstractions;
using Atlas.Application.DTOs;
using Atlas.Application.Services;
using Atlas.Infrastructure.Persistence;
using Atlas.Infrastructure.Persistence.Repositories.Application;
using Atlas.Infrastructure.Persistence.Repositories.Audit;
using Atlas.Infrastructure.Providers;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Application.Tests;

public class OnboardingServiceTests
{
    /// <summary>
    /// In-memory cache for testing (replaces Redis).
    /// </summary>
    private class InMemoryCacheService : ICacheService
    {
        private readonly Dictionary<string, object> _store = new();

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_store.TryGetValue(key, out var v) ? (T?)v : default);
        }

        public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
        {
            _store[key] = value!;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            _store.Remove(key);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Captures published events (replaces RabbitMQ).
    /// </summary>
    private class InMemoryEventBus : IEventBus
    {
        public List<object> Published { get; } = new();

        public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class
        {
            Published.Add(message);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task SubmitApplication_HappyPath_MarketMA_ApprovesAndProvisions()
    {
        var (service, db, eventBus, cache) = CreateTestService();

        var request = new SubmitApplicationRequest(
            FirstName: "Alice",
            LastName: "Smith",
            DateOfBirth: "1992-05-10",
            Country: "MA",
            NationalId: "1234567890123",
            PassportNumber: null,
            Email: "alice@example.com",
            Phone: "+355691234567",
            Documents: new List<DocumentSubmissionDto>
            {
                new("PASSPORT", "fake-base64-doc"),
                new("SELFIE", "fake-base64-selfie")
            },
            TermsAccepted: true
        );

        var context = new RequestContext("test-runner");
        var response = await service.SubmitApplicationAsync(request, context);

        // After submit, application is in Submitted state (verification is now async)
        Assert.Equal("SUBMITTED", response.Status);
        Assert.True(eventBus.Published.Count > 0); // ApplicationSubmittedEvent published

        // Simulate async verification pipeline (IDNow + WorldCheck both pass)
        await SimulateVerificationPipelineAsync(db, cache, response.ApplicationId);

        // Fetch the updated application
        var final = await service.GetApplicationAsync(response.ApplicationId, context);

        Assert.NotNull(final);
        Assert.Equal("APPROVED", final!.Status);
        Assert.False(final.RequiresManualReview);
        Assert.False(final.RequiresBranchVisit);

        // Verify audit log has entries
        var logs = await db.AuditLogs.Where(l => l.ApplicationId == response.ApplicationId).ToListAsync();
        Assert.True(logs.Count >= 2); // Created + Read
    }

    [Fact]
    public async Task SubmitApplication_WorldCheckHit_TransitionsToPendingManualReview()
    {
        var (service, db, _, cache) = CreateTestService();

        var request = new SubmitApplicationRequest(
            FirstName: "Sanctioned",
            LastName: "Person",
            DateOfBirth: "1980-01-01",
            Country: "MB",
            NationalId: "1234567890123",
            PassportNumber: null,
            Email: "sanctioned@example.com",
            Phone: "+111222333",
            Documents: new(),
            TermsAccepted: true
        );

        var context = new RequestContext("test-runner", WorldCheckOverride: "POSSIBLE_MATCH");
        var response = await service.SubmitApplicationAsync(request, context);

        // Simulate async verification with WorldCheck override → PossibleMatch
        await SimulateVerificationPipelineAsync(db, cache, response.ApplicationId, worldCheckOverride: "POSSIBLE_MATCH");

        var afterVerification = await service.GetApplicationAsync(response.ApplicationId, context);

        Assert.NotNull(afterVerification);
        Assert.Equal("PENDINGMANUALREVIEW", afterVerification!.Status);
        Assert.True(afterVerification.RequiresManualReview);
        Assert.Null(afterVerification.AccountNumber);

        // Compliance officer manually reviews and approves
        var reviewResult = await service.ResolveManualReviewAsync(
            response.ApplicationId,
            new ManualReviewRequest(Approve: true, ReviewedBy: "officer_dane", Notes: "Identity cleared, false match."),
            context
        );

        Assert.Equal("APPROVED", reviewResult.Status);
    }

    [Fact]
    public async Task SubmitApplication_MarketMD_RequiresBranchVisitBeforeApproval()
    {
        var (service, db, _ , cache) = CreateTestService();

        var request = new SubmitApplicationRequest(
            FirstName: "Elena",
            LastName: "Rostova",
            DateOfBirth: "1995-10-15",
            Country: "MD",
            NationalId: "1234567890", // 10 digits
            PassportNumber: null,
            Email: "elena@example.com",
            Phone: "+444555666",
            Documents: new(),
            TermsAccepted: true
        );

        var context = new RequestContext("test-runner");
        var response = await service.SubmitApplicationAsync(request, context);

        // Simulate async verification (IDNow + WorldCheck pass, but MD requires branch visit)
        await SimulateVerificationPipelineAsync(db, cache, response.ApplicationId);

        var afterVerification = await service.GetApplicationAsync(response.ApplicationId, context);

        Assert.NotNull(afterVerification);
        Assert.Equal("PENDINGBRANCHVISIT", afterVerification!.Status);
        Assert.True(afterVerification.RequiresBranchVisit);
        Assert.Null(afterVerification.AccountNumber);

        // Record branch visit
        var branchResult = await service.RecordBranchVisitAsync(
            response.ApplicationId,
            new BranchVisitRequest(OfficerName: "branch_officer_eva"),
            context
        );

        Assert.Equal("APPROVED", branchResult.Status);
    }

    //helpers:

    private static (OnboardingService service, AtlasDbContext db, InMemoryEventBus eventBus, InMemoryCacheService cache) CreateTestService()
    {
        var options = new DbContextOptionsBuilder<AtlasDbContext>()
            .UseInMemoryDatabase($"TestDb_{Guid.NewGuid():N}")
            .Options;

        var db = new AtlasDbContext(options);
        db.Database.EnsureCreated();

        var appRepo = new ApplicationRepository(db);
        var auditRepo = new AuditRepository(db);
        var cache = new InMemoryCacheService();
        var eventBus = new InMemoryEventBus();

        var service = new OnboardingService(appRepo, auditRepo, cache, eventBus);
        return (service, db, eventBus, cache);
    }

    private static async Task SimulateVerificationPipelineAsync(
        AtlasDbContext db,
        ICacheService cache,
        Guid applicationId,
        string? idNowOverride = null,
        string? worldCheckOverride = null)
    {
        var idNow = new MockIdNowProvider();
        var worldCheck = new MockWorldCheckProvider();

        var application = await db.Applications
            .Include(a => a.Applicant)
            .FirstAsync(a => a.Id == applicationId);

        application.StartIdentityVerification();
        var idNowResult = await idNow.VerifyAsync(new IdentityVerificationRequest(
            DocumentType: Domain.Model.Applicants.DocumentType.Passport,
            DocumentImageBase64: null,
            SelfieImageBase64: null,
            OverrideOutcome: idNowOverride));
        application.RecordIdentityVerification(idNowResult);

        if (application.Status != Domain.Model.Applications.ApplicationStatus.Rejected)
        {
            var wcResult = await worldCheck.ScreenAsync(new SanctionsScreeningRequest(
                FullName: $"{application.Applicant.FirstName} {application.Applicant.LastName}",
                DateOfBirth: application.Applicant.DateOfBirth,
                Nationality: application.Market.ToString(),
                OverrideOutcome: worldCheckOverride));
            application.RecordSanctionsScreening(wcResult);
        }

        await db.SaveChangesAsync();

        // Same thing the real consumer must do after it updates the application
        await cache.RemoveAsync($"application:{applicationId}");
    }
}
