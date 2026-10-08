using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DocumentMDConverter.Infrastructure.Warmup;

/// <summary>
/// Background worker that preheats and caches the anydoc npm binary upon application startup.
/// Eliminates cold start latency for the first user conversion.
/// </summary>
public sealed class AnydocWarmupBackgroundService(
    ILogger<AnydocWarmupBackgroundService> logger,
    IConfiguration configuration) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Yield execution to allow the ASP.NET Core web server to finish port binding first
        await Task.Yield();

        try
        {
            await Task.Delay(1500, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (stoppingToken.IsCancellationRequested) return;

        try
        {
            logger.LogInformation("Initiating anydoc engine warm-up in background...");
            Stopwatch stopwatch = Stopwatch.StartNew();

            string customBin = configuration["ANYDOC_BIN_PATH"] ??
                               Environment.GetEnvironmentVariable("ANYDOC_BIN_PATH") ?? string.Empty;

            bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            bool hasCustomBin = !string.IsNullOrEmpty(customBin) && File.Exists(customBin);

            ProcessStartInfo psi = new()
            {
                FileName = hasCustomBin ? customBin : (isWindows ? "cmd.exe" : "npx"),
                Arguments = hasCustomBin
                    ? "--version"
                    : (isWindows
                        ? "/c npx -y @firecrawl/anydoc@latest --version"
                        : "-y @firecrawl/anydoc@latest --version"),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using Process process = new();
            process.StartInfo = psi;
            process.Start();

            using CancellationTokenSource timeoutCts = new(TimeSpan.FromSeconds(30));
            using CancellationTokenSource linkedCts =
                CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, timeoutCts.Token);

            try
            {
                await process.WaitForExitAsync(linkedCts.Token);
            }
            catch (OperationCanceledException)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch
                {
                    // Ignore process cleanup errors during cancellation
                }

                return;
            }

            stopwatch.Stop();
            if (process.ExitCode == 0)
            {
                logger.LogInformation(
                    "anydoc engine warm-up completed successfully in {ElapsedMs}ms. Engine ready for instant conversions.",
                    stopwatch.ElapsedMilliseconds);
            }
            else
            {
                logger.LogWarning(
                    "anydoc engine warm-up exited with code {ExitCode}. First conversion will resolve dependencies on demand.",
                    process.ExitCode);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "anydoc engine warm-up encountered a non-fatal issue. First conversion will resolve dependencies on demand.");
        }
    }
}
