using DocumentMDConverter.Domain.Common;
using DocumentMDConverter.Domain.Models;

namespace DocumentMDConverter.Application.Interfaces;

public interface ICloudStorageService
{
    Task<Result<string>> UploadOriginalDocumentAsync(string? userEmail, string jobId, string fileName, Stream content, string contentType, CancellationToken cancellationToken = default);
    Task<Result<string>> UploadMarkdownResultAsync(string? userEmail, string jobId, string fileName, string markdown, string engine, CancellationToken cancellationToken = default);
    Task<Result> SaveConversionMetadataAsync(ConversionHistoryItem item, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<ConversionHistoryItem>>> GetUserConversionHistoryAsync(string userEmail, CancellationToken cancellationToken = default);
    Task<Result<string>> GetMarkdownContentAsync(string markdownObjectPath, CancellationToken cancellationToken = default);
    Task<Result<string>> GenerateSignedDownloadUrlAsync(string objectPath, TimeSpan? expiry = null, CancellationToken cancellationToken = default);
    bool IsConfigured { get; }
    string? BucketName { get; }
}
