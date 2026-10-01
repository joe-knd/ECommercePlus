namespace ECommercePlus.Domain;

public enum FailureKind
{
    None,
    Validation,
    NotFound,
    Conflict
}

public class OperationResult
{
    protected OperationResult(FailureKind failure, IReadOnlyList<ValidationError> errors)
    {
        Failure = failure;
        Errors = errors;
    }

    public FailureKind Failure { get; }
    public IReadOnlyList<ValidationError> Errors { get; }
    public bool Succeeded => Failure == FailureKind.None;

    public static OperationResult Success() => new(FailureKind.None, []);
    public static OperationResult Fail(FailureKind kind, string message, string field = "") => new(kind, [new(field, message)]);
    public static OperationResult Invalid(IReadOnlyList<ValidationError> errors) => new(FailureKind.Validation, errors);
}

public sealed class OperationResult<T> : OperationResult
{
    private OperationResult(T? value, FailureKind failure, IReadOnlyList<ValidationError> errors) : base(failure, errors)
    {
        Value = value;
    }

    public T? Value { get; }

    public static OperationResult<T> Success(T value) => new(value, FailureKind.None, []);
    public new static OperationResult<T> Fail(FailureKind kind, string message, string field = "") => new(default, kind, [new(field, message)]);
    public new static OperationResult<T> Invalid(IReadOnlyList<ValidationError> errors) => new(default, FailureKind.Validation, errors);
}
