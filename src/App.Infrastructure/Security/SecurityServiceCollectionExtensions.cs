using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace App.Infrastructure.Security;

public static class SecurityServiceCollectionExtensions
{
    public const string TestingEnvironment = "Testing";

    public static IServiceCollection AddSecurity(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddOptions<AuthenticationOptions>()
            .Bind(configuration.GetSection(AuthenticationOptions.Section))
            .ValidateDataAnnotations()
            .Validate(
                o => o.Mode != AuthenticationMode.Jwt || !string.IsNullOrWhiteSpace(o.Jwt.Authority),
                "Authentication:Jwt:Authority is required in Jwt mode.")
            .Validate(
                o => o.Mode != AuthenticationMode.DevelopmentHeaders || environment.IsDevelopment() || environment.IsEnvironment(TestingEnvironment),
                "Authentication:Mode=DevelopmentHeaders is only allowed in the Development or Testing environment.")
            .ValidateOnStart();

        var options = configuration.GetSection(AuthenticationOptions.Section).Get<AuthenticationOptions>() ?? new AuthenticationOptions();

        switch (options.Mode)
        {
            case AuthenticationMode.DevelopmentHeaders:
                services.AddAuthentication(HeaderAuthenticationHandler.SchemeName)
                    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, HeaderAuthenticationHandler>(HeaderAuthenticationHandler.SchemeName, null);
                break;

            case AuthenticationMode.Jwt:
            default:
                services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                    .AddJwtBearer(jwt =>
                    {
                        jwt.Authority = options.Jwt.Authority;
                        jwt.Audience = options.Jwt.Audience;
                        jwt.RequireHttpsMetadata = options.Jwt.RequireHttpsMetadata;
                        jwt.MapInboundClaims = false;
                        jwt.TokenValidationParameters.RoleClaimType = "roles";
                        jwt.TokenValidationParameters.NameClaimType = "preferred_username";
                    });
                break;
        }

        return services;
    }
}
