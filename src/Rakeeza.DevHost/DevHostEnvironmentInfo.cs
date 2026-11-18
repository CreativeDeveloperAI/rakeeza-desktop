using Rakeeza.Commands.System;

namespace Rakeeza.DevHost;

internal sealed class DevHostEnvironmentInfo : IHostEnvironmentInfo
{
    public HostKind Kind => HostKind.DevHost;
}
