using System.Net;
using System.Text.Json;
using VatDeclaration.Api.Services;

namespace VatDeclaration.Api.Middleware;

/// <summary>
/// Central exception handler. Ensures no stack traces, internal paths, or exception
/// details ever leak to the client — only a safe, generic message plus a correlation id
/// that can be used to find the full details in the server logs.
/// </summary>
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
        catch (Exception ex)
        {
            var correlationId = Guid.NewGuid().ToString("N");
            HttpStatusCode status;
            string publicMessage;

            switch (ex)
            {
                case CsvParsingException:
                    status = HttpStatusCode.BadRequest;
                    publicMessage = ex.Message;
                    _logger.LogWarning(ex, "Validation problem processing upload. CorrelationId={CorrelationId}", correlationId);
                    break;
                case InvalidOperationException when ex.Message.Contains("file", StringComparison.OrdinalIgnoreCase):
                    status = HttpStatusCode.BadRequest;
                    publicMessage = ex.Message;
                    _logger.LogWarning(ex, "Invalid upload. CorrelationId={CorrelationId}", correlationId);
                    break;
                default:
                    status = HttpStatusCode.InternalServerError;
                    publicMessage = "An unexpected error occurred while processing the request.";
                    _logger.LogError(ex, "Unhandled exception. CorrelationId={CorrelationId}", correlationId);
                    break;
            }

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)status;

            var payload = JsonSerializer.Serialize(new
            {
                error = publicMessage,
                correlationId
            });

            await context.Response.WriteAsync(payload);
        }
    }
}
