using Disqord.Bot.Hosting;
using Disqord.Gateway;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = new HostBuilder()
    .ConfigureHostConfiguration(host => host.AddEnvironmentVariables("CHEAP_"))
    .ConfigureAppConfiguration((ctx, config) =>
    {
        var env = ctx.HostingEnvironment;
        config.AddJsonFile("appsettings.json", false, true);

        if (File.Exists($"appsettings.{env.EnvironmentName}.json"))
            config.AddJsonFile($"appsettings.{env.EnvironmentName}.json", true, true);
    })
    .ConfigureLogging(logging =>
    {
        logging.AddSimpleConsole();
    })
    .ConfigureDiscordBot((context, bot) =>
    {
        bot.Token = context.Configuration["Token"];
        bot.Prefixes = new[] { "?" };
        bot.Intents = GatewayIntents.Unprivileged | GatewayIntents.MessageContent;
        bot.ServiceAssemblies = AppDomain.CurrentDomain.GetAssemblies();
    })
    .UseDefaultServiceProvider(options =>
    {
        options.ValidateScopes = true;
        options.ValidateOnBuild = true;
    })
    .UseConsoleLifetime();

var host = builder.Build();
await host.RunAsync();