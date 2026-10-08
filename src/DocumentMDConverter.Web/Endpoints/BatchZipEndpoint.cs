using System.IO.Compression;
using System.Text;
using DocumentMDConverter.Application.Interfaces;
using DocumentMDConverter.Domain.Common;
using DocumentMDConverter.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DocumentMDConverter.Web.Endpoints;

public sealed record BatchZipItem(string JobId, string FileName, string? MarkdownPath = null);

public sealed record BatchZipRequest(IReadOnlyList<BatchZipItem> Items);

public sealed class BatchZipEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/batch").WithTags("Batch");

        group.MapPost("/zip", async (
                [FromBody] BatchZipRequest request,
                ICloudStorageService storage,
                IUserContextService userContext,
                CancellationToken cancellationToken) =>
            {
                if (!storage.IsConfigured)
                {
                    return CustomResults.Problem(Error.Failure("Storage.Unconfigured", "Cloud Storage is not configured on this server."));
                }

                if (request.Items.Count == 0)
                {
                    return CustomResults.Problem(Error.Validation("Batch.Empty", "No items specified for batch compression."));
                }

                var userEmail = userContext.GetCurrentUserEmail();
                using var memoryStream = new MemoryStream();

                await using var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true);

                foreach (BatchZipItem item in request.Items)
                {
                    if (string.IsNullOrWhiteSpace(item.JobId) || string.IsNullOrWhiteSpace(item.FileName))
                    {
                        continue;
                    }

                    string path = !string.IsNullOrWhiteSpace(item.MarkdownPath)
                        ? item.MarkdownPath
                        : $"users/{userEmail}/{item.JobId}/{Path.GetFileNameWithoutExtension(item.FileName)}.md";

                    Result<string> markdownResult = await storage.GetMarkdownContentAsync(path, cancellationToken);
                    if (markdownResult.IsSuccess && !string.IsNullOrEmpty(markdownResult.Value))
                    {
                        string safeBaseName = Path.GetFileNameWithoutExtension(item.FileName);
                        string entryName = $"{safeBaseName}.md";

                        ZipArchiveEntry entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
                        await using Stream entryStream = await entry.OpenAsync(cancellationToken);
                        await using var writer = new StreamWriter(entryStream, Encoding.UTF8);
                        await writer.WriteAsync(markdownResult.Value);
                    }
                }

                memoryStream.Position = 0;
                string zipFileName = $"conversions-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip";
                return Results.File(memoryStream.ToArray(), "application/zip", zipFileName);
            })
            .WithName("DownloadBatchZip");
    }
}
