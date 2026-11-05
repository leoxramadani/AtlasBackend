using Atlas.Application.Abstractions;
using Atlas.Application.Contracts;
using Atlas.Application.DTOs;
using Atlas.Domain.Audit;
using Atlas.Domain.Model.Applicants;
using Atlas.Domain.Model.Applications;
using Atlas.Domain.Repositories;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Messaging.Consumers;

public class ApplicationVerificationConsumer(
    IApplicationRepository applicationRepository,
    IIdentityVerificationProvider idNowProvider,
    ISanctionsScreeningProvider worldCheckProvider,
    IAuditRepository auditRepository,
    IEventBus eventBus,
    ICacheService cacheService,
    ILogger<ApplicationVerificationConsumer> logger) : IConsumer<ApplicationSubmittedEvent>
{
    private readonly IApplicationRepository _applicationRepository = applicationRepository;
    private readonly IIdentityVerificationProvider _idNowProvider = idNowProvider;
    private readonly ISanctionsScreeningProvider _worldCheckProvider = worldCheckProvider;
    private readonly IAuditRepository _auditRepository = auditRepository;
    private readonly IEventBus _eventBus = eventBus;
    private readonly ICacheService _cacheService = cacheService;
    private readonly ILogger<ApplicationVerificationConsumer> _logger = logger;

    public async Task Consume(ConsumeContext<ApplicationSubmittedEvent> context)
    {
        var message = context.Message;
        _logger.LogInformation("Received ApplicationSubmittedEvent for application {ApplicationId} in market {Market}",
            message.ApplicationId, message.Market);

        var application = await _applicationRepository.GetByIdAsync(message.ApplicationId, context.CancellationToken);
        if (application == null)
        {
            _logger.LogWarning("Application {ApplicationId} not found in database for verification.", message.ApplicationId);
            return;
        }

        // Step 1: IDNow Identity Verification
        application.StartIdentityVerification();
        var idNowRequest = new IdentityVerificationRequest(
            DocumentType: DocumentType.Passport,
            DocumentImageBase64: message.PassportImageBase64,
            SelfieImageBase64: message.SelfieImageBase64,
            OverrideOutcome: message.IdNowOverride
        );

        var idNowResult = await _idNowProvider.VerifyAsync(idNowRequest, context.CancellationToken);
        application.RecordIdentityVerification(idNowResult);

        await _auditRepository.AddAsync(new AuditLogEntry(
            application.Id,
            message.AttributedActor,
            AuditAction.IdentityVerificationPerformed,
            $"IDNow verification: Status={idNowResult.ResultStatus}, FaceMatch={idNowResult.FaceMatch}, Conf={idNowResult.Confidence}"
        ), context.CancellationToken);

        if (application.Status == ApplicationStatus.Rejected)
        {
            await _applicationRepository.UpdateAsync(application, context.CancellationToken);
            var rejectedResponse = application.ToResponse();
            await _cacheService.SetAsync($"application:{application.Id}", rejectedResponse, TimeSpan.FromMinutes(5), context.CancellationToken);
            _logger.LogInformation("Application {ApplicationId} rejected during identity verification.", message.ApplicationId);
            return;
        }

        // Step 2: World-Check Sanctions & PEP screening
        var wcRequest = new SanctionsScreeningRequest(
            FullName: $"{application.Applicant.FirstName} {application.Applicant.LastName}",
            DateOfBirth: application.Applicant.DateOfBirth,
            Nationality: message.Market,
            OverrideOutcome: message.WorldCheckOverride
        );

        var wcResult = await _worldCheckProvider.ScreenAsync(wcRequest, context.CancellationToken);
        application.RecordSanctionsScreening(wcResult);

        await _auditRepository.AddAsync(new AuditLogEntry(
            application.Id,
            message.AttributedActor,
            AuditAction.SanctionsScreeningPerformed,
            $"World-Check screening: Status={wcResult.Status}, MatchDetails={wcResult.MatchDetails}"
        ), context.CancellationToken);

        // If approved directly, publish event for account and card provisioning
        if (application.Status == ApplicationStatus.Approved)
        {
            await _eventBus.PublishAsync(new ApplicationApprovedEvent(
                ApplicationId: application.Id,
                Market: message.Market,
                AttributedActor: message.AttributedActor
            ), context.CancellationToken);
        }

        await _applicationRepository.UpdateAsync(application, context.CancellationToken);
        var response = application.ToResponse();
        await _cacheService.SetAsync($"application:{application.Id}", response, TimeSpan.FromMinutes(5), context.CancellationToken);

        _logger.LogInformation("Application {ApplicationId} verification completed with status {Status}.",
            application.Id, application.Status);
    }
}
