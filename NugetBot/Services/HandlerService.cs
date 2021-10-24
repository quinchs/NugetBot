using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace NugetBot
{
    public abstract class DiscordHandler
    {
        public virtual Task InitializeAsync(DiscordShardedClient client)
        {
            return Task.CompletedTask;
        }

        public virtual void Initialize(DiscordShardedClient client)
        {
        }
    }
}

namespace NugetBot
{
    public class HandlerService
    {
        private readonly DiscordShardedClient _client;
        private readonly Logger _log;
        private static readonly Dictionary<DiscordHandler, object> _handlers = new Dictionary<DiscordHandler, object>();

        public static T GetHandlerInstance<T>()
            where T : DiscordHandler => _handlers.FirstOrDefault(x => x.Key.GetType() == typeof(T)).Value as T;

        private bool _hasInit = false;
        private object _lock = new object();

        public HandlerService(DiscordShardedClient client)
        {
            this._client = client;
            this._client.ShardReady += Client_ShardReady;
            _log = Logger.GetLogger<HandlerService>();

            List<Type> typs = new List<Type>();
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (Type type in assembly.GetTypes())
                {
                    if (type.IsAssignableTo(typeof(DiscordHandler)) && type != typeof(DiscordHandler))
                    {
                        typs.Add(type);
                    }
                }
            }

            foreach (var handler in typs)
            {
                var inst = Activator.CreateInstance(handler);
                _handlers.Add(inst as DiscordHandler, inst);
            }

            _log.Log($"Created {_handlers.Count} handlers");
        }

        private Task Client_ShardReady(DiscordSocketClient arg)
        {
            lock (_lock)
            {
                if (!_hasInit)
                {
                    _hasInit = true;
                }
                else return Task.CompletedTask;
            }

            _ = Task.Run(() =>
            {
                var work = new List<Func<Task>>();

                foreach (var item in _handlers)
                {
                    work.Add(async () =>
                    {
                        try
                        {
                            await item.Key.InitializeAsync(this._client);
                            item.Key.Initialize(this._client);
                        }
                        catch (Exception x)
                        {
                            _log.Error($"Exception occured while initializing {item.Key.GetType().Name}: ", exception: x);
                        }
                    });
                }

                Task.WaitAll(work.Select(x => x()).ToArray());

                _log.Info($"Initialized <Green>{_handlers.Count}</Green> handlers!");
            });

            return Task.CompletedTask;
        }
    }
}
