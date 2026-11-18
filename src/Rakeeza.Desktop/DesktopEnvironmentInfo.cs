using Rakeeza.Commands.System;

namespace Rakeeza.Desktop;

internal sealed class DesktopEnvironmentInfo : IHostEnvironmentInfo
{
    public HostKind Kind => HostKind.Desktop;
}
