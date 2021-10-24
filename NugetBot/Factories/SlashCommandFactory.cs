using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NugetBot.Factories
{
    public class SlashCommandFactory : ApplicationCommandFactory
    {
        [GuildSpecificCommand(848176216011046962)]
        public override IEnumerable<ApplicationCommandProperties> BuildCommands()
        {
            var packageCommand = new SlashCommandBuilder()
                .WithName("packages")
                .WithDescription("Manage the connected packages")
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("add")
                    .WithDescription("Add a new package to the tracker")
                    .WithType(ApplicationCommandOptionType.SubCommand)
                    .AddOption("package_id", ApplicationCommandOptionType.String, "The id of the package, usually the name within the url", true, isAutocomplete:true)
                )
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("remove")
                    .WithDescription("Removes a package from the tracker.")
                    .WithType(ApplicationCommandOptionType.SubCommand)
                    .AddOption("package_id", ApplicationCommandOptionType.String, "The id of the package to remove", true, isAutocomplete: true)
                );

            var statsCommand = new SlashCommandBuilder()
                .WithName("statistics")
                .WithDescription("Get package statistics")
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("downloads")
                    .WithDescription("List the download statistics of the tracked nuget packages")
                    .WithType(ApplicationCommandOptionType.SubCommand)
                    .AddOption("package_id", ApplicationCommandOptionType.String, "The optional id of the package to show statistics for", false, isAutocomplete: true)
                    .AddOption("from", ApplicationCommandOptionType.String, "A start date time string from which to show stats for", false)
                    .AddOption("to", ApplicationCommandOptionType.String, "A end date time string from which to show stats for", false)
                );

            return new ApplicationCommandProperties[] { packageCommand.Build(), statsCommand.Build() };
        }
    }
}
