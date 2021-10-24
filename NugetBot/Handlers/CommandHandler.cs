using Discord;
using Discord.Commands;
using Discord.WebSocket;
using NugetBot.Modules.TypeReaders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace NugetBot.Handlers
{
    public class CommandHandler : DiscordHandler
    {
        private DualCommandService _commandService;
        private DiscordShardedClient _client;

        public override async Task InitializeAsync(DiscordShardedClient client)
        {
            _client = client;

            _client.SlashCommandExecuted += Client_SlashCommandExecuted;

            _commandService = new DualCommandService();
            _commandService.Log += Program.LogAsync;

            //_commandService.AddTypeReader<DateTime>(DateTimeTypeReader.Instance);

            _commandService.CommandExecuted += _commandService_CommandExecuted;

            await _commandService.RegisterModulesAsync(Assembly.GetExecutingAssembly(), null);
        }

        private async Task _commandService_CommandExecuted(Discord.Optional<CommandInfo> arg1, ICommandContext icontext, IResult result)
        {
            var context = (DualCommandContext)icontext;

            if (!result.IsSuccess)
            {
                if(result is ExecuteResult exResult)
                {
                    var embed = new EmbedBuilder()
                        .WithTitle("Command Failed")
                        .WithDescription($"{(exResult.Error.HasValue ? $"{exResult.Error.Value}: " : "")}{exResult.ErrorReason}\n{exResult.Exception?.Message}")
                        .WithColor(Styles.Danger);

                    await context.ReplyAsync(embed: embed.Build());
                }
                else
                {
                    var embed = new EmbedBuilder()
                        .WithTitle("Command Failed")
                        .WithDescription($"{(result.Error.HasValue ? $"{result.Error.Value}: " : "")}{result.ErrorReason}")
                        .WithColor(Styles.Danger);

                    await context.ReplyAsync(embed: embed.Build());
                }
            }

        }

        private async Task Client_SlashCommandExecuted(SocketSlashCommand arg)
        {
            var context = new DualCommandContext(_client, arg);

            var name = arg.CommandName;

            if (arg.Data.Options?.Count == 1 && arg.Data.Options?.First().Type == Discord.ApplicationCommandOptionType.SubCommand)
            {
                name += " " + GetSubName(arg.Data.Options.First());
            }


            await _commandService.ExecuteAsync(context, name, null).ConfigureAwait(false);
        }

        private string GetSubName(SocketSlashCommandDataOption opt)
        {
            if (opt == null)
                return "";

            if (opt.Type == Discord.ApplicationCommandOptionType.SubCommand)
            {
                var others = GetSubName(opt.Options?.FirstOrDefault());

                return opt.Name + " " + others;
            }

            return "";
        }
    }
}
