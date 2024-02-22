using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication(builder =>
    {
        builder.UseFunctionContextAccessor();
        // Logs errors
        builder.UseMiddleware<ErrorMiddleware>();
        // Web-flow auth middleware
        builder.UseMiddleware<ClientPrincipalMiddleware>();
        // Api/device flow auth middleware
        builder.UseMiddleware<GitHubTokenMiddleware>();
    })
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        services.AddHttpClient();

        // Message handler that passes down current function invocation token 
        // to http client requests
        services.AddScoped<ClaimsMessageHandler>();
        services.AddHttpClient("user", http =>
        {
            http.BaseAddress = new Uri("https://api.github.com");
            http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("GitHubFunctions", "1.0"));
        }).AddHttpMessageHandler<ClaimsMessageHandler>();
    })
    .Build();

host.Run();