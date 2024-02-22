using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication(builder =>
    {
        builder.UseMiddleware<FunctionContextAccessorMiddleware>();
        builder.UseMiddleware<ErrorMiddleware>();
        builder.UseMiddleware<ClientPrincipalMiddleware>();
        builder.UseMiddleware<GitHubTokenMiddleware>();
    })
    .ConfigureServices(services =>
    {
        services.AddHttpContextAccessor();
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        services.AddSingleton<IFunctionContextAccessor, FunctionContextAccessor>();
        services.AddScoped<ClaimsMessageHandler>();

        services.AddHttpClient();
        services.AddHttpClient("user", http =>
        {
            http.BaseAddress = new Uri("https://api.github.com");
            http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("GitHubFunctions", "1.0"));
        }).AddHttpMessageHandler<ClaimsMessageHandler>();
    })
    .Build();

host.Run();