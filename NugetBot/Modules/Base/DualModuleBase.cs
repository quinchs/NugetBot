using Discord;
using Discord.Commands;
using Discord.Rest;
using Discord.WebSocket;
using NugetBot.Modules.TypeReaders;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NugetBot
{
    public class DualModuleBase
    {
        private static ConcurrentDictionary<Type, TypeReader> _defaultTypeReaders;

        static DualModuleBase()
        {
            _defaultTypeReaders = new ConcurrentDictionary<Type, TypeReader>();
            foreach (var type in PrimitiveParsers.SupportedTypes)
            {
                _defaultTypeReaders[type] = PrimitiveTypeReader.Create(type);
                _defaultTypeReaders[typeof(Nullable<>).MakeGenericType(type)] = NullableTypeReader.Create(type, _defaultTypeReaders[type]);
            }
        }

        private bool _hasDefferd = false;

        public DualCommandContext Context { get; private set; }

        public void SetContext(ICommandContext context)
        {
            this.Context = (DualCommandContext)context;
        }


        //public Task<IUserMessage> ReplyAsync(string message = null, bool isTTS = false, Embed embed = null, RequestOptions options = null, AllowedMentions allowedMentions = null, MessageReference messageReference = null, MessageComponent component = null)
        //    => ReplyAsync(message, isTTS: isTTS, embed: embed, options: options, allowedMentions: allowedMentions, messageReference: messageReference, component: component);

        public async Task<IUserMessage> ReplyAsync(string text = null, Embed[] embeds = null, bool isTTS = false, bool ephemeral = false, AllowedMentions allowedMentions = null, MessageReference messageReference = null, RequestOptions options = null, MessageComponent component = null, Embed embed = null)
        {
            if (this.Context.Interaction == null)
            {
                return await Context.Channel.SendMessageAsync(text, isTTS, embed, options, allowedMentions, messageReference, component);
            }
            else
            {
                if (!_hasDefferd)
                    await Context.Interaction.RespondAsync(text, embeds, isTTS, ephemeral, allowedMentions, options, component, embed);
                else
                    return await Context.Interaction.FollowupAsync(text, embeds, isTTS, ephemeral, allowedMentions, options, component, embed);
            }

            return null;
        }

        public Task DeferAsync(bool ephemeral = false, RequestOptions options = null)
        {
            if (Context.IsInteraction)
            {
                this._hasDefferd = true;
                return Context.Interaction.DeferAsync(ephemeral, options);
            }

            return Task.CompletedTask;
        }

        public Task<IUserMessage> FollowupAsync(string text = null, Embed[] embeds = null, bool isTTS = false, bool ephemeral = false, AllowedMentions allowedMentions = null, MessageReference messageReference = null, RequestOptions options = null, MessageComponent component = null, Embed embed = null)
        {
            if (Context.IsInteraction)
                return Context.Interaction.FollowupAsync(text, embeds, isTTS, ephemeral, allowedMentions, options, component, embed).ContinueWith(x => (IUserMessage)x);
            else
                return ReplyAsync(text, embeds, isTTS, ephemeral, allowedMentions, messageReference, options, component, embed);
        }

        public async Task<object[]> CreateInteractionArgsAsync(CommandInfo info, DualCommandService service, object[] args)
        {
            var returnParams = new List<object>();
            if (!Context.IsInteraction)
                return args;

            for (int i = 0; i != info.Parameters.Count; i++)
            {
                var param = info.Parameters[i];

                var paramName = ((NameAttribute)param.Attributes.FirstOrDefault(x => x.GetType() == typeof(NameAttribute)))?.Text ?? param.Name;

                if (Context.Interaction is SocketSlashCommand slash)
                {
                    try
                    {
                        var opts = slash.Data.Options;

                        if (opts == null)
                        {
                            returnParams.Add(Type.Missing);
                            continue;
                        }

                        while (opts.Count == 1 && opts.First().Type == ApplicationCommandOptionType.SubCommand)
                        {
                            opts = opts.First().Options;
                        }

                        var slashParam = opts?.FirstOrDefault(x => x.Name == paramName);

                        if (slashParam == null)
                        {
                            returnParams.Add(Type.Missing);
                            continue;
                        }

                        var tp = slashParam.Value.GetType();

                        object value = null;

                        if(_defaultTypeReaders.TryGetValue(param.Type, out var reader))
                        {
                            value = (await reader.ReadAsync(Context, slashParam.Value.ToString(), null)).Values.FirstOrDefault().Value;
                        }
                        else if(service.TryGetTypeReader(param.Type, out reader))
                        {
                            value = (await reader.ReadAsync(Context, slashParam.Value.ToString(), null)).Values.FirstOrDefault().Value;
                        }
                        else if (InternalConverters.ContainsKey((tp, param.Type)))
                        {
                            try
                            {
                                value = InternalConverters[(tp, param.Type)].Invoke(slashParam.Value);
                            }
                            catch(Exception x)
                            {
                                await param.HandleErrorAsync(Context, slashParam.Value).ConfigureAwait(false);
                                return null;
                            }
                        }
                        else if (tp.IsAssignableTo(param.Type))
                            value = slashParam.Value;
                        else
                        {
                            await param.HandleErrorAsync(Context, slashParam.Value).ConfigureAwait(false);
                            return null;
                        }
                        returnParams.Add(value);
                    }
                    catch
                    {
                        await param.HandleErrorAsync(Context, null).ConfigureAwait(false);
                        return null;
                    }
                }
            }

            return returnParams.ToArray();
        }

        private Dictionary<(Type from, Type to), Func<object, object>> InternalConverters = new Dictionary<(Type from, Type to), Func<object, object>>()
        {
            {(typeof(long), typeof(int)), (v) => { return Convert.ToInt32(v); } },
        };

        public virtual async Task BeforeExecuteAsync() { }
    }

    internal delegate bool TryParseDelegate<T>(string str, out T value);

    internal static class PrimitiveParsers
    {
        private static readonly Lazy<IReadOnlyDictionary<Type, Delegate>> Parsers = new Lazy<IReadOnlyDictionary<Type, Delegate>>(CreateParsers);

        public static IEnumerable<Type> SupportedTypes = Parsers.Value.Keys;

        static IReadOnlyDictionary<Type, Delegate> CreateParsers()
        {
            var parserBuilder = ImmutableDictionary.CreateBuilder<Type, Delegate>();
            parserBuilder[typeof(bool)] = (TryParseDelegate<bool>)bool.TryParse;
            parserBuilder[typeof(sbyte)] = (TryParseDelegate<sbyte>)sbyte.TryParse;
            parserBuilder[typeof(byte)] = (TryParseDelegate<byte>)byte.TryParse;
            parserBuilder[typeof(short)] = (TryParseDelegate<short>)short.TryParse;
            parserBuilder[typeof(ushort)] = (TryParseDelegate<ushort>)ushort.TryParse;
            parserBuilder[typeof(int)] = (TryParseDelegate<int>)int.TryParse;
            parserBuilder[typeof(uint)] = (TryParseDelegate<uint>)uint.TryParse;
            parserBuilder[typeof(long)] = (TryParseDelegate<long>)long.TryParse;
            parserBuilder[typeof(ulong)] = (TryParseDelegate<ulong>)ulong.TryParse;
            parserBuilder[typeof(float)] = (TryParseDelegate<float>)float.TryParse;
            parserBuilder[typeof(double)] = (TryParseDelegate<double>)double.TryParse;
            parserBuilder[typeof(decimal)] = (TryParseDelegate<decimal>)decimal.TryParse;
            parserBuilder[typeof(DateTime)] = (TryParseDelegate<DateTime>)TryParseDate;
            parserBuilder[typeof(DateTimeOffset)] = (TryParseDelegate<DateTimeOffset>)DateTimeOffset.TryParse;
            //parserBuilder[typeof(TimeSpan)] = (TryParseDelegate<TimeSpan>)TimeSpan.TryParse;
            parserBuilder[typeof(char)] = (TryParseDelegate<char>)char.TryParse;
            return parserBuilder.ToImmutable();
        }

        public static TryParseDelegate<T> Get<T>() => (TryParseDelegate<T>)Parsers.Value[typeof(T)];
        public static Delegate Get(Type type) => Parsers.Value[type];

        public static bool TryParseDate(string s, out DateTime date)
        {
            date = default;

            string[] formats = { 
                // Basic formats
                "yyyyMMddTHHmmsszzz",
                "yyyyMMddTHHmmsszz",
                "yyyyMMddTHHmmssZ",
                // Extended formats
                "yyyy-MM-ddTHH:mm:sszzz",
                "yyyy-MM-ddTHH:mm:sszz",
                "yyyy-MM-ddTHH:mm:ssZ",
                // All of the above with reduced accuracy
                "yyyyMMddTHHmmzzz",
                "yyyyMMddTHHmmzz",
                "yyyyMMddTHHmmZ",
                "yyyy-MM-ddTHH:mmzzz",
                "yyyy-MM-ddTHH:mmzz",
                "yyyy-MM-ddTHH:mmZ",
                // Accuracy reduced to hours
                "yyyyMMddTHHzzz",
                "yyyyMMddTHHzz",
                "yyyyMMddTHHZ",
                "yyyy-MM-ddTHHzzz",
                "yyyy-MM-ddTHHzz",
                "yyyy-MM-ddTHHZ"
                };

            if (DateTime.TryParse(s, out date))
                return true;

            return DateTime.TryParseExact(s, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
        }
    }
}
