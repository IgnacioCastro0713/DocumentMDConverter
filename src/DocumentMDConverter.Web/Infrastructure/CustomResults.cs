using DocumentMDConverter.Domain.Common;
using Microsoft.AspNetCore.Http;

namespace DocumentMDConverter.Web.Infrastructure;

public static class CustomResults
{
    public static IResult Problem(Error error)
    {
        return Results.Problem(
            title: GetTitle(error),
            detail: GetDetail(error),
            type: GetType(error.Type),
            statusCode: GetStatusCode(error.Type),
            extensions: GetErrors(error));

        static string GetTitle(Error error) =>
            error.Type switch
            {
                ErrorType.Validation => error.Code,
                ErrorType.Problem => error.Code,
                ErrorType.NotFound => error.Code,
                ErrorType.Conflict => error.Code,
                ErrorType.Timeout => error.Code,
                ErrorType.Unprocessable => error.Code,
                _ => "Server failure"
            };

        static string GetDetail(Error error) =>
            error.Type switch
            {
                ErrorType.Validation => error.Description,
                ErrorType.Problem => error.Description,
                ErrorType.NotFound => error.Description,
                ErrorType.Conflict => error.Description,
                ErrorType.Timeout => error.Description,
                ErrorType.Unprocessable => error.Description,
                _ => "An unexpected error occurred"
            };

        static string GetType(ErrorType errorType) =>
            errorType switch
            {
                ErrorType.Validation => "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                ErrorType.Problem => "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                ErrorType.NotFound => "https://tools.ietf.org/html/rfc7231#section-6.5.4",
                ErrorType.Conflict => "https://tools.ietf.org/html/rfc7231#section-6.5.8",
                ErrorType.Timeout => "https://tools.ietf.org/html/rfc7231#section-6.6.5",
                ErrorType.Unprocessable => "https://tools.ietf.org/html/rfc4918#section-11.2",
                _ => "https://tools.ietf.org/html/rfc7231#section-6.6.1"
            };

        static int GetStatusCode(ErrorType errorType) =>
            errorType switch
            {
                ErrorType.Validation or ErrorType.Problem => StatusCodes.Status400BadRequest,
                ErrorType.NotFound => StatusCodes.Status404NotFound,
                ErrorType.Conflict => StatusCodes.Status409Conflict,
                ErrorType.Timeout => StatusCodes.Status504GatewayTimeout,
                ErrorType.Unprocessable => StatusCodes.Status422UnprocessableEntity,
                _ => StatusCodes.Status500InternalServerError
            };

        static Dictionary<string, object?>? GetErrors(Error error)
        {
            if (error is not Error.ValidationError validationError)
            {
                return null;
            }

            return new Dictionary<string, object?>
            {
                { "errors", validationError.Errors }
            };
        }
    }
}
