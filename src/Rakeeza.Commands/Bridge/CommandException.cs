namespace Rakeeza.Commands.Bridge;

/// <summary>
/// رفض متوقَّع يُعاد إلى الواجهة برمزه. <see cref="Code"/> مفتاح ثابت (مثل <c>auth.invalid_credentials</c>)،
/// وليس نصًّا للعرض.
/// </summary>
public sealed class CommandException : Exception
{
    public CommandException(string code)
        : base(code)
    {
        Code = code;
    }

    public CommandException(string code, IReadOnlyDictionary<string, string> details)
        : base(code)
    {
        Code = code;
        Details = details;
    }

    public CommandException()
        : this(BridgeErrorCodes.Internal)
    {
    }

    public CommandException(string code, Exception innerException)
        : base(code, innerException)
    {
        Code = code;
    }

    public string Code { get; }

    /// <summary>قيم تُستبدل في الرسالة العربية (مثل اسم الحقل). لا تحتوي بيانات حسّاسة.</summary>
    public IReadOnlyDictionary<string, string>? Details { get; }
}
