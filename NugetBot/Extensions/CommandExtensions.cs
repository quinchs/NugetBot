using Discord;
using Discord.Rest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NugetBot
{
    public static class CommandExtensions
    {
        public static ApplicationCommandProperties ToProperties(this RestGuildCommand command)
        {
            switch (command.Type)
            {
                case ApplicationCommandType.Slash:
                    var props = new SlashCommandBuilder()
                    {
                        Name = command.Name,
                        DefaultPermission = command.IsDefaultPermission,
                        Description = command.Description,
                        Options = command.Options?.Any() ?? false ? command.Options.Select(x => new SlashCommandOptionBuilder()
                        {
                            Type = x.Type,
                            Required = x.IsRequired ?? false,
                            Name = x.Name,
                            Default = x.IsDefault,
                            Description = x.Description,
                            Choices = x.Choices?.Any() ?? false ? x.Choices.Select(x => new ApplicationCommandOptionChoiceProperties()
                            {
                                Name = x.Name,
                                Value = x.Value
                            }).ToList() : null,
                            Options = x.Options?.Any() ?? false ? x.Options.Select(x => ToProperties(x)).ToList() : null
                        }).ToList() : null,
                    };

                    return props.Build();

                default:
                    return null;
            }
        }

        public static SlashCommandOptionBuilder ToProperties(RestApplicationCommandOption x)
        {
            return new SlashCommandOptionBuilder()
            {
                Type = x.Type,
                Required = x.IsRequired ?? false,
                Name = x.Name,
                Default = x.IsDefault,
                Description = x.Description,
                Choices = x.Choices?.Any() ?? false ? x.Choices.Select(y => new ApplicationCommandOptionChoiceProperties()
                {
                    Name = y.Name,
                    Value = y.Value
                }).ToList() : null,
                Options = x.Options?.Any() ?? false ? x.Options.Select(z => ToProperties(z)).ToList() : null
            };
        }

    }
}
