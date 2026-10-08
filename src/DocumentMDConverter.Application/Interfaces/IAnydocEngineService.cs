using DocumentMDConverter.Domain.Common;

namespace DocumentMDConverter.Application.Interfaces;

public interface IAnydocEngineService
{
    Task<Result<string>> ConvertDocumentAsync(
        string filePath,
        string? format = null,
        CancellationToken cancellationToken = default
    );
}
