using DocumentMDConverter.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace DocumentMDConverter.Web.Services;

public sealed class UserContextService(IHttpContextAccessor httpContextAccessor, IConfiguration configuration) : IUserContextService
{
    private const string IapHeaderKey = "X-Goog-Authenticated-User-Email";

    public string GetCurrentUserEmail()
    {
        HttpContext? httpContext = httpContextAccessor.HttpContext;
        if (httpContext != null && httpContext.Request.Headers.TryGetValue(IapHeaderKey, out StringValues headerValues))
        {
            string rawHeader = headerValues.ToString();
            if (!string.IsNullOrWhiteSpace(rawHeader))
            {
                return CleanEmail(rawHeader);
            }
        }

        if (httpContext?.User?.Identity?.IsAuthenticated == true && !string.IsNullOrWhiteSpace(httpContext.User.Identity.Name))
        {
            return CleanEmail(httpContext.User.Identity.Name);
        }

        // Local development fallback
        string? devUser = configuration["Iap:DefaultDevUserEmail"];
        return !string.IsNullOrWhiteSpace(devUser) ? devUser : "local.development@example.com";
    }

    private static string CleanEmail(string raw)
    {
        string email = raw.Trim();
        if (email.StartsWith("accounts.google.com:", StringComparison.OrdinalIgnoreCase))
        {
            email = email["accounts.google.com:".Length..];
        }

        return email;
    }
}
