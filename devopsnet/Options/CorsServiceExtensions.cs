using Microsoft.Extensions.Options;

namespace devopsnet.Options;

public static class CorsServiceExtensions
{
    public const string PolicyName = "ReactApp";

    public static IServiceCollection AddReactCorsPolicy(this IServiceCollection services, IConfiguration configuration)
    {
        var origin = configuration[$"{CorsOptions.SectionName}:AllowedOrigin"];

        if (string.IsNullOrWhiteSpace(origin))
            throw new InvalidOperationException("Cors:AllowedOrigin manquant dans la configuration.");

        var uri = new Uri(origin);
        var httpVariant = $"http://{uri.Authority}";
        var httpsVariant = $"https://{uri.Authority}";

        services.AddCors(options =>
        {
            options.AddPolicy(PolicyName, policy =>
            {
                policy.WithOrigins(httpVariant, httpsVariant)
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials();
            });
        });

        return services;
    }
}