using System.Net;
using System.Text.Json;
using Atlas.Domain.Exceptions;

namespace Atlas.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (MarketValidationException ex)
        {
            _logger.LogWarning(ex, "Market validation failure: {Message}", ex.Message);
            await WriteProblemResponse(context, HttpStatusCode.BadRequest, "Market Validation Error", ex.Message);
        }
        catch (InvalidApplicationStateException ex)
        {
            _logger.LogWarning(ex, "Invalid state transition: {Message}", ex.Message);
            await WriteProblemResponse(context, HttpStatusCode.Conflict, "Invalid State Transition", ex.Message);
        }
        catch (DomainException ex)
        {
            _logger.LogWarning(ex, "Domain exception: {Message}", ex.Message);
            await WriteProblemResponse(context, HttpStatusCode.BadRequest, "Domain Error", ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled server exception: {Message}", ex.Message);
            await WriteProblemResponse(context, HttpStatusCode.InternalServerError, "Server Error", "An unexpected error occurred processing your request.");
        }
    }

    private static async Task WriteProblemResponse(HttpContext context, HttpStatusCode statusCode, string title, string detail)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var response = new
        {
            Title = title,
            Status = (int)statusCode,
            Detail = detail,
            Timestamp = DateTime.UtcNow
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}
