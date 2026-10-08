using DocumentMDConverter.Application.Interfaces;
using DocumentMDConverter.Domain.Common;
using DocumentMDConverter.Domain.Models;
using DocumentMDConverter.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DocumentMDConverter.Web.Endpoints;

public sealed class ConversionHistoryEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/history").WithTags("History");

        group.MapGet("/", async (
            ICloudStorageService storage,
            IUserContextService userContext,
            CancellationToken cancellationToken) =>
        {
            if (!storage.IsConfigured)
            {
                return Results.Ok(Array.Empty<ConversionHistoryItem>());
            }

            string userEmail = userContext.GetCurrentUserEmail();
            Result<IReadOnlyList<ConversionHistoryItem>> historyResult = await storage.GetUserConversionHistoryAsync(userEmail, cancellationToken);
            return historyResult.Match(Results.Ok, CustomResults.Problem);
        })
        .WithName("GetUserHistory");

        // Direct signed URL redirect: No streaming through server memory or bandwidth
        group.MapGet("/download", async (
            [FromQuery] string path,
            ICloudStorageService storage,
            CancellationToken cancellationToken) =>
        {
            if (!storage.IsConfigured || string.IsNullOrWhiteSpace(path))
            {
                return CustomResults.Problem(Error.NotFound("Storage.NotFound", "File not found or storage not configured."));
            }

            Result<string> signedUrlResult = await storage.GenerateSignedDownloadUrlAsync(path, TimeSpan.FromHours(1), cancellationToken);

            return signedUrlResult.Match(url => Results.Redirect(url), CustomResults.Problem);
        })
        .WithName("DownloadHistoricalFileViaSignedUrl");

        group.MapGet("/markdown-content", async (
            [FromQuery] string path,
            ICloudStorageService storage,
            CancellationToken cancellationToken) =>
        {
            if (!storage.IsConfigured || string.IsNullOrWhiteSpace(path))
            {
                return CustomResults.Problem(Error.NotFound("Storage.NotFound", "Document not found or storage not configured."));
            }

            Result<string> markdownResult = await storage.GetMarkdownContentAsync(path, cancellationToken);
            return markdownResult.Match(markdown => Results.Ok(new { markdown }), CustomResults.Problem);
        })
        .WithName("GetHistoricalMarkdownContent");
    }
}
