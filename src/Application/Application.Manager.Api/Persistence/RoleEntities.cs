namespace Application.Manager.Api.Persistence;

/// <summary>A role there is. The table is made with <c>user</c> and <c>admin</c>.</summary>
public sealed class RoleRow
{
    public string Name { get; set; } = "";
}

/// <summary>A role attached to an account.</summary>
public sealed class AccountRoleRow
{
    public Guid AccountId { get; set; }

    public string Role { get; set; } = "";
}
