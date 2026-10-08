using DocumentMDConverter.Domain.Common;

namespace DocumentMDConverter.Application.Interfaces;

public interface IGoogleVisionOcrService
{
    Task<Result<string>> ExtractTextAsMarkdownAsync(
        string filePath,
        string mimeType,
        CancellationToken cancellationToken = default
    );
}
