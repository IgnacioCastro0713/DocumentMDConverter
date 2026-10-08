using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using DocumentMDConverter.Application.Interfaces;
using DocumentMDConverter.Domain.Common;

namespace DocumentMDConverter.Infrastructure.Engines;

public sealed class AnydocEngineService(
    ILogger<AnydocEngineService> logger,
    IConfiguration configuration) : IAnydocEngineService
{
    private static readonly TimeSpan DefaultExecutionTimeout = TimeSpan.FromSeconds(60);

    public async Task<Result<string>> ConvertDocumentAsync(string filePath, string? format = null, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            return Error.Failure($"File does not exist: {filePath}", -1);
        }

        string formatArg = string.IsNullOrWhiteSpace(format) ? string.Empty : $"--format {format.TrimStart('.').ToUpperInvariant()}";
        string arguments = string.IsNullOrEmpty(formatArg) ? $"\"{filePath}\"" : $"{formatArg} \"{filePath}\"";
        ProcessStartInfo psi = BuildProcessStartInfo(arguments);

        try
        {
            using Process process = new();
            process.StartInfo = psi;
            StringBuilder outputBuilder = new();
            StringBuilder errorBuilder = new();

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null) outputBuilder.AppendLine(e.Data);
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null) errorBuilder.AppendLine(e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using CancellationTokenSource timeoutCts = new(DefaultExecutionTimeout);
            using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

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
                        logger.LogWarning("anydoc process timed out or was canceled for {FilePath}. Terminating process tree...", filePath);
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch (Exception killEx)
                {
                    logger.LogWarning(killEx, "Failed to terminate process tree for {FilePath}", filePath);
                }

                if (timeoutCts.IsCancellationRequested)
                {
                    logger.LogError("anydoc conversion exceeded timeout of {TimeoutSeconds}s for {FilePath}", DefaultExecutionTimeout.TotalSeconds, filePath);
                    return Error.TimedOut((int)DefaultExecutionTimeout.TotalSeconds);
                }

                throw;
            }

            int exitCode = process.ExitCode;
            string output = outputBuilder.ToString().Trim();
            string error = errorBuilder.ToString().Trim();

            logger.LogInformation("anydoc executed for {File}. ExitCode: {ExitCode}", filePath, exitCode);

            // Exit Code 3 is the official anydoc convention for PDFs that require OCR
            if (exitCode == 3 || error.Contains("need OCR", StringComparison.OrdinalIgnoreCase) || error.Contains("NeedsOcr", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning("anydoc detected that document requires OCR (ExitCode {ExitCode})", exitCode);
                return Error.NeedsOcr;
            }

            if (exitCode == 0)
            {
                return output; // implicit conversion to Result<string>.Success(output)
            }

            if (error.Contains("Encrypted", StringComparison.OrdinalIgnoreCase))
            {
                return Error.DocumentProtected;
            }

            if (error.Contains("ResourceLimit", StringComparison.OrdinalIgnoreCase))
            {
                return Error.ResourceLimit;
            }

            if (error.Contains("Malformed", StringComparison.OrdinalIgnoreCase))
            {
                return Error.Malformed;
            }

            string userFriendlyError = error;
            if (error.Contains("MissingPart", StringComparison.OrdinalIgnoreCase))
            {
                userFriendlyError = "The document is incomplete or missing essential structural parts.";
            }
            else if (error.Contains("Unsupported", StringComparison.OrdinalIgnoreCase))
            {
                userFriendlyError = "The file format is not supported for document conversion.";
            }
            else if (string.IsNullOrWhiteSpace(error))
            {
                userFriendlyError = $"Conversion failed with exit code {exitCode}.";
            }

            return Error.Failure(userFriendlyError, exitCode);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while invoking anydoc for {FilePath}", filePath);
            return Error.Failure(ex.Message, -1);
        }
    }

    private ProcessStartInfo BuildProcessStartInfo(string arguments)
    {
        string customBin = configuration["ANYDOC_BIN_PATH"] ?? Environment.GetEnvironmentVariable("ANYDOC_BIN_PATH") ?? string.Empty;
        if (string.IsNullOrEmpty(customBin) || !File.Exists(customBin))
        {
            if (File.Exists("/usr/bin/anydoc"))
            {
                customBin = "/usr/bin/anydoc";
            }
            else if (File.Exists("/usr/local/bin/anydoc"))
            {
                customBin = "/usr/local/bin/anydoc";
            }
        }

        string fileName;
        string args;

        if (!string.IsNullOrEmpty(customBin) && File.Exists(customBin))
        {
            fileName = customBin;
            args = arguments;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            fileName = "cmd.exe";
            args = $"/c npx -y @firecrawl/anydoc@latest {arguments}";
        }
        else
        {
            fileName = "npx";
            args = $"-y @firecrawl/anydoc@latest {arguments}";
        }

        return new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
    }
}
