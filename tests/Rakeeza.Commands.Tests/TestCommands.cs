using Rakeeza.Commands.Bridge;

namespace Rakeeza.Commands.Tests;

public sealed record EchoRequest(string Text, int Count);

public sealed record EchoResponse(string Text);

[Command("test.echo")]
public sealed class EchoCommand : ICommand<EchoRequest, EchoResponse>
{
    public Task<EchoResponse> ExecuteAsync(EchoRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(new EchoResponse(string.Concat(Enumerable.Repeat(request.Text, request.Count))));
}

public sealed record EmptyRequest;

public sealed record EmptyResponse;

[Command("test.reject")]
public sealed class RejectCommand : ICommand<EmptyRequest, EmptyResponse>
{
    public Task<EmptyResponse> ExecuteAsync(EmptyRequest request, CancellationToken cancellationToken) =>
        throw new CommandException("test.rejected", new Dictionary<string, string> { ["field"] = "title" });
}

[Command("test.crash")]
public sealed class CrashCommand : ICommand<EmptyRequest, EmptyResponse>
{
    public const string SecretMessage = "connection string with secret";

    public Task<EmptyResponse> ExecuteAsync(EmptyRequest request, CancellationToken cancellationToken) =>
        throw new InvalidOperationException(SecretMessage);
}

[Command("test.wait")]
public sealed class WaitCommand : ICommand<EmptyRequest, EmptyResponse>
{
    public async Task<EmptyResponse> ExecuteAsync(EmptyRequest request, CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.Infinite, cancellationToken);
        return new EmptyResponse();
    }
}

// أشكال خاطئة تُفحص عبر CommandRegistry.Describe مباشرة، لا عبر مسح التجميعة.
[Command("test.no_interface")]
public sealed class AttributeWithoutInterface;

public sealed class UnnamedCommand : ICommand<EmptyRequest, EmptyResponse>
{
    public Task<EmptyResponse> ExecuteAsync(EmptyRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(new EmptyResponse());
}
