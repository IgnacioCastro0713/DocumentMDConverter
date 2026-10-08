using System.Diagnostics;
using Microsoft.Extensions.Logging;
using DocumentMDConverter.Application.Interfaces;
using DocumentMDConverter.Domain.Common;
using DocumentMDConverter.Domain.Models;

namespace DocumentMDConverter.Application.Services;

public sealed class DocumentConverterService(
    IAnydocEngineService anydocEngine,
    IGoogleVisionOcrService visionOcr,
    ICloudStorageService cloudStorage,
    ILogger<DocumentConverterService> logger) : IDocumentConverterService
{
    private static readonly char[] WordSeparators = [' ', '\r', '\n', '\t'];

    public async Task<Result<ConversionResult>> ConvertAsync(
        ConversionRequest request,
        IProgress<ConversionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (Path.GetExtension(request.FileName).Equals(".epub", StringComparison.OrdinalIgnoreCase))
        {
            return Error.Validation("EPUB format is not supported.");
        }

        Stopwatch stopwatch = Stopwatch.StartNew();
        string jobId = $"conv-{Guid.NewGuid():N}";
        string tempFilePath = Path.Combine(Path.GetTempPath(), $"{jobId}_{Path.GetFileName(request.FileName)}");
        string? originalObjectPath = null;
        long originalFileSize = 0;

        try
        {
            progress?.Report(new ConversionProgress("Upload", 20, "Reading document..."));

            // 1. Buffer file to disk for CLI/SDK execution
            await using (FileStream fileStream = File.Create(tempFilePath))
            {
                await request.ContentStream.CopyToAsync(fileStream, cancellationToken);
            }

            originalFileSize = new FileInfo(tempFilePath).Length;

            // 2. Persist original document to Cloud Storage bucket if configured
            if (cloudStorage.IsConfigured)
            {
                try
                {
                    await using FileStream originalStream = File.OpenRead(tempFilePath);
                    var uploadResult = await cloudStorage.UploadOriginalDocumentAsync(
                        request.UserEmail,
                        jobId,
                        request.FileName,
                        originalStream,
                        request.ContentType,
                        cancellationToken).ConfigureAwait(false);
                    if (uploadResult.IsSuccess)
                    {
                        originalObjectPath = uploadResult.Value;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Could not upload original document {FileName} to storage bucket",
                        request.FileName);
                }
            }

            string markdown;
            string engine;

            // Direct route for standalone image files (photos, screenshots, receipts)
            if (IsImageFile(request.FileName, request.ContentType))
            {
                logger.LogInformation("Routing image file {FileName} directly to Google Cloud Vision OCR",
                    request.FileName);
                progress?.Report(new ConversionProgress("OCR", 60, "Recognizing text in image (Vision AI)..."));

                Result<string> visionResult =
                    await visionOcr.ExtractTextAsMarkdownAsync(tempFilePath, request.ContentType, cancellationToken);
                if (!visionResult.IsSuccess || visionResult.Value is null)
                {
                    return visionResult.Error ?? Error.Failure("Vision OCR failed to extract text.");
                }

                markdown = visionResult.Value;
                engine = "Google Cloud Vision OCR";
            }
            else
            {
                progress?.Report(new ConversionProgress("Extract", 55, "Extracting text and tables..."));

                // Primary Stage: anydoc (Rust Engine)
                Result<string> anydocResult =
                    await anydocEngine.ConvertDocumentAsync(tempFilePath, request.CustomFormat, cancellationToken);

                // Fallback Stage: Google Cloud Vision OCR if document requires OCR
                if (anydocResult.Error is Error.NeedsOcrError && request.EnableOcrFallback)
                {
                    logger.LogInformation("Triggering Google Cloud Vision OCR fallback for {FileName}",
                        request.FileName);
                    progress?.Report(new ConversionProgress("OCR", 75, "Recognizing scanned content..."));

                    Result<string> visionFallback =
                        await visionOcr.ExtractTextAsMarkdownAsync(tempFilePath, request.ContentType,
                            cancellationToken);
                    if (!visionFallback.IsSuccess || visionFallback.Value is null)
                    {
                        return visionFallback.Error ?? Error.Failure("Vision OCR fallback failed to extract text.");
                    }

                    markdown = visionFallback.Value;
                    engine = "Google Cloud Vision OCR";
                }
                else if (anydocResult.IsSuccess && anydocResult.Value != null)
                {
                    markdown = anydocResult.Value;
                    engine = "anydoc (Rust Engine)";
                }
                else
                {
                    return anydocResult.Error ?? Error.Failure("Document conversion failed.");
                }
            }

            stopwatch.Stop();

            return await FinalizeResultAndHistoryAsync(
                jobId,
                request.FileName,
                originalFileSize,
                request.ContentType,
                request.UserEmail,
                markdown,
                engine,
                stopwatch.ElapsedMilliseconds,
                originalObjectPath,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.LogError(ex, "Error during document conversion orchestration for {FileName}", request.FileName);
            return Error.Failure($"Internal exception: {ex.Message}");
        }
        finally
        {
            try
            {
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
            }
            catch (Exception cleanupEx)
            {
                logger.LogWarning(cleanupEx, "Failed to clean up temporary file: {TempFile}", tempFilePath);
            }
        }
    }

    private async Task<ConversionResult> FinalizeResultAndHistoryAsync(
        string jobId,
        string fileName,
        long fileSize,
        string contentType,
        string? userEmail,
        string markdown,
        string engine,
        long durationMs,
        string? originalObjectPath,
        CancellationToken cancellationToken)
    {
        string? markdownObjectPath = null;
        if (cloudStorage.IsConfigured)
        {
            try
            {
                var uploadResult = await cloudStorage.UploadMarkdownResultAsync(
                    userEmail,
                    jobId,
                    fileName,
                    markdown,
                    engine,
                    cancellationToken).ConfigureAwait(false);
                if (uploadResult.IsSuccess)
                {
                    markdownObjectPath = uploadResult.Value;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not upload markdown result for {FileName} to storage bucket", fileName);
            }
        }

        ConversionResult result = BuildSuccessResult(jobId, fileName, markdown, engine, durationMs, markdownObjectPath);

        if (cloudStorage.IsConfigured && !string.IsNullOrWhiteSpace(userEmail) &&
            !string.IsNullOrWhiteSpace(markdownObjectPath))
        {
            try
            {
                DateTimeOffset now = DateTimeOffset.UtcNow;
                ConversionHistoryItem historyItem = new(
                    JobId: jobId,
                    UserEmail: userEmail,
                    OriginalFileName: fileName,
                    OriginalFileSizeBytes: fileSize,
                    ContentType: contentType,
                    ConvertedAt: now,
                    ExpiresAt: now.AddDays(7),
                    EngineUsed: engine,
                    WordCount: result.WordCount,
                    CharacterCount: result.CharacterCount,
                    EstimatedTokens: result.EstimatedTokens,
                    DurationMs: durationMs,
                    OriginalObjectPath: originalObjectPath ?? string.Empty,
                    MarkdownObjectPath: markdownObjectPath
                );

                await cloudStorage.SaveConversionMetadataAsync(historyItem, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not save conversion history metadata for {FileName}", fileName);
            }
        }

        return result;
    }

    private static ConversionResult BuildSuccessResult(
        string jobId,
        string fileName,
        string markdown,
        string engine,
        long durationMs,
        string? storageUri = null)
    {
        int charCount = markdown.Length;
        int wordCount = markdown.Split(WordSeparators, StringSplitOptions.RemoveEmptyEntries).Length;
        int estimatedTokens = (int)Math.Ceiling(charCount / 3.8);

        return new ConversionResult(
            JobId: jobId,
            FileName: fileName,
            IsSuccess: true,
            MarkdownText: markdown,
            EngineUsed: engine,
            DurationMs: durationMs,
            WordCount: wordCount,
            CharacterCount: charCount,
            EstimatedTokens: estimatedTokens,
            ErrorMessage: null,
            StorageUri: storageUri
        );
    }

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".webp", ".tiff", ".tif", ".bmp"
    };

    private static bool IsImageFile(string fileName, string? contentType)
    {
        if (!string.IsNullOrEmpty(contentType) && contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string ext = Path.GetExtension(fileName);
        return ImageExtensions.Contains(ext);
    }
}
