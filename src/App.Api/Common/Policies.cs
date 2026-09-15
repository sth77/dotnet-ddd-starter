namespace App.Api.Common;

/// <summary>Authorisation policy names. The policy attached to a route is the single source of truth for HAL link visibility.</summary>
public static class Policies
{
    public const string User = "User";
    public const string Admin = "Admin";
}

/// <summary>Role claim values the policies are built on.</summary>
public static class Roles
{
    public const string User = "user";
    public const string Admin = "admin";
}
