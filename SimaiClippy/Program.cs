// See https://aka.ms/new-console-template for more information
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;


static async Task MainAsync()
{
    var client = new DiscordClient(new DiscordConfiguration()
    {
        Token = "YOUR_TOKEN_GOES_HERE",
        TokenType = TokenType.Bot,
        Intents = DiscordIntents.AllUnprivileged | DiscordIntents.MessageContents,
        MinimumLogLevel = Microsoft.Extensions.Logging.LogLevel.Debug,
    });

    var commands = client.UseCommandsNext(new CommandsNextConfiguration()
    {
        StringPrefixes = new[] { "?" }
    });
    commands.RegisterCommands(Assembly.GetExecutingAssembly());

    var activity = new DiscordActivity("?check", ActivityType.ListeningTo);
    
    await client.ConnectAsync(activity);
    await Task.Delay(-1);
}

MainAsync().GetAwaiter().GetResult();
