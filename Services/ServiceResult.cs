namespace ScrapManagement.Web.Services;

public class ServiceResult
{
    public bool Succeeded => Errors.Count == 0;
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
    public string? FirstError => Errors.FirstOrDefault();

    public static ServiceResult Ok() => new();
    public static ServiceResult Fail(params string[] errors) => new() { Errors = errors };
    public static ServiceResult Fail(IEnumerable<string> errors) => new() { Errors = errors.ToList() };
}

public class ServiceResult<T> : ServiceResult
{
    public T? Value { get; init; }

    public static ServiceResult<T> Ok(T value) => new() { Value = value };
    public new static ServiceResult<T> Fail(params string[] errors) => new() { Errors = errors };
    public new static ServiceResult<T> Fail(IEnumerable<string> errors) => new() { Errors = errors.ToList() };
}
