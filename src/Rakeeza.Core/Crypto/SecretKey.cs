using System.Security.Cryptography;

namespace Rakeeza.Core.Crypto;

/// <summary>مفتاح متماثل من 32 بايتًا يُصفَّر عند التخلّص منه.</summary>
public abstract class SecretKey : IDisposable
{
    private readonly byte[] _bytes;
    private bool _disposed;

    private protected SecretKey(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != Sodium.KeyBytes)
        {
            throw new ArgumentException("Key must be 32 bytes.", nameof(bytes));
        }

        _bytes = bytes.ToArray();
    }

    internal ReadOnlySpan<byte> Bytes
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _bytes;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            CryptographicOperations.ZeroMemory(_bytes);
            _disposed = true;
        }

        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// المفتاح الرئيسي للخزنة: يفتح القاعدة (SQLite3MC) ويُشتقّ منه مفتاح المرفقات.
/// </summary>
public sealed class MasterKey : SecretKey
{
    /// <summary>رسالة الاشتقاق كما في النظام القديم.</summary>
    internal static ReadOnlySpan<byte> MediaKeyContext => "rakeeza-media"u8;

    public MasterKey(ReadOnlySpan<byte> bytes)
        : base(bytes)
    {
    }

    public static MasterKey Generate()
    {
        Span<byte> bytes = stackalloc byte[Sodium.KeyBytes];
        try
        {
            Sodium.RandomBytes(bytes);
            return new MasterKey(bytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    /// <summary>مفتاح المرفقات = crypto_generichash("rakeeza-media", key: master, outlen: 32).</summary>
    public MediaKey DeriveMediaKey()
    {
        Span<byte> derived = stackalloc byte[Sodium.KeyBytes];
        try
        {
            Sodium.GenericHash(derived, MediaKeyContext, Bytes);
            return new MediaKey(derived);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(derived);
        }
    }

    /// <summary>صيغة المفتاح لـ <c>PRAGMA key = "x'&lt;hex&gt;'"</c> في SQLite3MC (hex بأحرف صغيرة).</summary>
    public string ToSqlCipherHex() => Convert.ToHexStringLower(Bytes);
}

/// <summary>مفتاح تشفير ملفّات المرفقات (RKZF).</summary>
public sealed class MediaKey : SecretKey
{
    public MediaKey(ReadOnlySpan<byte> bytes)
        : base(bytes)
    {
    }
}
