using Atlas.Application.Abstractions;
using Atlas.Application.Contracts;
using Atlas.Application.DTOs;
using Atlas.Domain.Audit;
using Atlas.Domain.Model.Applications;
using Atlas.Domain.Repositories;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Messaging.Consumers;

public class AccountProvisioningConsumer(
    IApplicationRepository applicationRepository,
    ICoreBankingService coreBankingService,
    ICardIssuanceService cardIssuanceService,
    IAuditRepository auditRepository,
    ICacheService cacheService,
    ILogger<AccountProvisioningConsumer> logger) : IConsumer<ApplicationApprovedEvent>
{
    private readonly IApplicationRepository _applicationRepository = applicationRepository;
    private readonly ICoreBankingService _coreBankingService = coreBankingService;
    private readonly ICardIssuanceService _cardIssuanceService = cardIssuanceService;
    private readonly IAuditRepository _auditRepository = auditRepository;
    private readonly ICacheService _cacheService = cacheService;
    private readonly ILogger<AccountProvisioningConsumer> _logger = logger;

    public async Task Consume(ConsumeContext<ApplicationApprovedEvent> context)
    {
        var message = context.Message;
        _logger.LogInformation("Received ApplicationApprovedEvent for application {ApplicationId}. Provisioning account and card...",
            message.ApplicationId);

        var application = await _applicationRepository.GetByIdAsync(message.ApplicationId, context.CancellationToken);
        if (application == null)
        {
            _logger.LogWarning("Application {ApplicationId} not found in database for account provisioning.", message.ApplicationId);
            return;
        }

        if (application.Status != ApplicationStatus.Approved)
        {
            _logger.LogWarning("Application {ApplicationId} is in status {Status}, cannot provision account.",
                application.Id, application.Status);
            return;
        }

        var account = await _coreBankingService.OpenAccountAsync(application.Id, message.Market, context.CancellationToken);
        var card = await _cardIssuanceService.OrderCardAsync(application.Id, account, context.CancellationToken);

        application.ProvisionAccountAndCard(account, card);

        await _auditRepository.AddAsync(new AuditLogEntry(
            application.Id,
            message.AttributedActor,
            AuditAction.AccountProvisioned,
            $"Core banking account {account} opened and card {card} ordered."
        ), context.CancellationToken);

        await _applicationRepository.UpdateAsync(application, context.CancellationToken);
        var response = application.ToResponse();
        await _cacheService.SetAsync($"application:{application.Id}", response, TimeSpan.FromMinutes(5), context.CancellationToken);

        _logger.LogInformation("Successfully provisioned account {AccountNumber} and card {CardReference} for application {ApplicationId}.",
            account, card, application.Id);
    }
}

