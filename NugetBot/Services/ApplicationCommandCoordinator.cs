using Discord;
using Discord.Rest;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace NugetBot
{
    public class ApplicationCommandCoordinator
    {
        private readonly DiscordShardedClient client;
        private List<CommandRegisterInfo> _commands;
        private List<ApplicationCommandFactory> _readyQueue;
        private Logger _log;
        private readonly TaskCompletionSource registrarSource;
        private object _lock = new object();
        private bool _hasInit = false;

        public ApplicationCommandCoordinator(DiscordShardedClient client)
        {
            this.client = client;
            _log = Logger.GetLogger<ApplicationCommandCoordinator>();
            client.ShardReady += InitializeCommandsAsync;
            registrarSource = new TaskCompletionSource();
            PreregisterCommands();
        }

        public void PreregisterCommands()
        {
            List<CommandRegisterInfo> commands = new();
            List<ApplicationCommandFactory> readyQueue = new();

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (Type type in assembly.GetTypes())
                {
                    if (type.IsAssignableTo(typeof(ApplicationCommandFactory)) && type != typeof(ApplicationCommandFactory))
                    {
                        var instance = Activator.CreateInstance(type) as ApplicationCommandFactory;
                        instance.Client = this.client;


                        var buildMethod = type.GetMethod("BuildCommands");
                        var guildAttribute = buildMethod.GetCustomAttribute<GuildSpecificCommand>();
                        var readyAttribute = buildMethod.GetCustomAttribute<RequreReadyEvent>();

                        if (readyAttribute != null)
                        {
                            readyQueue.Add(instance);
                            continue;
                        }

                        var commandProperties = instance.BuildCommands();

                        if (guildAttribute != null)
                        {
                            commands.Add(new CommandRegisterInfo(commandProperties, instance, guildAttribute.GuildId));

                        }
                        else
                        {
                            commands.Add(new CommandRegisterInfo(commandProperties, instance));
                        }
                    }
                }
            }

            this._commands = commands;
            this._readyQueue = readyQueue;

            registrarSource.SetResult();
        }

        private void EmptyReadyQueue()
        {
            foreach (var factory in _readyQueue)
            {
                var buildMethod = factory.GetType().GetMethod("BuildCommands");
                var guildAttribute = buildMethod.GetCustomAttribute<GuildSpecificCommand>();


                var commandProperties = factory.BuildCommands();

                if (guildAttribute != null)
                {
                    _commands.Add(new CommandRegisterInfo(commandProperties, factory, guildAttribute.GuildId));

                }
                else
                {
                    _commands.Add(new CommandRegisterInfo(commandProperties, factory));
                }
            }
        }

        private async Task InitializeCommandsAsync(DiscordSocketClient client)
        {
            lock (_lock)
            {
                if (_hasInit)
                    return;

                _hasInit = true;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await registrarSource.Task;

                    EmptyReadyQueue();

                    var global = _commands.Where(x => !x.GuildId.HasValue);
                    var guild = _commands.Where(x => x.GuildId.HasValue);

                    if (global.Any())
                    {
                        var commands = await client.Rest.BulkOverwriteGlobalCommands(global.SelectMany(x => x.Properties).ToArray());

                        foreach (var command in commands)
                        {
                            global.FirstOrDefault(x => x.Properties.Any(x => x.Name.GetValueOrDefault() == command.Name))?.ExecuteSingleCallback(command);
                        }

                        foreach (var factoryRegisterInfo in global)
                        {
                            var factoryCommands = commands.Where(x => factoryRegisterInfo.Properties.Any(y => y.Name.GetValueOrDefault() == x.Name));

                            factoryRegisterInfo.ExecuteAllCallback(factoryCommands.ToImmutableArray());
                        }
                    }

                    if (guild.Any())
                    {
                        Dictionary<ulong, List<ApplicationCommandProperties>> props = new();

                        foreach (var factoryRegisterInfo in guild)
                        {
                            if (props.ContainsKey(factoryRegisterInfo.GuildId.Value))
                            {
                                var lc = new List<ApplicationCommandProperties>(factoryRegisterInfo.Properties);

                                lc.AddRange(props[factoryRegisterInfo.GuildId.Value]);

                                props[factoryRegisterInfo.GuildId.Value] = lc;
                            }
                            else
                            {
                                props.Add(factoryRegisterInfo.GuildId.Value, new List<ApplicationCommandProperties>(factoryRegisterInfo.Properties));
                            }
                        }

                        foreach (var item in props)
                        {
                            // get the current list of commands.
                            var current = await client.Rest.GetGuildApplicationCommands(item.Key);

                            var difference = current.Where(x => !item.Value.Any(y => y.Name.GetValueOrDefault() == x.Name)).ToArray();

                            item.Value.AddRange(difference.Select(x => x.ToProperties()));

                            var commands = await client.Rest.BulkOverwriteGuildCommands(item.Value.ToArray(), item.Key);


                            foreach (var command in commands)
                            {
                                guild.FirstOrDefault(x => x.Properties.Any(y => y.Name.GetValueOrDefault() == command.Name))?.ExecuteSingleCallback(command);
                            }

                            foreach (var regInfo in guild)
                            {
                                List<RestGuildCommand> allCommands = new List<RestGuildCommand>(commands);
                                allCommands.AddRange(current);

                                var distinctCommands = allCommands.GroupBy(x => x.Name).Select(x => x.First()).ToList();

                                var cmds = distinctCommands.Where(x => regInfo.Properties.Any(y => commands.Any(z => z.Name == y.Name.GetValueOrDefault())));

                                regInfo.ExecuteAllCallback(cmds.ToArray());
                            }
                        }
                    }
                }
                catch (Exception x)
                {
                    _log.Critical("Failed to initialize commands!", Severity.Core, x);
                }
            });
        }

        private class CommandRegisterInfo
        {
            public ulong? GuildId { get; }
            public IEnumerable<ApplicationCommandProperties> Properties { get; }
            public ApplicationCommandFactory Factory { get; }

            public CommandRegisterInfo(IEnumerable<ApplicationCommandProperties> properties, ApplicationCommandFactory factory, ulong? guildId = null)
            {
                this.GuildId = guildId;
                this.Properties = properties;
                this.Factory = factory;
            }

            public void ExecuteSingleCallback(RestApplicationCommand cmd)
            {
                var factory = this.Factory;
                _ = Task.Run(async () =>
                {
                    factory.OnRegisterSingle(cmd);
                    await factory.OnRegisterSingleAsync(cmd);
                });
            }

            public void ExecuteAllCallback(IReadOnlyCollection<RestApplicationCommand> cmds)
            {
                var factory = this.Factory;
                _ = Task.Run(async () =>
                {
                    factory.OnRegisterAll(cmds);
                    await factory.OnRegisterAllAsync(cmds);
                });
            }
        }
    }
}
