using System.Collections.Frozen;
using System.Reflection;

namespace Rakeeza.Commands.Bridge;

/// <summary>يكتشف كلّ أوامر الجسر في التجميعات المعطاة ويتحقّق من سلامتها عند الإقلاع.</summary>
public sealed class CommandRegistry
{
    private readonly FrozenDictionary<string, CommandDescriptor> _byName;

    public CommandRegistry(IEnumerable<CommandDescriptor> descriptors)
    {
        var map = new Dictionary<string, CommandDescriptor>(StringComparer.Ordinal);
        foreach (var descriptor in descriptors)
        {
            if (!map.TryAdd(descriptor.Name, descriptor))
            {
                throw new InvalidOperationException(
                    $"Command '{descriptor.Name}' is declared by both {map[descriptor.Name].HandlerType} and {descriptor.HandlerType}.");
            }
        }

        _byName = map.ToFrozenDictionary(StringComparer.Ordinal);
    }

    public IEnumerable<CommandDescriptor> All => _byName.Values.OrderBy(d => d.Name, StringComparer.Ordinal);

    public bool TryGet(string name, out CommandDescriptor descriptor) => _byName.TryGetValue(name, out descriptor!);

    /// <summary>كلّ الأوامر في <c>Rakeeza.Commands</c>.</summary>
    public static CommandRegistry FromCommandsAssembly() => FromAssemblies(typeof(CommandRegistry).Assembly);

    public static CommandRegistry FromAssemblies(params Assembly[] assemblies) =>
        new(assemblies.SelectMany(a => a.GetTypes()).Select(Describe).OfType<CommandDescriptor>());

    internal static CommandDescriptor? Describe(Type type)
    {
        if (type is not { IsClass: true, IsAbstract: false })
        {
            return null;
        }

        var commandInterfaces = type.GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommand<,>))
            .ToList();
        var attribute = type.GetCustomAttribute<CommandAttribute>();

        if (commandInterfaces.Count == 0)
        {
            return attribute is null
                ? null
                : throw new InvalidOperationException($"{type} has [Command] but does not implement ICommand<,>.");
        }

        if (attribute is null)
        {
            throw new InvalidOperationException($"{type} implements ICommand<,> but has no [Command(\"name\")].");
        }

        if (commandInterfaces.Count > 1)
        {
            throw new InvalidOperationException($"{type} implements ICommand<,> more than once.");
        }

        var args = commandInterfaces[0].GetGenericArguments();
        return new CommandDescriptor(attribute.Name, args[0], args[1], type);
    }
}
