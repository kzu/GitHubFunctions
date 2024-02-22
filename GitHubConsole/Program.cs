using GitHubConsole;
using Spectre.Console;
using Spectre.Console.Cli;

var app = new CommandApp<RunCommand>();

if (args.Length == 0)
{
    var clientId = AnsiConsole.Ask<string>("Please enter the app's Client ID");
    args = [clientId];
}

await app.RunAsync(args);

Console.ReadLine();