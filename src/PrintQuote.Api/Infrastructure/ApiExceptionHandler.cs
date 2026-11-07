using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using PrintQuote.Application.Common;

namespace PrintQuote.Api.Infrastructure;

/// <summary>
/// Turns expected application exceptions (bad input, missing rows) into RFC 7807 responses
/// inside MVC, so they never reach the error log as "unhandled".
/// </summary>
public sealed class ApiExceptionFilter(ProblemDetailsFactory problems) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        var http = context.HttpContext;
        ProblemDetails? problem = context.Exception switch
        {
            ValidationException validation => CreateValidationProblem(http, validation),
            NotFoundException notFound => problems.CreateProblemDetails(
                http, StatusCodes.Status404NotFound, "Not found", detail: notFound.Message),
            _ => null
        };
        if (problem is null)
        {
            return;
        }

        context.Result = new ObjectResult(problem) { StatusCode = problem.Status };
        context.ExceptionHandled = true;
    }

    private ValidationProblemDetails CreateValidationProblem(HttpContext http, ValidationException validation)
    {
        var state = new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary();
        foreach (var (field, messages) in validation.Errors)
        {
            foreach (var message in messages)
            {
                state.AddModelError(field, message);
            }
        }
        return problems.CreateValidationProblemDetails(
            http, state, StatusCodes.Status400BadRequest, "Some details need fixing.");
    }
}

/// <summary>Last resort for anything else: logs it and returns a 500 problem response.</summary>
public sealed class ApiExceptionHandler(IProblemDetailsService problemDetails, ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Something went wrong on the server."
            },
            Exception = exception
        });
    }
}
