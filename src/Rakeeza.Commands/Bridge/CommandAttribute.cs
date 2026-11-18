using System.Text.RegularExpressions;

namespace Rakeeza.Commands.Bridge;

/// <summary>الاسم الثابت للأمر كما تستدعيه الواجهة، بصيغة <c>group.action</c> أو <c>group.resource.action</c>.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed partial class CommandAttribute : Attribute
{
    public CommandAttribute(string name)
    {
        if (!NamePattern().IsMatch(name))
        {
            throw new ArgumentException($"Invalid command name '{name}'. Expected lower-case dotted segments.", nameof(name));
        }

        Name = name;
    }

    public string Name { get; }

    [GeneratedRegex("^[a-z][a-z0-9_]*(\\.[a-z][a-z0-9_]*)+$")]
    private static partial Regex NamePattern();
}
