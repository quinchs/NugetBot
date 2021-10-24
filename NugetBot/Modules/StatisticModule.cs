using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Driver;
using System.IO;
using System.Drawing.Imaging;
using Discord;
using NugetBot.Models;

namespace NugetBot.Modules
{
    [RequireContext(ContextType.Guild)]
    [Group("statistics")]
    public class StatisticModule : DualModuleBase
    {
        [Command("downloads")]
        public async Task ListPackageStatisticsAsync([Name("package_id")]string package = null, DateTime? from = null, DateTime? to = null)
        {
            await DeferAsync();

            if(package == null)
            {
                var packages = await MongoService.PackageTracker.Find(x => x.GuildId == Context.Guild.Id).ToListAsync();

                if (!packages.Any())
                {
                    await ReplyAsync(embed: new EmbedBuilder()
                        .WithTitle("No packages found")
                        .WithDescription("There are currently no packages configured yet to track!")
                        .WithColor(Styles.Danger)
                        .Build());
                    return;
                }

                if(packages.Count() == 1)
                {
                    var pkg = packages.FirstOrDefault();

                    var image = ImageGenerator.CreateGraph(pkg, from, to);

                    using(var stream = new MemoryStream())
                    {
                        image.Image.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                        stream.Position = 0;
                        await Context.Interaction.FollowupWithFileAsync(stream, "graph.png", embed: new EmbedBuilder()
                            .WithAuthor($"{pkg.PackageName}", pkg.IconUrl)
                            .WithColor(Styles.Primary)
                            .WithDescription($"Total downloads: {pkg.AvgTotalDownloads}\n" +
                                         $"Current version: {pkg.CurrentVersion.Version} - {pkg.CurrentVersion.Downloads}\n" +
                                         $"Most downloaded version: {pkg.MostDownloadedVersion.Version} - {pkg.MostDownloadedVersion.Downloads}\n" +
                                         $"Downloads per day: {pkg.PerDayAverage}")
                            .WithImageUrl("attachment://graph.png")
                            .Build());
                    }

                    image.Image.Dispose();
                }
                else
                {
                    var image = ImageGenerator.CreateGraph(packages, from, to);

                    var desc = string.Join("\n", image.Colors.Select(x => $"{ColorUtils.GetColorEmoji(x.Value)} - {x.Key}"));

                    using (var stream = new MemoryStream())
                    {
                        image.Image.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                        stream.Position = 0;
                        var embed = new EmbedBuilder()
                            .WithColor(Styles.Primary)
                            .WithDescription(desc)
                            .WithImageUrl("attachment://graph.png");

                        embed.WithFields(packages.Select(pkg => new EmbedFieldBuilder()
                            .WithName(pkg.PackageName)
                            .WithValue($"> Total downloads: {pkg.AvgTotalDownloads}\n" +
                                         $"> Current version: {pkg.CurrentVersion.Version} - {pkg.CurrentVersion.Downloads}\n" +
                                         $"> Most downloaded version: {pkg.MostDownloadedVersion.Version} - {pkg.MostDownloadedVersion.Downloads}\n" +
                                         $"> Downloads per day: {pkg.PerDayAverage}")
                        ));

                        await Context.Interaction.FollowupWithFileAsync(stream, "graph.png", embed: embed.Build());
                    }

                    image.Image.Dispose();
                }
            }
            else
            {
                var pkg = await NugetPackageTracker.GetAsync(package, Context.Guild.Id).ConfigureAwait(false);

                if(pkg == null)
                {
                    await ReplyAsync(embed: new EmbedBuilder()
                        .WithTitle("No packages found")
                        .WithDescription("There are currently no packages configured yet to track!")
                        .WithColor(Styles.Danger)
                        .Build());
                    return;
                }

                var image = ImageGenerator.CreateGraph(pkg, from, to);

                using (var stream = new MemoryStream())
                {
                    image.Image.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                    stream.Position = 0;
                    await Context.Interaction.FollowupWithFileAsync(stream, "graph.png", embed: new EmbedBuilder()
                        .WithAuthor($"{pkg.PackageName}", pkg.IconUrl)
                        .WithColor(Styles.Primary)
                        .WithDescription($"Total downloads: {pkg.AvgTotalDownloads}\n" +
                                         $"Current version: {pkg.CurrentVersion.Version} - {pkg.CurrentVersion.Downloads}\n" +
                                         $"Most downloaded version: {pkg.MostDownloadedVersion.Version} - {pkg.MostDownloadedVersion.Downloads}\n" +
                                         $"Downloads per day: {pkg.PerDayAverage}")
                        .WithImageUrl("attachment://graph.png")
                        .Build());
                }

                image.Image.Dispose();
            }
        }
    }
}
