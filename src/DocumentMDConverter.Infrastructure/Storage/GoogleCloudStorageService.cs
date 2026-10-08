using System.Text;
using System.Text.Json;
using DocumentMDConverter.Application.Interfaces;
using DocumentMDConverter.Domain.Common;
using DocumentMDConverter.Domain.Models;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DocumentMDConverter.Infrastructure.Storage;

public sealed class GoogleCloudStorageService : ICloudStorageService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly ILogger<GoogleCloudStorageService> _logger;
    private readonly Lazy<StorageClient?> _lazyClient;
    private readonly Lazy<UrlSigner?> _lazyUrlSigner;
    private readonly string? _bucketName;

    public GoogleCloudStorageService(IConfiguration configuration, ILogger<GoogleCloudStorageService> logger)
    {
        _logger = logger;

        _bucketName = Environment.GetEnvironmentVariable("GCS_TEMP_BUCKET")
            ?? configuration["GCS_TEMP_BUCKET"]
            ?? configuration["GoogleCloud:BucketName"]
            ?? configuration["Storage:BucketName"];

        _lazyClient = new Lazy<StorageClient?>(() =>
        {
            if (string.IsNullOrWhiteSpace(_bucketName))
            {
                _logger.LogInformation("GCS_TEMP_BUCKET is not set. Cloud storage uploads are disabled.");
                return null;
            }

            try
            {
                return StorageClient.Create();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not initialize Google Cloud Storage client. Verify GCP Application Default Credentials.");
                return null;
            }
        });

        _lazyUrlSigner = new Lazy<UrlSigner?>(() =>
        {
            if (string.IsNullOrWhiteSpace(_bucketName))
            {
                return null;
            }

            try
            {
                Google.Apis.Auth.OAuth2.GoogleCredential credential = Google.Apis.Auth.OAuth2.GoogleCredential.GetApplicationDefault();
                return UrlSigner.FromCredential(credential);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not initialize Google Cloud Storage UrlSigner.");
                return null;
            }
        });
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_bucketName) && _lazyClient.Value != null;

    public string? BucketName => _bucketName;

    private static string SanitizeEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return "anonymous";
        }

        string clean = email.Trim().ToLowerInvariant();
        if (clean.StartsWith("accounts.google.com:", StringComparison.OrdinalIgnoreCase))
        {
            clean = clean["accounts.google.com:".Length..];
        }

        return clean.Replace('/', '_').Replace('\\', '_').Replace(':', '_');
    }

    public async Task<Result<string>> UploadOriginalDocumentAsync(
        string? userEmail,
        string jobId,
        string fileName,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured || _lazyClient.Value == null || string.IsNullOrWhiteSpace(_bucketName))
        {
            return Error.Failure("Storage.Unconfigured", "Cloud Storage is not configured.");
        }

        try
        {
            string cleanFileName = Path.GetFileName(fileName);
            string sanitizedEmail = SanitizeEmail(userEmail);
            string objectName = $"users/{sanitizedEmail}/{jobId}/input/{cleanFileName}";

            if (content.CanSeek)
            {
                content.Position = 0;
            }

            _logger.LogInformation("Uploading original document {FileName} to gs://{Bucket}/{ObjectName}", cleanFileName, _bucketName, objectName);

            await _lazyClient.Value.UploadObjectAsync(
                bucket: _bucketName,
                objectName: objectName,
                contentType: string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
                source: content,
                cancellationToken: cancellationToken
            ).ConfigureAwait(false);

            return objectName;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload document {FileName} to GCS bucket {Bucket}", fileName, _bucketName);
            return Error.Failure("Storage.UploadFailed", ex.Message);
        }
    }

    public async Task<Result<string>> UploadMarkdownResultAsync(
        string? userEmail,
        string jobId,
        string fileName,
        string markdown,
        string engine,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured || _lazyClient.Value == null || string.IsNullOrWhiteSpace(_bucketName))
        {
            return Error.Failure("Storage.Unconfigured", "Cloud Storage is not configured.");
        }

        try
        {
            string baseName = Path.GetFileNameWithoutExtension(fileName);
            string sanitizedEmail = SanitizeEmail(userEmail);
            string objectName = $"users/{sanitizedEmail}/{jobId}/output/{baseName}.md";

            byte[] bytes = Encoding.UTF8.GetBytes(markdown);
            using MemoryStream stream = new(bytes);

            _logger.LogInformation("Uploading converted markdown for {FileName} to gs://{Bucket}/{ObjectName}", fileName, _bucketName, objectName);

            Google.Apis.Storage.v1.Data.Object storageObject = new()
            {
                Bucket = _bucketName,
                Name = objectName,
                ContentType = "text/markdown; charset=utf-8",
                Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["original-filename"] = fileName,
                    ["engine-used"] = engine,
                    ["job-id"] = jobId,
                    ["converted-at"] = DateTimeOffset.UtcNow.ToString("O")
                }
            };

            await _lazyClient.Value.UploadObjectAsync(
                storageObject,
                stream,
                cancellationToken: cancellationToken
            ).ConfigureAwait(false);

            return objectName;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload markdown result for {FileName} to GCS bucket {Bucket}", fileName, _bucketName);
            return Error.Failure("Storage.UploadFailed", ex.Message);
        }
    }

    public async Task<Result> SaveConversionMetadataAsync(ConversionHistoryItem item, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured || _lazyClient.Value == null || string.IsNullOrWhiteSpace(_bucketName))
        {
            return Error.Failure("Storage.Unconfigured", "Cloud Storage is not configured.");
        }

        try
        {
            string sanitizedEmail = SanitizeEmail(item.UserEmail);
            string objectName = $"users/{sanitizedEmail}/{item.JobId}/metadata.json";

            byte[] jsonBytes = JsonSerializer.SerializeToUtf8Bytes(item, JsonOptions);
            using MemoryStream stream = new(jsonBytes);

            Google.Apis.Storage.v1.Data.Object storageObject = new()
            {
                Bucket = _bucketName,
                Name = objectName,
                ContentType = "application/json; charset=utf-8"
            };

            await _lazyClient.Value.UploadObjectAsync(storageObject, stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Saved conversion metadata to gs://{Bucket}/{ObjectName}", _bucketName, objectName);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save conversion metadata for Job {JobId}", item.JobId);
            return Error.Failure("Storage.SaveMetadataFailed", ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<ConversionHistoryItem>>> GetUserConversionHistoryAsync(string userEmail, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured || _lazyClient.Value == null || string.IsNullOrWhiteSpace(_bucketName))
        {
            return Error.Failure("Storage.Unconfigured", "Cloud Storage is not configured.");
        }

        try
        {
            string sanitizedEmail = SanitizeEmail(userEmail);
            string prefix = $"users/{sanitizedEmail}/";

            List<ConversionHistoryItem> results = [];
            IAsyncEnumerable<Google.Apis.Storage.v1.Data.Object> objects = _lazyClient.Value.ListObjectsAsync(_bucketName, prefix);

            await foreach (Google.Apis.Storage.v1.Data.Object obj in objects.WithCancellation(cancellationToken))
            {
                if (!obj.Name.EndsWith("/metadata.json", StringComparison.OrdinalIgnoreCase)) continue;
                try
                {
                    using MemoryStream ms = new();
                    await _lazyClient.Value.DownloadObjectAsync(_bucketName, obj.Name, ms, cancellationToken: cancellationToken).ConfigureAwait(false);
                    ms.Position = 0;
                    ConversionHistoryItem? item = await JsonSerializer.DeserializeAsync<ConversionHistoryItem>(ms, JsonOptions, cancellationToken).ConfigureAwait(false);
                    if (item != null)
                    {
                        results.Add(item);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to read conversion metadata object {Name}", obj.Name);
                }
            }

            return results
                .OrderByDescending(x => x.ConvertedAt)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch conversion history for user {UserEmail}", userEmail);
            return Error.Failure("Storage.FetchHistoryFailed", ex.Message);
        }
    }

    public async Task<Result<string>> GetMarkdownContentAsync(string markdownObjectPath, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured || _lazyClient.Value == null || string.IsNullOrWhiteSpace(_bucketName))
        {
            return Error.Failure("Storage.Unconfigured", "Cloud Storage is not configured.");
        }

        try
        {
            using MemoryStream ms = new();
            await _lazyClient.Value.DownloadObjectAsync(_bucketName, markdownObjectPath, ms, cancellationToken: cancellationToken).ConfigureAwait(false);
            return Encoding.UTF8.GetString(ms.ToArray());
        }
        catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return Error.NotFound("Storage.NotFound", $"Document not found: {markdownObjectPath}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read markdown object {Path} from bucket {Bucket}", markdownObjectPath, _bucketName);
            return Error.Failure("Storage.ReadFailed", ex.Message);
        }
    }

    public async Task<Result<string>> GenerateSignedDownloadUrlAsync(
        string objectPath,
        TimeSpan? expiry = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured || _lazyUrlSigner.Value == null || string.IsNullOrWhiteSpace(_bucketName) || string.IsNullOrWhiteSpace(objectPath))
        {
            return Error.Failure("Storage.Unconfigured", "Cloud Storage is not configured.");
        }

        try
        {
            TimeSpan duration = expiry ?? TimeSpan.FromHours(1);
            string url = await _lazyUrlSigner.Value.SignAsync(
                _bucketName,
                objectPath,
                duration,
                HttpMethod.Get,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return url;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate signed download URL for object {ObjectPath} in bucket {Bucket}", objectPath, _bucketName);
            return Error.Failure("Storage.SignUrlFailed", ex.Message);
        }
    }
}
