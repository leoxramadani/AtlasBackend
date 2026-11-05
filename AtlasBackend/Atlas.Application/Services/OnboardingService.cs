using Atlas.Application.Abstractions;
using Atlas.Application.Contracts;
using Atlas.Application.DTOs;
using Atlas.Domain.Audit;
using Atlas.Domain.Exceptions;
using Atlas.Domain.Model.Applicants;
using Atlas.Domain.Model.Applications;
using Atlas.Domain.Model.Markets;
using Atlas.Domain.Repositories;
using System.Security.Cryptography;
using System.Text;

namespace Atlas.Application.Services;

public class OnboardingService(
    IApplicationRepository applicationRepository,
    IAuditRepository auditRepository,
    ICacheService cacheService,
    IEventBus eventBus) : IOnboardingService
{
    private readonly IApplicationRepository _applicationRepository = applicationRepository;
    private readonly IAuditRepository _auditRepository = auditRepository;
    private readonly ICacheService _cacheService = cacheService;
    private readonly IEventBus _eventBus = eventBus;

    // Submits a new onboarding application, persists it in Submitted state, and publishes ApplicationSubmittedEvent for asynchronous processing.
    #region Submit application
    public async Task<ApplicationResponse> SubmitApplicationAsync(
        SubmitApplicationRequest request,
        RequestContext context,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<MarketCode>(request.Country, true, out var market))
        {
            throw new MarketValidationException($"Unsupported market code: '{request.Country}'. Supported markets are MA, MB, MC, MD, ME, MF.");
        }

        if (!DateOnly.TryParse(request.DateOfBirth, out var dob))
        {
            throw new DomainException($"Invalid DateOfBirth format: '{request.DateOfBirth}'. Expected YYYY-MM-DD.");
        }

        var applicant = new Applicant(
            firstName: request.FirstName,
            lastName: request.LastName,
            dateOfBirth: dob,
            nationalId: request.NationalId,
            passportNumber: request.PassportNumber,
            email: request.Email,
            phone: request.Phone,
            termsAccepted: request.TermsAccepted
        );

        var documents = new List<Document>();
        if (request.Documents != null)
        {
            foreach (var docDto in request.Documents)
            {
                var docType = docDto.Type.ToUpperInvariant() switch
                {
                    "PASSPORT" => DocumentType.Passport,
                    "ID_CARD" => DocumentType.IdCard,
                    "SELFIE" => DocumentType.Selfie,
                    _ => DocumentType.Passport
                };

                string? hash = null;
                if (!string.IsNullOrEmpty(docDto.Image))
                {
                    hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(docDto.Image)));
                }

                documents.Add(new Document(docType, hash));
            }
        }

        var application = OnboardingApplication.Create(market, applicant, documents);
        await _applicationRepository.AddAsync(application, cancellationToken);

        await _auditRepository.AddAsync(new AuditLogEntry(
            application.Id,
            context.AttributedActor,
            AuditAction.ApplicationCreated,
            $"Application created for Market {market} with {documents.Count} documents."
        ), cancellationToken);

        // Publish event to RabbitMQ for asynchronous identity verification and sanctions screening
        await _eventBus.PublishAsync(new ApplicationSubmittedEvent(
            ApplicationId: application.Id,
            Market: market.ToString(),
            PassportImageBase64: request.Documents?.FirstOrDefault(d => d.Type.Equals("PASSPORT", StringComparison.OrdinalIgnoreCase))?.Image,
            SelfieImageBase64: request.Documents?.FirstOrDefault(d => d.Type.Equals("SELFIE", StringComparison.OrdinalIgnoreCase))?.Image,
            IdNowOverride: context.IdNowOverride,
            WorldCheckOverride: context.WorldCheckOverride,
            AttributedActor: context.AttributedActor
        ), cancellationToken);

        var response = application.ToResponse();
        await _cacheService.SetAsync($"application:{application.Id}", response, TimeSpan.FromMinutes(5), cancellationToken);
        return response;
    }
    #endregion

    // Retrieves an application by its ID, returning an ApplicationResponse DTO if found, or null if not found. It also records the read access in the audit log.
    #region Get Application
    public async Task<ApplicationResponse?> GetApplicationAsync(
        Guid id,
        RequestContext context,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"application:{id}";

        var application = await _applicationRepository.GetByIdAsync(id, cancellationToken);
        if (application == null) return null;

        // Compliance Notice GC-2026-0814 #2: Record every read access individually attributable
        await _auditRepository.AddAsync(new AuditLogEntry(
            application.Id,
            context.AttributedActor,
            AuditAction.ApplicationRead,
            "Application details retrieved."
        ), cancellationToken);

        var response = application.ToResponse();
        await _cacheService.SetAsync(cacheKey, response, TimeSpan.FromMinutes(5), cancellationToken);

        return response;
    }
    #endregion

    // branch visit and manual review methods
    #region Branch Visit and Manual Review
    public async Task<ApplicationResponse> RecordBranchVisitAsync(
        Guid id,
        BranchVisitRequest request,
        RequestContext context,
        CancellationToken cancellationToken = default)
    {
        var application = await _applicationRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new DomainException($"Application {id} not found.");

        application.RecordBranchVisit(request.OfficerName, request.CompletedAtUtc ?? DateTime.UtcNow);

        await _auditRepository.AddAsync(new AuditLogEntry(
            application.Id,
            context.AttributedActor,
            AuditAction.BranchVisitRecorded,
            $"In-person branch visit verified by officer {request.OfficerName} for Market MD."
        ), cancellationToken);

        if (application.Status == ApplicationStatus.Approved)
        {
            await _eventBus.PublishAsync(new ApplicationApprovedEvent(
                ApplicationId: application.Id,
                Market: application.Market.ToString(),
                AttributedActor: context.AttributedActor
            ), cancellationToken);
        }

        await _applicationRepository.UpdateAsync(application, cancellationToken);
        var response = application.ToResponse();
        await _cacheService.SetAsync($"application:{application.Id}", response, TimeSpan.FromMinutes(5), cancellationToken);
        return response;
    }
    #endregion

    // Request ensures that the application is not in a terminal state (Approved or Rejected) before proceeding with erasure
    #region Request Erasure
    public async Task<bool> RequestErasureAsync(
        Guid id,
        RequestContext context,
        CancellationToken cancellationToken = default)
    {
        var application = await _applicationRepository.GetByIdAsync(id, cancellationToken);
        if (application == null) return false;

        application.ApplyRightToErasure();

        await _auditRepository.AddAsync(new AuditLogEntry(
            application.Id,
            context.AttributedActor,
            AuditAction.RightToErasureExercised,
            $"Customer personal data erased under GDPR Right to Erasure request."
        ), cancellationToken);

        await _cacheService.RemoveAsync($"application:{id}", cancellationToken);
        await _applicationRepository.UpdateAsync(application, cancellationToken);
        return true;
    }
    #endregion

    // Retrieves all applications that are currently pending manual review, returning them as a list of ApplicationResponse DTOs.
    #region Pending Reviews
    public async Task<IReadOnlyList<ApplicationResponse>> GetPendingReviewsAsync(
        RequestContext context,
        CancellationToken cancellationToken = default)
    {
        var list = await _applicationRepository.GetPendingManualReviewsAsync(cancellationToken);
        return [.. list.Select(a => a.ToResponse())];
    }
    #endregion

    #region Helpers
    // Resolves a manual review for a specific application, either approving or rejecting it based on the provided request. It updates the application status, records the action in the audit log, and provisions an account and card if approved.
    public async Task<ApplicationResponse> ResolveManualReviewAsync(
        Guid id,
        ManualReviewRequest request,
        RequestContext context,
        CancellationToken cancellationToken = default)
    {
        var application = await _applicationRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new DomainException($"Application {id} not found.");

        application.ResolveManualReview(request.Approve, request.ReviewedBy, request.Notes);

        await _auditRepository.AddAsync(new AuditLogEntry(
            application.Id,
            context.AttributedActor,
            AuditAction.ManualReviewProcessed,
            $"Manual review resolved by {request.ReviewedBy}. Approved={request.Approve}. Notes: {request.Notes}"
        ), cancellationToken);

        if (application.Status == ApplicationStatus.Approved)
        {
            await _eventBus.PublishAsync(new ApplicationApprovedEvent(
                ApplicationId: application.Id,
                Market: application.Market.ToString(),
                AttributedActor: context.AttributedActor
            ), cancellationToken);
        }

        await _applicationRepository.UpdateAsync(application, cancellationToken);
        var response = application.ToResponse();
        await _cacheService.SetAsync($"application:{application.Id}", response, TimeSpan.FromMinutes(5), cancellationToken);
        return response;
    }
    #endregion
}
