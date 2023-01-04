namespace Library.Commons.Api.Entities;

public record CorsPolicyName
{
    internal string Value { get; private set; }
    public static implicit operator CorsPolicyName(string value) => new() { Value = value };
    public static implicit operator string(CorsPolicyName value) => value.Value;
}