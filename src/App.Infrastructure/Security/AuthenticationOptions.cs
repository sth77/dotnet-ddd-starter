using System.ComponentModel.DataAnnotations;

namespace App.Infrastructure.Security;

/// <summary>
/// Bound from <c>Authentication</c>, validated on start (design §8 layer 4, §2 "Spring profiles").
/// <list type="bullet">
/// <item><c>Jwt</c>: bearer tokens from an OpenID Connect authority — the production mode.</item>
/// <item><c>DevelopmentHeaders</c>: identity from <c>X-User</c>/<c>X-Roles</c> request headers. Local runs and tests only;
/// refused outside the Development and Testing environments.</item>
/// </list>
/// </summary>
public sealed class AuthenticationOptions
{
    public const string Section = "Authentication";

    [Required]
    public AuthenticationMode Mode { get; init; } = AuthenticationMode.Jwt;

    public JwtOptions Jwt { get; init; } = new();

    public sealed class JwtOptions
    {
        [Url]
        public string? Authority { get; init; }

        public string? Audience { get; init; }

        public bool RequireHttpsMetadata { get; init; } = true;
    }
}

public enum AuthenticationMode
{
    Jwt,
    DevelopmentHeaders,
}
