using System.ComponentModel;
using Spectre.Console;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.Json;
using Spectre.Console.Cli;
using Spectre.Console.Json;

namespace GitHubConsole;

[Description("Showcases a device flow authentication with a functions app")]
public partial class RunCommand : AsyncCommand<RunCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [Description("Your GitHub OAuth app's Client ID")]
        [CommandArgument(0, "<client-id>")]
        public required string ClientId { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter<AuthError>() },
            WriteIndented = true
        };

        var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("GitHubFunctions", "0.1"));

        if (Debugger.IsAttached)
            http.Timeout = TimeSpan.FromMinutes(10);

        // Whether we need to do the device flow auth again.
        var authenticate = true;

        // Check existing creds, if any
        var store = GitCredentialManager.CredentialManager.Create("com.devlooped");
        // We use the client ID to persist the token, so it can be used across different apps.
        var creds = store.Get("https://github.com", settings.ClientId);

        if (creds != null)
        {
            // Try using the creds to see if they are still valid.
            var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user");
            request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + creds.Password);
            if (await http.SendAsync(request) is HttpResponseMessage { IsSuccessStatusCode: true } response)
            {
                var user = await response.Content.ReadFromJsonAsync<JsonElement>();
                AnsiConsole.MarkupLine($"[green]Logged in as[/]: {user.GetProperty("login").GetString()}");

                // Add the creds to the default headers in the http client for subsequent requests.
                http.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", "Bearer " + creds.Password);
                authenticate = false;
            }
        }

        if (authenticate)
        {
            // Perform device flow auth. See https://docs.github.com/en/apps/oauth-apps/building-oauth-apps/authorizing-oauth-apps#device-flow

            var codeUrl = $"https://github.com/login/device/code?client_id={settings.ClientId}&scope=read:user,read:org";
            var auth = await(await http.PostAsync(codeUrl, null)).Content.ReadFromJsonAsync<Auth>(options);
            
            // Render the auth response as JSON to console, user should copy the code to paste on the URL in the browser
            AnsiConsole.Write(new JsonText(JsonSerializer.Serialize(auth, options)));
            AnsiConsole.WriteLine();

            // Start the browser to the verification URL
            Process.Start(new ProcessStartInfo(auth!.verification_uri) { UseShellExecute = true });

            AuthCode? code;
            do
            {
                // Be gentle with the backend, wait for the interval before polling again.
                await Task.Delay(TimeSpan.FromSeconds(auth!.interval));

                var url = $"https://github.com/login/oauth/access_token?client_id={settings.ClientId}&device_code={auth.device_code}&grant_type=urn:ietf:params:oauth:grant-type:device_code";

                code = await(await http.PostAsync(url, null)).Content.ReadFromJsonAsync<AuthCode>(options);

                // Render status and code again, just in case.
                AnsiConsole.Write(new JsonText(JsonSerializer.Serialize(code, options)));
                AnsiConsole.WriteLine();

                if (code!.error == AuthError.slow_down && code.interval is int interval)
                {
                    // This is per the docs, we should slow down the polling.
                    await Task.Delay(TimeSpan.FromSeconds(interval));
                }
                else if (code.error == AuthError.expired_token)
                {
                    // We need an entirely new code, start over.
                    auth = await(await http.PostAsync(codeUrl, null)).Content.ReadFromJsonAsync<Auth>();
                    AnsiConsole.Write(new JsonText(JsonSerializer.Serialize(auth, options)));
                    AnsiConsole.WriteLine();
                }
                // Continue while we have an error, meaning the code has not been authorized yet.
            } while (code.error != null);

            // At this point, we should have a valid access token with the right scopes.
            http.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", "Bearer " + code.access_token);
            store.AddOrUpdate("https://github.com", settings.ClientId, code.access_token);
        }

        // If debugging, we'll invoke the locally running function app, otherwise, we'll use the deployed one.
        var meUrl = Debugger.IsAttached ?
            "http://localhost:7110/me" :
            "https://ghauth.azurewebsites.net/me";

        var data = await http.GetStringAsync(meUrl);

        AnsiConsole.Write(
            new Panel(new JsonText(data))
            {
                Header = new PanelHeader(meUrl),
            });

        // NOTE: we can read both user profile and org memberships, due to the requested scopes.
        var me = await http.GetStringAsync("https://api.github.com/user");
        AnsiConsole.Write(
            new Panel(new JsonText(me))
            {
                Header = new PanelHeader("https://api.github.com/user"),
            });

        var orgs = await http.GetStringAsync("https://api.github.com/user/memberships/orgs");
        AnsiConsole.Write(
            new Panel(new JsonText(orgs))
            {
                Header = new PanelHeader("https://api.github.com/user"),
            });

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[green]Success![/]");

        return 0;
    }
}