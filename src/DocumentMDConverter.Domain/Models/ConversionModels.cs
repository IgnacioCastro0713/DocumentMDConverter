namespace DocumentMDConverter.Domain.Models;

public sealed record ConversionRequest(
    Stream ContentStream,
    string FileName,
    string ContentType,
    bool EnableOcrFallback = true,
    string? CustomFormat = null,
    string? UserEmail = null
);

public sealed record ConversionProgress(
    string StepName,
    int Percentage,
    string? Detail = null
);

public sealed record ConversionResult(
    string JobId,
    string FileName,
    bool IsSuccess,
    string MarkdownText,
    string EngineUsed, // "anydoc (Rust Engine)" or "Google Cloud Vision OCR"
    long DurationMs,
    int WordCount,
    int CharacterCount,
    int EstimatedTokens,
    string? ErrorMessage = null,
    string? StorageUri = ""
);

public sealed record ConversionHistoryItem(
    string JobId,
    string UserEmail,
    string OriginalFileName,
    long OriginalFileSizeBytes,
    string ContentType,
    DateTimeOffset ConvertedAt,
    DateTimeOffset ExpiresAt,
    string EngineUsed,
    int WordCount,
    int CharacterCount,
    int EstimatedTokens,
    long DurationMs,
    string OriginalObjectPath,
    string MarkdownObjectPath
)
{
    public int DaysRemaining => Math.Max(0, (int)Math.Ceiling((ExpiresAt - DateTimeOffset.UtcNow).TotalDays));
    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt;
}

public static class FormatUtils
{
    public static string FormatFileSize(long bytes) =>
        bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
            _ => $"{bytes / (1024.0 * 1024.0):F1} MB"
        };

    public static string FormatWordCount(int words)
    {
        if (words < 100_000)
        {
            return words.ToString("N0");
        }

        if (words < 1_000_000)
        {
            long k = (long)Math.Round(words / 1000.0);
            if (k < 1000)
            {
                return $"~{k}k";
            }
        }

        double m = words / 1_000_000.0;
        return m >= 10 ? $"~{Math.Round(m):0}M" : $"~{m:0.#}M";
    }

    public static string GetFormat(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return "DOC";
        string ext = Path.GetExtension(fileName).TrimStart('.').ToUpperInvariant();
        return string.IsNullOrEmpty(ext) ? "DOC" : ext;
    }

    public static string GetFormatBadgeClass(string? fileName)
    {
        string format = GetFormat(fileName).ToLowerInvariant();
        return format switch
        {
            "pdf" => "badge-format-pdf",
            "docx" or "doc" => "badge-format-docx",
            "xlsx" or "xls" or "csv" or "ods" => "badge-format-xlsx",
            "pptx" or "ppt" => "badge-format-pptx",
            "png" or "jpg" or "jpeg" or "webp" => "badge-format-png",
            "rtf" or "txt" => "badge-format-text",
            _ => "badge-format-default"
        };
    }

    public static string FormatRelativeTime(DateTimeOffset time)
    {
        TimeSpan span = DateTimeOffset.UtcNow - time;
        if (span.TotalMinutes < 1) return "Just now";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
        if (span.TotalDays < 2) return "Yesterday";
        if (span.TotalDays < 7) return $"{(int)span.TotalDays}d ago";
        return time.ToString("MMM d, yyyy");
    }
}
