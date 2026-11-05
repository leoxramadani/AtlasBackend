using Atlas.Application.DTOs;
using Atlas.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class BranchController : ControllerBase
{
    private readonly IOnboardingService _onboardingService;

    public BranchController(IOnboardingService onboardingService)
    {
        _onboardingService = onboardingService;
    }

    /// <summary>
    /// Record in-person customer branch attendance and wet signature (Annex B requirement for Market MD)
    /// </summary>
    [HttpPost("applications/{id:guid}/visit")]
    public async Task<IActionResult> RecordVisit(
        Guid id,
        [FromBody] BranchVisitRequest request,
        [FromHeader(Name = "X-Officer-Id")] string? officerId,
        CancellationToken cancellationToken)
    {
        var context = new RequestContext(AttributedActor: officerId ?? request.OfficerName);
        var result = await _onboardingService.RecordBranchVisitAsync(id, request, context, cancellationToken);
        return Ok(result);
    }
}
