using Atlas.Application.DTOs;
using Atlas.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class ApplicationsController : ControllerBase
{
    private readonly IOnboardingService _onboardingService;

    public ApplicationsController(IOnboardingService onboardingService)
    {
        _onboardingService = onboardingService;
    }

    /// <summary>
    /// Submit a new onboarding application (supports all markets MA - MF)
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApplicationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Submit(
        [FromBody] SubmitApplicationRequest request,
        [FromHeader(Name = "X-Caller-Id")] string? callerId,
        [FromHeader(Name = "X-IdNow-Outcome")] string? idNowOutcome,
        [FromHeader(Name = "X-WorldCheck-Outcome")] string? worldCheckOutcome,
        CancellationToken cancellationToken)
    {
        var context = new RequestContext(
            AttributedActor: string.IsNullOrWhiteSpace(callerId) ? "mobile-app-client" : callerId,
            IdNowOverride: idNowOutcome,
            WorldCheckOverride: worldCheckOutcome
        );

        var result = await _onboardingService.SubmitApplicationAsync(request, context, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.ApplicationId }, result);
    }

    /// <summary>
    /// Get the current status and details of an onboarding application
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApplicationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        Guid id,
        [FromHeader(Name = "X-Caller-Id")] string? callerId,
        CancellationToken cancellationToken)
    {
        var context = new RequestContext(
            AttributedActor: string.IsNullOrWhiteSpace(callerId) ? "mobile-app-client" : callerId
        );

        var result = await _onboardingService.GetApplicationAsync(id, context, cancellationToken);
        if (result == null) return NotFound();

        return Ok(result);
    }

}
