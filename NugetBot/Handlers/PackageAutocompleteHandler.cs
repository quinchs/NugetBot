using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Driver;

namespace NugetBot.Handlers
{
    public class PackageAutocompleteHandler : DiscordHandler
    {
        private Logger _log;
        public override void Initialize(DiscordShardedClient client)
        {
            _log = Logger.GetLogger<PackageAutocompleteHandler>();
            client.AutocompleteExecuted += Client_AutocompleteExecuted;
        }

        private async Task Client_AutocompleteExecuted(SocketAutocompleteInteraction arg)
        {
            switch (arg.Data.Current.Name)
            {
                case "package_id" when(arg.Data.Options.FirstOrDefault()?.Name == "add" && arg.User is IGuildUser):
                    {
                        Stopwatch sw = Stopwatch.StartNew();
                        var results = await NugetService.SearchAsync(arg.Data.Current.Value?.ToString() ?? "").ConfigureAwait(false);
                        sw.Stop();
                        await arg.RespondAsync(results.Select(x => new Discord.AutocompleteResult(x.Title, x.Identity.Id)).Take(20));
                        _log.Trace($"Autocomplete for packages took {sw.ElapsedMilliseconds}ms");
                    }
                    break;

                case "package_id" when ((arg.Data.Options.FirstOrDefault()?.Name == "remove" || arg.Data.Options.FirstOrDefault()?.Name == "downloads") && arg.User is IGuildUser guildUser):
                    {
                        Stopwatch sw = Stopwatch.StartNew();
                        var results = await MongoService.PackageTracker.Find(x => x.GuildId == guildUser.GuildId).Limit(20).ToListAsync().ConfigureAwait(false);
                        sw.Stop();
                        await arg.RespondAsync(results.Select(x => new Discord.AutocompleteResult(x.PackageName, x.PackageName)).Take(20));
                        _log.Trace($"Autocomplete for packages took {sw.ElapsedMilliseconds}ms");
                    }
                    break;
            }
        }
    }
}
