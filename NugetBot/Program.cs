using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using System;
using System.Threading.Tasks;

namespace NugetBot
{
    class Program
    {
        static void Main(string[] args)
        {
            new Program().MainAsync().GetAwaiter().GetResult();
        }

        private static Logger _log;

        public async Task MainAsync()
        {

            //var s = await NugetService.SearchAsync("");
            //var n = await NugetService.GetPackageInfoAsync("Discord.Net.Labs");

            ConfigService.LoadConfig();
            ConfigureLogger();
            ConfigureCaches();

            var img = ImageGenerator.CreateGraph(MongoService.PackageTracker.Find(x => true).FirstOrDefault());


            _log = Logger.GetLogger<Program>();

            var client = new DiscordShardedClient(new DiscordSocketConfig()
            {
                GatewayIntents = GatewayIntents.All,
                MessageCacheSize = 50,
                AlwaysDownloadUsers = false,
                LogLevel = LogSeverity.Debug,
                TotalShards = 1,
            });

            client.Log += LogAsync;
            client.ShardReady += ReadyAsync;

            var handlerService = new HandlerService(client);

            var commandCoordinator = new ApplicationCommandCoordinator(client);

            await client.LoginAsync(TokenType.Bot, ConfigService.Config.Token);
            await client.StartAsync();
            await client.SetStatusAsync(UserStatus.Online);
            await client.SetGameAsync("with new packages!", type: ActivityType.Playing);

            _log.Log("<Green>Services created successfully!</Green>");

            await Task.Delay(-1);
        }

        private Task ReadyAsync(DiscordSocketClient client)
        {
            _log.Info($"Shard {client.ShardId} ready!");

            return Task.CompletedTask;
        }

        public static Task LogAsync(LogMessage log)
        {
            var msg = log.Message;

            if (log.Source.StartsWith("Audio ") && (msg?.StartsWith("Sent") ?? false))
                return Task.CompletedTask;

            Severity? sev = null;

            if (log.Source.StartsWith("Gateway"))
                sev = Severity.Socket;
            if (log.Source.StartsWith("Rest"))
                sev = Severity.Rest;

            _log.Write($"{log.Message}", sev.HasValue ? new Severity[] { sev.Value, log.Severity.ToLogSeverity() } : new Severity[] { log.Severity.ToLogSeverity() }, log.Exception);

            return Task.CompletedTask;
        }

        private void ConfigureLogger()
        {
            Logger.AddStream(Console.OpenStandardError(), StreamType.StandardError);
            Logger.AddStream(Console.OpenStandardOutput(), StreamType.StandardOut);
        }

        private void ConfigureCaches()
        {
            PackageTrackerCache.Configure(3, 150);
        }
    }
}
