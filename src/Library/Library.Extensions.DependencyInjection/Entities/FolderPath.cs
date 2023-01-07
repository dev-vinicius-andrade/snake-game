namespace Library.Extensions.DependencyInjection.Entities;

public record FolderPath
{
    internal string Value { get; private set; }
    public static implicit operator FolderPath(string value) => new() { Value = value };
    public static implicit operator string(FolderPath value) => value.Value;
}