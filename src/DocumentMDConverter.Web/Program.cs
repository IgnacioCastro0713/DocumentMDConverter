using DocumentMDConverter.Web.Components;
using DocumentMDConverter.Web.Extensions;
using DocumentMDConverter.Application;
using DocumentMDConverter.Infrastructure;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Untracked per-developer settings (project id, bucket, dev identity); see appsettings.Example.json
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 35 * 1024 * 1024; // 35MB buffer for 30MB payload
});

// Register Clean Architecture Layers in a clean, fluid chain
builder.Services
    .AddApplication()
    .AddPresentation()
    .AddInfrastructure(builder.Configuration);

builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 35 * 1024 * 1024;
});

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddHubOptions(options =>
    {
        options.MaximumReceiveMessageSize = 35 * 1024 * 1024; // 35MB buffer for 30MB file uploads via SignalR
    });

WebApplication app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseAntiforgery();
app.MapStaticAssets();

// Dynamically map all registered endpoints
app.MapEndpoints();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
