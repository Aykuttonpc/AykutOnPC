using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace AykutOnPC.Web.Infrastructure;

public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(exception, "An unhandled exception occurred: {Message}", exception.Message);

        // Determine if it's an API request or MVC request
        var isApiRequest = httpContext.Request.Path.StartsWithSegments("/api");

        if (isApiRequest)
        {
            var statusCode = exception switch
            {
                ArgumentException => (int)HttpStatusCode.BadRequest,
                UnauthorizedAccessException => (int)HttpStatusCode.Unauthorized,
                KeyNotFoundException => (int)HttpStatusCode.NotFound,
                _ => (int)HttpStatusCode.InternalServerError
            };

            httpContext.Response.StatusCode = statusCode;
            httpContext.Response.ContentType = "application/json";

            var problemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = "An error occurred while processing your request.",
                Type = $"https://httpstatuses.com/{statusCode}"
            };

            await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
            return true;
        }

        // For MVC requests, let the default error handler take over
        return false;
    }
}
