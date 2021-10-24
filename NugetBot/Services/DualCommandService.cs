using Discord;
using Discord.Commands;
using Discord.Commands.Builders;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NugetBot
{
    public interface ParserErrorHandler
    {
        Task ExecuteAsync(DualCommandContext context, Discord.Commands.ParameterInfo paramType, object input);
    }

    public class ParseErrorHandlerAttribute : Attribute
    {
        internal readonly ParserErrorHandler Handler;

        public ParseErrorHandlerAttribute(Type handler)
        {
            if (!handler.IsAssignableTo(typeof(ParserErrorHandler)))
                throw new ArgumentException();

            Handler = (ParserErrorHandler)Activator.CreateInstance(handler);
        }
    }

    public class DualCommandService
    {
        public event Func<LogMessage, Task> Log
        {
            add => underlyingService.Log += value;
            remove => underlyingService.Log -= value;
        }
        public event Func<Optional<CommandInfo>, ICommandContext, IResult, Task> CommandExecuted
        {
            add => underlyingService.CommandExecuted += value;
            remove => underlyingService.CommandExecuted -= value;
        }

        private List<ModuleInfo> CustomModules = new List<ModuleInfo>();

        public ILookup<Type, TypeReader> TypeReaders
            => underlyingService.TypeReaders;

        private CommandService underlyingService;
        private static readonly TypeInfo ModuleTypeInfo = typeof(DualModuleBase).GetTypeInfo();
        //private static const TypeInfo BaseModuleTypeInfo = typeof(ModuleBase<SocketCommandContext>).GetTypeInfo();
        private readonly SemaphoreSlim _moduleLock;
        private CommandServiceConfig Config;
        private Logger _log;

        public DualCommandService()
            : this(new CommandServiceConfig())
        {
           
        }

        public bool TryGetTypeReader<TType>(out TypeReader reader)
            => TryGetTypeReader(typeof(TType), out reader);

        public bool TryGetTypeReader(Type type, out TypeReader reader)
        {
            reader = null;

            if (type.Name == "Nullable`1")
                type = type.GenericTypeArguments[0];

            if (TypeReaders.Contains(type))
            {
                reader = TypeReaders[type].FirstOrDefault();
                return true;
            }

            return false;
        }

        public void AddTypeReader<T>(TypeReader reader)
            => AddTypeReader(typeof(T), reader);

        public void AddTypeReader(Type type, TypeReader reader)
            => underlyingService.AddTypeReader(type, reader);

        public DualCommandService(CommandServiceConfig conf)
        {
            _log = Logger.GetLogger<DualCommandService>();
            conf.IgnoreExtraArgs = true;
            _moduleLock = new SemaphoreSlim(1, 1);
            underlyingService = new CommandService(conf);
            this.Config = conf;
        }
        public Task<IResult> ExecuteAsync(ICommandContext context, int argPos, IServiceProvider services, MultiMatchHandling multiMatchHandling = MultiMatchHandling.Exception)
            => ExecuteAsync(context, context.Message.Content.Substring(argPos), services, multiMatchHandling);

        public Task<IResult> ExecuteAsync(ICommandContext context, string input, IServiceProvider services, MultiMatchHandling multiMatchHandling = MultiMatchHandling.Exception)
            => underlyingService.ExecuteAsync(context, input, services, multiMatchHandling);

        public async Task RegisterModulesAsync(Assembly assembly, IServiceProvider services)
        {
            await _moduleLock.WaitAsync().ConfigureAwait(false);

            var types = Search(assembly);
            await BuildAsync(types, services).ConfigureAwait(false);
        }

        private async Task<Dictionary<Type, ModuleInfo>> BuildAsync(IEnumerable<TypeInfo> validTypes, IServiceProvider services)
        {
            var topLevelGroups = validTypes.Where(x => x.DeclaringType == null || !IsValidModuleDefinition(x.DeclaringType.GetTypeInfo()));

            var result = new Dictionary<Type, ModuleInfo>();

            foreach (var typeInfo in topLevelGroups)
            {
                // TODO: This shouldn't be the case; may be safe to remove?
                if (result.ContainsKey(typeInfo.AsType()))
                    continue;

                ModuleInfo module = null;

                if (ModuleTypeInfo.IsAssignableFrom(typeInfo))
                {
                    module = await underlyingService.CreateModuleAsync("", (x) => BuildModule(x, typeInfo, services));
                    CustomModules.Add(module);
                }
                else
                {
                    module = await underlyingService.AddModuleAsync(typeInfo, services).ConfigureAwait(false);
                }

                result.TryAdd(typeInfo.AsType(), module);
            }

            _log.Debug($"Successfully built {result.Count} modules.", Severity.CommandService);

            return result;
        }

        private IReadOnlyList<TypeInfo> Search(Assembly assembly)
        {
            bool IsLoadableModule(TypeInfo info)
            {
                return info.DeclaredMethods.Any(x => x.GetCustomAttribute<CommandAttribute>() != null) &&
                    info.GetCustomAttribute<DontAutoLoadAttribute>() == null;
            }

            var result = new List<TypeInfo>();

            foreach (var typeInfo in assembly.DefinedTypes)
            {
                if (typeInfo.IsPublic || typeInfo.IsNestedPublic)
                {
                    if (IsValidModuleDefinition(typeInfo) &&
                        !typeInfo.IsDefined(typeof(DontAutoLoadAttribute)))
                    {
                        result.Add(typeInfo);
                    }
                }
                else if (IsLoadableModule(typeInfo))
                {
                    _log.Warn($"Class {typeInfo.FullName} is not public and cannot be loaded. To suppress this message, mark the class with {nameof(DontAutoLoadAttribute)}.");
                }
            }

            return result;
        }

        private static bool IsValidModuleDefinition(TypeInfo typeInfo)
        {
            return (ModuleTypeInfo.IsAssignableFrom(typeInfo) || IsSubclassOfRawGeneric(typeof(ModuleBase<>), typeInfo.AsType())) &&
                   !typeInfo.IsAbstract &&
                   !typeInfo.ContainsGenericParameters;
        }
        private static bool IsValidCommandDefinition(MethodInfo methodInfo)
        {
            return methodInfo.IsDefined(typeof(CommandAttribute)) &&
                   (methodInfo.ReturnType == typeof(Task) || methodInfo.ReturnType == typeof(Task<RuntimeResult>)) &&
                   !methodInfo.IsStatic &&
                   !methodInfo.IsGenericMethod;
        }

        private static bool IsSubclassOfRawGeneric(Type generic, Type toCheck)
        {
            while (toCheck != null && toCheck != typeof(object))
            {
                var cur = toCheck.IsGenericType ? toCheck.GetGenericTypeDefinition() : toCheck;
                if (generic == cur)
                {
                    return true;
                }
                toCheck = toCheck.BaseType;
            }
            return false;
        }

        private void BuildModule(ModuleBuilder builder, TypeInfo typeInfo, IServiceProvider services)
        {
            var attributes = typeInfo.GetCustomAttributes();

            foreach (var attribute in attributes)
            {
                switch (attribute)
                {
                    case NameAttribute name:
                        builder.Name = name.Text;
                        break;
                    case SummaryAttribute summary:
                        builder.Summary = summary.Text;
                        break;
                    case RemarksAttribute remarks:
                        builder.Remarks = remarks.Text;
                        break;
                    case AliasAttribute alias:
                        builder.AddAliases(alias.Aliases);
                        break;
                    case GroupAttribute group:
                        builder.Name = builder.Name ?? group.Prefix;
                        builder.Group = group.Prefix;
                        builder.AddAliases(group.Prefix);
                        break;
                    case PreconditionAttribute precondition:
                        builder.AddPrecondition(precondition);
                        break;
                    default:
                        builder.AddAttributes(attribute);
                        break;
                }
            }

            //Check for unspecified info
            if (builder.Aliases.Count == 0)
                builder.AddAliases("");
            if (builder.Name == null)
                builder.Name = typeInfo.Name;

            // Get all methods (including from inherited members), that are valid commands
            var validCommands = typeInfo.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Where(IsValidCommandDefinition);

            foreach (var method in validCommands)
            {
                var name = method.GetCustomAttribute<CommandAttribute>();

                var createInstance = ReflectionUtils.CreateBuilder<DualModuleBase>(typeInfo);

                async Task<IResult> ExecuteCallback(ICommandContext context, object[] args, IServiceProvider services, CommandInfo cmd)
                {
                    var instance = createInstance(services);
                    instance.SetContext(context);
                    args = await instance.CreateInteractionArgsAsync(cmd, this, args);

                    if (args == null)
                        return ParseResult.FromError(CommandError.ParseFailed, "Input was not in the correct format");

                    try
                    {
                        await instance.BeforeExecuteAsync().ConfigureAwait(false);

                        var task = method.Invoke(instance, args) as Task ?? Task.Delay(0);
                        if (task is Task<RuntimeResult> resultTask)
                        {
                            return await resultTask.ConfigureAwait(false);
                        }
                        else
                        {
                            await task.ConfigureAwait(false);
                            return ExecuteResult.FromSuccess();
                        }
                    }
                    finally
                    {
                        (instance as IDisposable)?.Dispose();
                    }
                }

                builder.AddCommand(name.Text, ExecuteCallback, (command) =>
                {
                    BuildCommand(command, method, services);
                });
            }
        }

        private void BuildCommand(CommandBuilder builder, MethodInfo method, IServiceProvider serviceprovider)
        {
            var attributes = method.GetCustomAttributes();

            foreach (var attribute in attributes)
            {
                switch (attribute)
                {
                    case CommandAttribute command:
                        builder.AddAliases(command.Text);
                        builder.RunMode = command.RunMode;
                        builder.Name = builder.Name ?? command.Text;
                        builder.IgnoreExtraArgs = command.IgnoreExtraArgs ?? Config.IgnoreExtraArgs;
                        break;
                    case NameAttribute name:
                        builder.Name = name.Text;
                        break;
                    case PriorityAttribute priority:
                        builder.Priority = priority.Priority;
                        break;
                    case SummaryAttribute summary:
                        builder.Summary = summary.Text;
                        break;
                    case RemarksAttribute remarks:
                        builder.Remarks = remarks.Text;
                        break;
                    case AliasAttribute alias:
                        builder.AddAliases(alias.Aliases);
                        break;
                    case PreconditionAttribute precondition:
                        builder.AddPrecondition(precondition);
                        break;
                    default:
                        builder.AddAttributes(attribute);
                        break;
                }
            }

            if (builder.Name == null)
                builder.Name = method.Name;

            var parameters = method.GetParameters();
            int pos = 0, count = parameters.Length;
            foreach (var paramInfo in parameters)
            {
                builder.AddParameter(paramInfo.Name, paramInfo.ParameterType, (parameter) =>
                {
                    BuildParameter(parameter, paramInfo, pos++, count, serviceprovider);
                });
            }
        }

        private static void BuildParameter(ParameterBuilder builder, System.Reflection.ParameterInfo paramInfo, int position, int count, IServiceProvider services)
        {
            var attributes = paramInfo.GetCustomAttributes();
            var paramType = paramInfo.ParameterType;

            builder.IsOptional = true;
            builder.DefaultValue = paramInfo.HasDefaultValue ? paramInfo.DefaultValue : null;

            foreach (var attribute in attributes)
            {
                switch (attribute)
                {
                    case SummaryAttribute summary:
                        builder.Summary = summary.Text;
                        break;
                    case ParamArrayAttribute _:
                        builder.IsMultiple = true;
                        paramType = paramType.GetElementType();
                        break;
                    case ParameterPreconditionAttribute precon:
                        builder.AddPrecondition(precon);
                        break;
                    case RemainderAttribute _:
                        if (position != count - 1)
                            throw new InvalidOperationException($"Remainder parameters must be the last parameter in a command. Parameter: {paramInfo.Name} in {paramInfo.Member.DeclaringType.Name}.{paramInfo.Member.Name}");

                        builder.IsRemainder = true;
                        break;
                    default:
                        builder.AddAttributes(attribute);
                        break;
                }
            }
        }
    }
}
