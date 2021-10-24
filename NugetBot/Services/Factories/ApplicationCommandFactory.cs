using Discord;
using Discord.Rest;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NugetBot
{
    public abstract class ApplicationCommandFactory
    {
        public DiscordShardedClient Client;
        public abstract IEnumerable<ApplicationCommandProperties> BuildCommands();

        public virtual Task OnRegisterSingleAsync(RestApplicationCommand command) { return Task.CompletedTask; }

        public virtual void OnRegisterSingle(RestApplicationCommand command) { }

        public virtual Task OnRegisterAllAsync(IReadOnlyCollection<RestApplicationCommand> commands) { return Task.CompletedTask; }
        public virtual void OnRegisterAll(IReadOnlyCollection<RestApplicationCommand> commands) { }
    }

    public class GuildSpecificCommand : Attribute
    {
        public ulong GuildId { get; }

        public GuildSpecificCommand(ulong guildId)
        {
            this.GuildId = guildId;
        }
    }

    public class RequreReadyEvent : Attribute { }
}
