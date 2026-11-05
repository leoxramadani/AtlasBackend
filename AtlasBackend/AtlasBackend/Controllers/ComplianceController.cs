using Atlas.Application.DTOs;
using Atlas.Application.Services;
using Atlas.Domain.Audit;
using Atlas.Domain.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class ComplianceController : ControllerBase
{
    private readonly IOnboardingService _onboardingService;
    private readonly IAuditRepository _auditRepository;

    public ComplianceController(
        IOnboardingService onboardingService,
        IAuditRepository auditRepository)
    {
        _onboardingService = onboardingService;
        _auditRepository = auditRepository;
    }

    /// <summary>
    /// List all applications currently requiring manual sanctions/PEP compliance review (Compliance GC-2026-0814 #3)
    /// </summary>
    [HttpGet("reviews")]
    public async Task<IActionResult> GetPendingReviews(
        [FromHeader(Name = "X-Officer-Id")] string? officerId,
        CancellationToken cancellationToken)
    {
        var context = new RequestContext(AttributedActor: officerId ?? "compliance-officer");
        var list = await _onboardingService.GetPendingReviewsAsync(context, cancellationToken);
        return Ok(list);
    }

    /// <summary>
    /// Approve or reject an application flagged for manual compliance review
    /// </summary>
    [HttpPost("reviews/{id:guid}")]
    public async Task<IActionResult> ResolveReview(
        Guid id,
        [FromBody] ManualReviewRequest request,
        [FromHeader(Name = "X-Officer-Id")] string? officerId,
        CancellationToken cancellationToken)
    {
        var context = new RequestContext(AttributedActor: officerId ?? request.ReviewedBy);
        var result = await _onboardingService.ResolveManualReviewAsync(id, request, context, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieve the 10-year immutable audit log for an application (Compliance GC-2026-0814 #2 &amp; #4)
    /// </summary>
    [HttpGet("audit-logs/{id:guid}")]
    public async Task<IActionResult> GetAuditLogs(Guid id, CancellationToken cancellationToken)
    {
        var logs = await _auditRepository.GetByApplicationIdAsync(id, cancellationToken);
        return Ok(logs.Select(l => new
        {
            l.Id,
            l.ApplicationId,
            l.PerformedBy,
            Action = l.Action.ToString(),
            l.Details,
            l.CreatedAtUtc,
            l.RetainUntilUtc
        }));
    }
}
