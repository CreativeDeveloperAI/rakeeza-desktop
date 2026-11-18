namespace Rakeeza.Commands.System;

/// <summary>ما يعرّف به المضيف نفسه (DevHost أو Desktop). يطبّقه كلّ مضيف.</summary>
public interface IHostEnvironmentInfo
{
    HostKind Kind { get; }
}

public enum HostKind
{
    DevHost,
    Desktop,
}
