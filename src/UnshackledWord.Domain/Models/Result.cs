namespace UnshackledWord.Domain.Models;

public class Result<T>
{
    public string? Message { get; set; }
    public T? Value { get; set; }
}
