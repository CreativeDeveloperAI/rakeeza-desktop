using Rakeeza.Commands.Bridge;

namespace Rakeeza.Commands.System;

public sealed record PingRequest(string Message);

public sealed record PingResponse(string Echo, HostKind Host, DateTimeOffset ServerTimeUtc);

/// <summary>أمر تشخيصي: يعيد الرسالة واسم المضيف ووقته. يثبت أنّ الجسر يعمل من الطرف إلى الطرف.</summary>
[Command("system.ping")]
public sealed class PingCommand : ICommand<PingRequest, PingResponse>
{
    private readonly IHostEnvironmentInfo _host;
    private readonly TimeProvider _time;

    public PingCommand(IHostEnvironmentInfo host, TimeProvider time)
    {
        _host = host;
        _time = time;
    }

    public Task<PingResponse> ExecuteAsync(PingRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Task.FromResult(new PingResponse(request.Message, _host.Kind, _time.GetUtcNow()));
    }
}
