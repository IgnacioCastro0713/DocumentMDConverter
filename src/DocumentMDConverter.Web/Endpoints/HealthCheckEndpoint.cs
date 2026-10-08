namespace DocumentMDConverter.Web.Endpoints;

public sealed class HealthCheckEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/health", () => Results.Ok(new
        {
            status = "healthy",
            service = "DocumentMDConverter",
            version = "1.0.0",
            timestamp = DateTime.UtcNow
        }))
        .WithName("HealthCheck")
        .WithTags("System");
    }
}
