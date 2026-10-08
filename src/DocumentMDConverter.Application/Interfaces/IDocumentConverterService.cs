using DocumentMDConverter.Domain.Common;
using DocumentMDConverter.Domain.Models;

namespace DocumentMDConverter.Application.Interfaces;

public interface IDocumentConverterService
{
    Task<Result<ConversionResult>> ConvertAsync(
        ConversionRequest request,
        IProgress<ConversionProgress>? progress = null,
        CancellationToken cancellationToken = default
    );
}
