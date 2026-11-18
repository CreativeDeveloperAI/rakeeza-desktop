using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Rakeeza.Commands.Bridge;
using Rakeeza.Commands.System;

namespace Rakeeza.Commands.Tests;

public sealed class CommandDispatcherTests
{
    private static CommandDispatcher CreateDispatcher()
    {
        Type[] handlers = [typeof(EchoCommand), typeof(RejectCommand), typeof(CrashCommand), typeof(WaitCommand), typeof(PingCommand)];
        var registry = new CommandRegistry(handlers.Select(t => CommandRegistry.Describe(t)!));
        var services = new ServiceCollection();
        foreach (var descriptor in registry.All)
        {
            services.AddScoped(descriptor.HandlerType);
        }

        services.AddSingleton<IHostEnvironmentInfo>(new FixedHost());
        services.AddSingleton<TimeProvider>(new FixedTime());
        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        return new CommandDispatcher(registry, provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<CommandDispatcher>.Instance);
    }

    private static Task<BridgeResponse> Dispatch(string json, CancellationToken ct = default) =>
        CreateDispatcher().DispatchJsonAsync(json, ct);

    [Fact]
    public async Task Executes_command_and_serialises_result_in_camel_case()
    {
        var response = await Dispatch("""{"id":"1","command":"test.echo","payload":{"text":"ab","count":2}}""");

        Assert.True(response.Ok);
        Assert.Equal("1", response.Id);
        Assert.Null(response.Error);
        Assert.Equal("abab", response.Result!.Value.GetProperty("text").GetString());
    }

    [Fact]
    public async Task Ping_reports_host_and_utc_time()
    {
        var response = await Dispatch("""{"id":"p","command":"system.ping","payload":{"message":"مرحبا"}}""");

        var result = response.Result!.Value;
        Assert.Equal("مرحبا", result.GetProperty("echo").GetString());
        Assert.Equal("devHost", result.GetProperty("host").GetString());
        Assert.Equal(FixedTime.Now, result.GetProperty("serverTimeUtc").GetDateTimeOffset());
    }

    [Fact]
    public async Task Unknown_command_returns_stable_code()
    {
        var response = await Dispatch("""{"id":"2","command":"nope.nothing","payload":{}}""");

        AssertFailure(response, "2", BridgeErrorCodes.UnknownCommand);
    }

    [Theory]
    [InlineData("""{"text":"a"}""")] // حقل مطلوب مفقود
    [InlineData("""{"text":"a","count":"2"}""")] // نوع خاطئ
    [InlineData("""{"text":null,"count":1}""")] // null في حقل غير قابل لـnull
    [InlineData("""{"text":"a","count":1,"extra":true}""")] // حقل غير معروف
    [InlineData("null")]
    public async Task Invalid_payload_is_rejected(string payload)
    {
        var response = await Dispatch($$"""{"id":"3","command":"test.echo","payload":{{payload}}}""");

        AssertFailure(response, "3", BridgeErrorCodes.InvalidPayload);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"command":"test.echo","payload":{}}""")]
    [InlineData("""{"id":"4","payload":{}}""")]
    [InlineData("""{"id":"","command":"test.echo","payload":{}}""")]
    public async Task Invalid_envelope_is_rejected(string json)
    {
        var response = await Dispatch(json);

        Assert.False(response.Ok);
        Assert.Equal(BridgeErrorCodes.InvalidEnvelope, response.Error!.Code);
    }

    [Fact]
    public async Task Command_exception_passes_code_and_details()
    {
        var response = await Dispatch("""{"id":"5","command":"test.reject","payload":{}}""");

        AssertFailure(response, "5", "test.rejected");
        Assert.Equal("title", response.Error!.Details!["field"]);
    }

    [Fact]
    public async Task Unhandled_exception_becomes_internal_without_leaking_its_message()
    {
        var response = await Dispatch("""{"id":"6","command":"test.crash","payload":{}}""");

        AssertFailure(response, "6", BridgeErrorCodes.Internal);
        var json = JsonSerializer.Serialize(response, BridgeJson.Options);
        Assert.DoesNotContain(CrashCommand.SecretMessage, json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cancellation_returns_cancelled()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        var response = await Dispatch("""{"id":"7","command":"test.wait","payload":{}}""", cts.Token);

        AssertFailure(response, "7", BridgeErrorCodes.Cancelled);
    }

    [Fact]
    public async Task Response_envelope_has_the_shape_the_ui_expects()
    {
        var response = await Dispatch("""{"id":"8","command":"nope.nothing","payload":{}}""");

        var json = JsonSerializer.Serialize(response, BridgeJson.Options);
        Assert.Equal("""{"id":"8","ok":false,"result":null,"error":{"code":"bridge.unknown_command","details":null}}""", json);
    }

    private static void AssertFailure(BridgeResponse response, string id, string code)
    {
        Assert.False(response.Ok);
        Assert.Equal(id, response.Id);
        Assert.Null(response.Result);
        Assert.Equal(code, response.Error!.Code);
    }

    private sealed class FixedHost : IHostEnvironmentInfo
    {
        public HostKind Kind => HostKind.DevHost;
    }

    private sealed class FixedTime : TimeProvider
    {
        public static readonly DateTimeOffset Now = new(2026, 11, 18, 12, 30, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
