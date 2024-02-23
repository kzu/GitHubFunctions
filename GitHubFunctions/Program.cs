using System.Net.Http.Headers;
using GitHubFunctions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication(builder =>
    {
        // Allows access to the function context from scoped DI components.
        builder.UseFunctionContextAccessor();
        // Logs errors
        builder.UseMiddleware<ErrorMiddleware>();

        // Populates the current principal from X-MS-CLIENT-PRINCIPAL
        builder.UseAppServiceAuthentication();
        // Api/device flow auth middleware
        builder.UseGitHubAuthentication();
        // Adds the current principal from either from above
        builder.UseClaimsPrincipal();
    })
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // Message handler that passes down current auth token (if any) to http client requests
        services.AddScoped<AccessTokenMessageHandler>();
        services.AddHttpClient("user", http =>
        {
            http.BaseAddress = new Uri("https://api.github.com");
            http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("GitHubFunctions", "1.0"));
        }).AddHttpMessageHandler<AccessTokenMessageHandler>();
    })
    .Build();

host.Run();