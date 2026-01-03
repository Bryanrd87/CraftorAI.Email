using System.Net.Http.Headers;
using CraftorAI.Email.Configuration;
using CraftorAI.Email.Contracts;
using CraftorAI.Email.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CraftorAI.Email.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds CraftorAI email services using Resend as the email provider.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration containing Resend settings.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCraftorEmailServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register configuration
        services.Configure<ResendConfiguration>(
            configuration.GetSection(ResendConfiguration.SectionName));

        // Register HttpClient with Resend configuration
        services.AddHttpClient<IEmailService, ResendEmailService>((serviceProvider, client) =>
        {
            var config = serviceProvider.GetRequiredService<IOptions<ResendConfiguration>>().Value;

            client.BaseAddress = new Uri("https://api.resend.com");
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", config.ApiKey);
            client.Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });

        return services;
    }

    /// <summary>
    /// Adds CraftorAI email services with custom configuration action.
    /// </summary>
    public static IServiceCollection AddCraftorEmailServices(
        this IServiceCollection services,
        Action<ResendConfiguration> configureOptions)
    {
        services.Configure(configureOptions);

        services.AddHttpClient<IEmailService, ResendEmailService>((serviceProvider, client) =>
        {
            var config = serviceProvider.GetRequiredService<IOptions<ResendConfiguration>>().Value;

            client.BaseAddress = new Uri("https://api.resend.com");
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", config.ApiKey);
            client.Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });

        return services;
    }
}
