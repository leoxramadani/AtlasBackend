using Atlas.Application.Abstractions;
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

public class OnboardingService : IOnboardingService
{
    private readonly IApplicationRepository _applicationRepository;
    private readonly IIdentityVerificationProvider _idNowProvider;
    private readonly ISanctionsScreeningProvider _worldCheckProvider;
    private readonly IAuditRepository _auditRepository;
    private readonly ICoreBankingService _coreBankingService;
    private readonly ICardIssuanceService _cardIssuanceService;

    public OnboardingService(
        IApplicationRepository applicationRepository,
        IIdentityVerificationProvider idNowProvider,
        ISanctionsScreeningProvider worldCheckProvider,
        IAuditRepository auditRepository,
        ICoreBankingService coreBankingService,
        ICardIssuanceService cardIssuanceService)
    {
        _applicationRepository = applicationRepository;
        _idNowProvider = idNowProvider;
        _worldCheckProvider = worldCheckProvider;
        _auditRepository = auditRepository;
        _coreBankingService = coreBankingService;
        _cardIssuanceService = cardIssuanceService;
    }

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

        //id verification and sanctions screening
        application.StartIdentityVerification();
        var idNowRequest = new IdentityVerificationRequest(
            DocumentType: DocumentType.Passport,
            DocumentImageBase64: request.Documents?.FirstOrDefault(d => d.Type.Equals("PASSPORT", StringComparison.OrdinalIgnoreCase))?.Image,
            SelfieImageBase64: request.Documents?.FirstOrDefault(d => d.Type.Equals("SELFIE", StringComparison.OrdinalIgnoreCase))?.Image,
            OverrideOutcome: context.IdNowOverride
        );

        var idNowResult = await _idNowProvider.VerifyAsync(idNowRequest, cancellationToken);
        application.RecordIdentityVerification(idNowResult);

        await _auditRepository.AddAsync(new AuditLogEntry(
            application.Id,
            context.AttributedActor,
            AuditAction.IdentityVerificationPerformed,
            $"IDNow verification: Status={idNowResult.ResultStatus}, FaceMatch={idNowResult.FaceMatch}, Conf={idNowResult.Confidence}"
        ), cancellationToken);

        if (application.Status == ApplicationStatus.Rejected)
        {
            await _applicationRepository.UpdateAsync(application, cancellationToken);
            return ToResponse(application);
        }

        // Step 2: Sanctions & PEP screening (World-Check)
        var wcRequest = new SanctionsScreeningRequest(
            FullName: $"{request.FirstName} {request.LastName}",
            DateOfBirth: dob,
            Nationality: request.Country,
            OverrideOutcome: context.WorldCheckOverride
        );

        var wcResult = await _worldCheckProvider.ScreenAsync(wcRequest, cancellationToken);
        application.RecordSanctionsScreening(wcResult);

        await _auditRepository.AddAsync(new AuditLogEntry(
            application.Id,
            context.AttributedActor,
            AuditAction.SanctionsScreeningPerformed,
            $"World-Check screening: Status={wcResult.Status}, MatchDetails={wcResult.MatchDetails}"
        ), cancellationToken);

        //if approved, provision core banking account and order card
        if (application.Status == ApplicationStatus.Approved)
        {
            await ProvisionAccountAndCardAsync(application, context, cancellationToken);
        }

        await _applicationRepository.UpdateAsync(application, cancellationToken);
        return ToResponse(application);
    }

    private async Task ProvisionAccountAndCardAsync(
    OnboardingApplication application,
    RequestContext context,
    CancellationToken cancellationToken)
    {
        var account = await _coreBankingService.OpenAccountAsync(application.Id, application.Market.ToString(), cancellationToken);
        var card = await _cardIssuanceService.OrderCardAsync(application.Id, account, cancellationToken);

        application.ProvisionAccountAndCard(account, card);

        await _auditRepository.AddAsync(new AuditLogEntry(
            application.Id,
            context.AttributedActor,
            AuditAction.AccountProvisioned,
            $"Core banking account {account} opened and card {card} ordered."
        ), cancellationToken);
    }

    public async Task<ApplicationResponse?> GetApplicationAsync(
        Guid id,
        RequestContext context,
        CancellationToken cancellationToken = default)
    {
        var application = await _applicationRepository.GetByIdAsync(id, cancellationToken);
        if (application == null) return null;

        // Compliance Notice GC-2026-0814 #2: Record every read access individually attributable
        await _auditRepository.AddAsync(new AuditLogEntry(
            application.Id,
            context.AttributedActor,
            AuditAction.ApplicationRead,
            "Application details retrieved."
        ), cancellationToken);

        return ToResponse(application);
    }

    private static ApplicationResponse ToResponse(OnboardingApplication app)
    {
        return new ApplicationResponse(
            ApplicationId: app.Id,
            Status: app.Status.ToString().ToUpperInvariant(),
            Market: app.Market.ToString(),
            RejectionReason: app.RejectionReason,
            AccountNumber: app.AccountNumber,
            CardReference: app.CardReference,
            RequiresManualReview: app.Status == ApplicationStatus.PendingManualReview,
            RequiresBranchVisit: app.Status == ApplicationStatus.PendingBranchVisit,
            CreatedAtUtc: app.CreatedAtUtc
        );
    }

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
            await ProvisionAccountAndCardAsync(application, context, cancellationToken);
        }

        await _applicationRepository.UpdateAsync(application, cancellationToken);
        return ToResponse(application);
    }
}
