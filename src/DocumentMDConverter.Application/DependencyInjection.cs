using Microsoft.Extensions.DependencyInjection;
using DocumentMDConverter.Application.Interfaces;
using DocumentMDConverter.Application.Services;

namespace DocumentMDConverter.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IDocumentConverterService, DocumentConverterService>();
        services.AddSingleton<IMarkdownRendererService, MarkdownRendererService>();
        return services;
    }
}
