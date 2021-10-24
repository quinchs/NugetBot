using Discord;
using Discord.Commands;
using NugetBot.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NugetBot.Modules
{
    [RequireContext(ContextType.Guild), RequireUserPermission(GuildPermission.Administrator, ErrorMessage = "You must be an administrator to use this command")]
    public class ConfigurationModule : DualModuleBase
    {
        [Command("packages add")]
        public async Task AddPackageAsync([Name("package_id")] string name)
        {
            await DeferAsync();

            if (!await NugetService.PackageExists(name) || await NugetPackageTracker.GetAsync(name, Context.Guild.Id) != null)
            {
                var errorEmbed = new EmbedBuilder()
                    .WithTitle("Error")
                    .WithDescription("Package doesnt exist or its already added, please enter a valid package id!")
                    .WithColor(Styles.Danger);

                await ReplyAsync(embed: errorEmbed.Build());
                return;
            }

            var tracker = await NugetPackageTracker.CreateAsync(name, Context.Guild.Id).ConfigureAwait(false);

            var successEmbed = new EmbedBuilder()
                .WithAuthor($"{name} by {tracker.Author}", tracker.IconUrl)
                .WithTitle("Package Added")
                .WithColor(Styles.Primary)
                .WithDescription($"The package was successfully added to the tracker.");

            ImageGenerator.CreateGraph(tracker, DateTime.UtcNow.AddMonths(-2));

            await ReplyAsync(embed: successEmbed.Build()).ConfigureAwait(false);
        }

        [Command("packages remove")]
        public async Task RemovePackageAsync([Name("package_id")]  string name)
        {
            await DeferAsync();

            var tracker = await NugetPackageTracker.GetAsync(name, Context.Guild.Id);

            if(tracker == null)
            {
                var errorEmbed = new EmbedBuilder()
                   .WithTitle("Error")
                   .WithDescription($"The package {name} isn't in the tracker!")
                   .WithColor(Styles.Danger);

                await ReplyAsync(embed: errorEmbed.Build());
                return;
            }

            await tracker.DeleteAsync();

            var successEmbed = new EmbedBuilder()
                .WithAuthor($"{name} by {tracker.Author}", tracker.IconUrl)
                .WithTitle("Package Removed")
                .WithColor(Styles.Primary)
                .WithDescription($"The package was successfully removed from the tracker.");

            await ReplyAsync(embed: successEmbed.Build()).ConfigureAwait(false);
        }
    }
}
