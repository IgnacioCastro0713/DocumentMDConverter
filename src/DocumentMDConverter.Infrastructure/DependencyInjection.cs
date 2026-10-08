using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using DocumentMDConverter.Application.Interfaces;
using DocumentMDConverter.Infrastructure.Engines;
using DocumentMDConverter.Infrastructure.Warmup;

namespace DocumentMDConverter.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // 1. Extraction and conversion engines (Engines)
        services.AddSingleton<IAnydocEngineService, AnydocEngineService>();

        // 2. Computer Vision and AI providers (OCR)
        services.AddSingleton<IGoogleVisionOcrService, GoogleVisionOcrService>();

        // 3. Google Cloud Storage Provider
        services.AddSingleton<ICloudStorageService, Storage.GoogleCloudStorageService>();

        // 4. Background Warm-up Service (Cold Start Eliminator)
        services.AddHostedService<AnydocWarmupBackgroundService>();

        return services;
    }
}
