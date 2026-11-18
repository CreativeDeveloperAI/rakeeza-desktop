using Rakeeza.Commands.Bridge;
using Rakeeza.Commands.System;

namespace Rakeeza.Commands.Tests;

public sealed class CommandRegistryTests
{
    [Fact]
    public void Discovers_commands_in_the_commands_assembly()
    {
        var registry = CommandRegistry.FromCommandsAssembly();

        Assert.True(registry.TryGet("system.ping", out var ping));
        Assert.Equal(typeof(PingRequest), ping.RequestType);
        Assert.Equal(typeof(PingResponse), ping.ResponseType);
        Assert.Equal(typeof(PingCommand), ping.HandlerType);
    }

    [Fact]
    public void Rejects_duplicate_command_names()
    {
        var a = new CommandDescriptor("test.same", typeof(EmptyRequest), typeof(EmptyResponse), typeof(RejectCommand));
        var b = a with { HandlerType = typeof(CrashCommand) };

        var ex = Assert.Throws<InvalidOperationException>(() => new CommandRegistry([a, b]));
        Assert.Contains("test.same", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_attribute_without_interface() =>
        Assert.Throws<InvalidOperationException>(() => CommandRegistry.Describe(typeof(AttributeWithoutInterface)));

    [Fact]
    public void Rejects_interface_without_attribute() =>
        Assert.Throws<InvalidOperationException>(() => CommandRegistry.Describe(typeof(UnnamedCommand)));

    [Fact]
    public void Ignores_unrelated_types() => Assert.Null(CommandRegistry.Describe(typeof(EchoRequest)));

    [Theory]
    [InlineData("ping")]
    [InlineData("System.Ping")]
    [InlineData("system.")]
    [InlineData("system..ping")]
    [InlineData("system.ping-now")]
    public void Rejects_malformed_names(string name) =>
        Assert.Throws<ArgumentException>(() => new CommandAttribute(name));

    [Theory]
    [InlineData("system.ping")]
    [InlineData("archive.documents.create")]
    [InlineData("archive.confidentiality.top_secret")]
    public void Accepts_dotted_lower_case_names(string name) => Assert.Equal(name, new CommandAttribute(name).Name);
}
