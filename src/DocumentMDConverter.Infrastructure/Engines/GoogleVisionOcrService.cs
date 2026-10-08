using System.Text;
using DocumentMDConverter.Application.Interfaces;
using DocumentMDConverter.Domain.Common;
using Google.Cloud.Vision.V1;
using Google.Protobuf;
using Microsoft.Extensions.Logging;

namespace DocumentMDConverter.Infrastructure.Engines;

public sealed class GoogleVisionOcrService(
    ILogger<GoogleVisionOcrService> logger) : IGoogleVisionOcrService
{
    public async Task<Result<string>> ExtractTextAsMarkdownAsync(string filePath, string mimeType, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return Error.Failure($"File not found: {filePath}");
            }

            ImageAnnotatorClient client;
            try
            {
                client = await ImageAnnotatorClient.CreateAsync(cancellationToken);
            }
            catch (Exception credEx)
            {
                logger.LogWarning(credEx, "No valid GCP credentials found for Cloud Vision.");
                return Error.Failure(
                    "Failed to connect to Google Cloud Vision API: No credentials configured in current environment. Configure the GCP Service Account or run 'gcloud auth application-default login' locally.");
            }

            StringBuilder mdBuilder = new();

            if (mimeType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase) || filePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                FileInfo fileInfo = new(filePath);
                if (fileInfo.Length > 10 * 1024 * 1024)
                {
                    return Error.Failure("This scanned PDF exceeds the 10 MB limit for Google Cloud Vision OCR. Please upload a smaller scanned PDF or a document with digital text.");
                }

                byte[] fileBytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
                AnnotateFileRequest annotateRequest = new()
                {
                    InputConfig = new InputConfig
                    {
                        Content = ByteString.CopyFrom(fileBytes),
                        MimeType = "application/pdf"
                    },
                    Features = { new Feature { Type = Feature.Types.Type.DocumentTextDetection } }
                };

                BatchAnnotateFilesResponse batchResponse = await client.BatchAnnotateFilesAsync([annotateRequest], cancellationToken);
                AnnotateFileResponse? fileResponse = batchResponse.Responses.FirstOrDefault();

                if (fileResponse == null || fileResponse.Responses.Count == 0)
                {
                    return Error.Failure("Google Cloud Vision returned no text for this PDF document.");
                }

                int pageNum = 1;
                foreach (AnnotateImageResponse pageResponse in fileResponse.Responses)
                {
                    if (pageResponse.Error != null && pageResponse.Error.Code != 0)
                    {
                        logger.LogWarning("Error on page {Page}: {Error}", pageNum, pageResponse.Error.Message);
                        continue;
                    }

                    if (pageResponse.FullTextAnnotation != null && !string.IsNullOrWhiteSpace(pageResponse.FullTextAnnotation.Text))
                    {
                        mdBuilder.AppendLine($"## Page {pageNum}");
                        mdBuilder.AppendLine();
                        mdBuilder.AppendLine(ConvertFullTextToMarkdown(pageResponse.FullTextAnnotation));
                        mdBuilder.AppendLine();
                    }
                    pageNum++;
                }
            }
            else
            {
                FileInfo fileInfo = new(filePath);
                if (fileInfo.Length > 20 * 1024 * 1024)
                {
                    return Error.Failure("This image exceeds Google Cloud Vision's 20 MB limit. Please upload an image under 20 MB.");
                }

                Image image = await Image.FromFileAsync(filePath);
                TextAnnotation annotation = await client.DetectDocumentTextAsync(image);

                if (annotation != null && !string.IsNullOrWhiteSpace(annotation.Text))
                {
                    mdBuilder.AppendLine(ConvertFullTextToMarkdown(annotation));
                }
                else
                {
                    return Error.Failure("No legible text detected in image.");
                }
            }

            string resultMarkdown = mdBuilder.ToString().Trim();
            if (string.IsNullOrEmpty(resultMarkdown))
            {
                return Error.Failure("Text detection completed but found no legible content.");
            }

            return resultMarkdown; // implicit conversion to Result<string>.Success
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during Google Cloud Vision OCR for {File}", filePath);

            string msg = ex.Message;
            if (msg.Contains("page", StringComparison.OrdinalIgnoreCase) && (msg.Contains("limit", StringComparison.OrdinalIgnoreCase) || msg.Contains("exceeded", StringComparison.OrdinalIgnoreCase) || msg.Contains('5', StringComparison.OrdinalIgnoreCase)))
            {
                return Error.Failure("This scanned PDF exceeds the 5-page limit for Google Cloud Vision OCR. Please upload a scanned document with 5 or fewer pages, or a digital document.");
            }

            if (msg.Contains("10485760", StringComparison.OrdinalIgnoreCase) || msg.Contains("payload", StringComparison.OrdinalIgnoreCase))
            {
                return Error.Failure("This scanned PDF exceeds Google Cloud Vision's 10 MB payload limit for inline OCR.");
            }

            return Error.Failure($"Cloud Vision OCR could not read the document: {ex.Message}");
        }
    }

    private static string ConvertFullTextToMarkdown(TextAnnotation annotation)
    {
        if (annotation.Pages == null || annotation.Pages.Count == 0)
        {
            return annotation.Text;
        }

        StringBuilder sb = new();

        foreach (Page page in annotation.Pages)
        {
            foreach (Block block in page.Blocks)
            {
                StringBuilder blockText = new();

                foreach (Paragraph paragraph in block.Paragraphs)
                {
                    StringBuilder paraText = new();
                    foreach (Word word in paragraph.Words)
                    {
                        foreach (Symbol symbol in word.Symbols)
                        {
                            paraText.Append(symbol.Text);
                            if (symbol.Property?.DetectedBreak != null)
                            {
                                TextAnnotation.Types.DetectedBreak.Types.BreakType breakType = symbol.Property.DetectedBreak.Type;
                                switch (breakType)
                                {
                                    case TextAnnotation.Types.DetectedBreak.Types.BreakType.Space:
                                    case TextAnnotation.Types.DetectedBreak.Types.BreakType.SureSpace:
                                        paraText.Append(' ');
                                        break;
                                    case TextAnnotation.Types.DetectedBreak.Types.BreakType.LineBreak:
                                    case TextAnnotation.Types.DetectedBreak.Types.BreakType.EolSureSpace:
                                        paraText.AppendLine();
                                        break;
                                    case TextAnnotation.Types.DetectedBreak.Types.BreakType.Hyphen:
                                        paraText.Append('-');
                                        break;
                                    case TextAnnotation.Types.DetectedBreak.Types.BreakType.Unknown:
                                    default:
                                        break;
                                }
                            }
                        }
                    }
                    string trimmedPara = paraText.ToString().Trim();
                    if (!string.IsNullOrEmpty(trimmedPara))
                    {
                        blockText.AppendLine(trimmedPara);
                        blockText.AppendLine();
                    }
                }

                string trimmedBlock = blockText.ToString().Trim();
                if (!string.IsNullOrEmpty(trimmedBlock))
                {
                    sb.AppendLine(trimmedBlock);
                    sb.AppendLine();
                }
            }
        }

        return sb.Length > 0 ? sb.ToString().Trim() : annotation.Text;
    }
}
