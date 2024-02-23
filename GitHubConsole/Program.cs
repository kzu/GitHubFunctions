using GitHubConsole;
using Spectre.Console;
using Spectre.Console.Cli;

var app = new CommandApp<RunCommand>();

if (args.Length == 0)
{
    var clientId = AnsiConsole.Ask<string>("Please enter the app's Client ID");
    args = [clientId];
}

while (true)
{
    await app.RunAsync(args);
    AnsiConsole.MarkupLine("Press [green]Enter[/] to authenticate again, or any other key to exit.");
    if (Console.ReadKey().Key != ConsoleKey.Enter)
        break;
}