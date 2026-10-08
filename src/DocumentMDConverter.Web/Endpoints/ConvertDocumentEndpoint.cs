using DocumentMDConverter.Application.Interfaces;
using DocumentMDConverter.Domain.Common;
using DocumentMDConverter.Domain.Models;
using Microsoft.AspNetCore.Mvc;
using DocumentMDConverter.Web.Infrastructure;

namespace DocumentMDConverter.Web.Endpoints;

public sealed class ConvertDocumentEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/documents/convert", async (
            IFormFile file,
            [FromForm] bool enableOcrFallback,
            [FromForm] string? format,
            IDocumentConverterService converter,
            IUserContextService userContext,
            CancellationToken cancellationToken) =>
        {
            const long maxFileSize = 30 * 1024 * 1024; // 30MB
            if (file.Length == 0)
            {
                return CustomResults.Problem(Error.Malformed);
            }

            if (Path.GetExtension(file.FileName).Equals(".epub", StringComparison.OrdinalIgnoreCase))
            {
                return CustomResults.Problem(Error.Validation("EPUB format is not supported."));
            }

            if (file.Length > maxFileSize)
            {
                return CustomResults.Problem(Error.ResourceLimit);
            }

            await using Stream stream = file.OpenReadStream();
            ConversionRequest request = new(
                ContentStream: stream,
                FileName: file.FileName,
                ContentType: file.ContentType,
                EnableOcrFallback: enableOcrFallback,
                CustomFormat: format,
                UserEmail: userContext.GetCurrentUserEmail()
            );

            Result<ConversionResult> result = await converter.ConvertAsync(request, cancellationToken: cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .DisableAntiforgery()
        .WithName("ConvertDocument")
        .WithTags("Documents");
    }
}
