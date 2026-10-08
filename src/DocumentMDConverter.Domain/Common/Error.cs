namespace DocumentMDConverter.Domain.Common;

public enum ErrorType
{
    Failure = 0,
    Validation = 1,
    Problem = 2,
    NotFound = 3,
    Conflict = 4,
    Timeout = 5,
    Unprocessable = 6
}

/// <summary>
/// Discriminated Union representing errors across conversion operations.
/// Exposes static cached instances to eliminate allocations on repeated error paths.
/// </summary>
public abstract record Error(string Code, string Description, ErrorType Type = ErrorType.Failure)
{
    // Static cached singleton instances (Zero memory allocation)
    public static readonly NoneError None = new();
    public static readonly NullValueError NullValue = new();
    public static readonly NeedsOcrError NeedsOcr = new();
    public static readonly DocumentProtectedError DocumentProtected = new();
    public static readonly ResourceLimitError ResourceLimit = new();
    public static readonly MalformedError Malformed = new();
    public static readonly TimedOutError DefaultTimeout = new(60);

    // Factory methods
    public static TimedOutError TimedOut(int seconds) => seconds == 60 ? DefaultTimeout : new(seconds);
    public static FailureError Failure(string description, int exitCode = -1) => new("General.Failure", description, exitCode);
    public static FailureError Failure(string code, string description, int exitCode = -1) => new(code, description, exitCode);
    public static NotFoundError NotFound(string description = "The requested resource was not found.") => new("General.NotFound", description);
    public static NotFoundError NotFound(string code, string description) => new(code, description);
    public static ValidationError Validation(string description, IReadOnlyDictionary<string, string[]>? errors = null) => new(description, errors ?? new Dictionary<string, string[]>());
    public static CustomValidationError Validation(string code, string description) => new(code, description);

    public sealed record NoneError() : Error("General.None", string.Empty, ErrorType.Failure);

    public sealed record NullValueError() : Error("General.NullValue", "The specified result value is null.", ErrorType.Validation);

    public sealed record NeedsOcrError(string Reason = "This document contains scanned images without selectable text. Cloud Vision OCR is required.")
        : Error("Document.NeedsOcr", Reason, ErrorType.Unprocessable);

    public sealed record DocumentProtectedError(string Details = "The document is password-protected or encrypted.")
        : Error("Document.Protected", Details, ErrorType.Problem);

    public sealed record ResourceLimitError(string Details = "Document exceeded safety resource limits.")
        : Error("Document.ResourceLimit", Details, ErrorType.Problem);

    public sealed record MalformedError(string Details = "Document is structurally damaged or corrupted.")
        : Error("Document.Malformed", Details, ErrorType.Validation);

    public sealed record TimedOutError(int Seconds)
        : Error("Process.Timeout", $"Operation timed out after {Seconds} seconds.", ErrorType.Timeout);

    public sealed record ValidationError(string Description, IReadOnlyDictionary<string, string[]> Errors)
        : Error("General.Validation", Description, ErrorType.Validation);
    public sealed record CustomValidationError(string Code, string Description)
        : Error(Code, Description, ErrorType.Validation);

    public sealed record NotFoundError(string Code, string Description)
        : Error(Code, Description, ErrorType.NotFound);

    public sealed record FailureError(string Code, string Description, int ExitCode = -1)
        : Error(Code, Description, ErrorType.Failure);
}
