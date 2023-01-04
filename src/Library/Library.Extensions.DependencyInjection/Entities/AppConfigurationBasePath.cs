namespace Library.Extensions.DependencyInjection.Entities;

public record AppConfigurationBasePath
{
    internal string Value { get; private set; }
    public static implicit operator AppConfigurationBasePath(string value) => new() { Value = value };
    public static implicit operator string(AppConfigurationBasePath value) => value.Value;
}