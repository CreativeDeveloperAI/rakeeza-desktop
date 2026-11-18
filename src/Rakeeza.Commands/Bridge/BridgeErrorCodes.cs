namespace Rakeeza.Commands.Bridge;

/// <summary>
/// رموز الأخطاء الثابتة التي تعبر الجسر. الواجهة تترجمها إلى رسائل عربية من ملفّ النصوص المركزي،
/// ولا تعبر الجسرَ أيّ رسالة استثناء داخلية.
/// </summary>
public static class BridgeErrorCodes
{
    public const string InvalidEnvelope = "bridge.invalid_envelope";
    public const string UnknownCommand = "bridge.unknown_command";
    public const string InvalidPayload = "bridge.invalid_payload";
    public const string Cancelled = "bridge.cancelled";
    public const string Internal = "internal";
}
