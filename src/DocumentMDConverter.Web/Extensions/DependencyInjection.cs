using System.Reflection;

namespace DocumentMDConverter.Web.Extensions;

public static class DependencyInjection
{
    public static IServiceCollection AddPresentation(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<Application.Interfaces.IUserContextService, Services.UserContextService>();
        services.AddEndpoints(Assembly.GetExecutingAssembly());
        return services;
    }
}
