namespace Rakeeza.Core.Crypto;

/// <summary>سبب ثابت لفشل التشفير، تحوّله الطبقات العليا إلى رمز خطأ ورسالة عربية.</summary>
public enum CryptoError
{
    /// <summary>ملفّ المفتاح غير صالح: JSON معطوب، أو إصدار أو KDF غير مدعوم، أو أطوال خاطئة.</summary>
    InvalidKeyFile,

    /// <summary>عبارة المرور لا تفكّ المفتاح الرئيسي (أو المفتاح المغلَّف عُبث به؛ لا يمكن التمييز بينهما).</summary>
    WrongPassphrase,

    /// <summary>الملفّ ليس بصيغة RKZF المعروفة (التوقيع أو الإصدار أو حجم الكتلة).</summary>
    InvalidFileFormat,

    /// <summary>كتلة في ملفّ RKZF لم يتحقّق وسمها: الملفّ تالف أو عُبث به أو المفتاح خاطئ.</summary>
    CorruptedFile,
}

public sealed class CryptoException : Exception
{
    public CryptoException(CryptoError error, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Error = error;
    }

    public CryptoException()
        : this(CryptoError.InvalidKeyFile, "Crypto error.")
    {
    }

    public CryptoException(string message)
        : this(CryptoError.InvalidKeyFile, message)
    {
    }

    public CryptoException(string message, Exception innerException)
        : this(CryptoError.InvalidKeyFile, message, innerException)
    {
    }

    public CryptoError Error { get; }
}
