using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Rakeeza.Core.Crypto;

/// <summary>
/// ملفّ <c>rakeeza.key</c>: المفتاح الرئيسي مغلَّفًا بمفتاح مشتقّ من عبارة المرور.
/// <code>
/// { "version": 1, "kdf": "argon2id", "salt": b64, "ops": int, "mem": int, "nonce": b64, "key": b64 }
/// wrap   = crypto_pwhash(32, passphrase, salt, ops, mem, ARGON2ID13)
/// master = crypto_secretbox_open(key, nonce, wrap)
/// </code>
/// </summary>
public sealed class KeyFile
{
    public const int CurrentVersion = 1;
    public const string Argon2Id = "argon2id";

    /// <summary>معاملات Argon2id للملفّات الجديدة: ثوابت libsodium "MODERATE" (‏ops = 3، ‏mem = 256 MiB).</summary>
    public static readonly KdfParameters DefaultKdf = new(OpsLimit: 3, MemLimit: 268_435_456);

    private const int WrappedKeyBytes = Sodium.KeyBytes + Sodium.SecretBoxMacBytes;

    private static readonly JsonWriterOptions WriterOptions = new()
    {
        // base64 يحوي "+" و"/"؛ لا داعي لتهريبهما في ملفّ محلّي لا يُعرض في HTML.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly byte[] _salt;
    private readonly byte[] _nonce;
    private readonly byte[] _wrappedKey;

    private KeyFile(KdfParameters kdf, byte[] salt, byte[] nonce, byte[] wrappedKey)
    {
        Kdf = kdf;
        _salt = salt;
        _nonce = nonce;
        _wrappedKey = wrappedKey;
    }

    public KdfParameters Kdf { get; }

    public static KeyFile Read(string path) => Parse(File.ReadAllBytes(path));

    public static KeyFile Parse(ReadOnlySpan<byte> utf8Json)
    {
        try
        {
            using var doc = JsonDocument.Parse(utf8Json.ToArray());
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw Invalid("root is not an object");
            }

            if (Required(root, "version", JsonValueKind.Number).GetInt32() != CurrentVersion)
            {
                throw Invalid("unsupported version");
            }

            if (Required(root, "kdf", JsonValueKind.String).GetString() != Argon2Id)
            {
                throw Invalid("unsupported kdf");
            }

            var ops = Required(root, "ops", JsonValueKind.Number).GetUInt64();
            var mem = Required(root, "mem", JsonValueKind.Number).GetUInt64();
            if (ops == 0 || mem == 0 || mem > nuint.MaxValue)
            {
                throw Invalid("kdf limits out of range");
            }

            var salt = Base64(root, "salt", Sodium.PwHashSaltBytes);
            var nonce = Base64(root, "nonce", Sodium.SecretBoxNonceBytes);
            var key = Base64(root, "key", WrappedKeyBytes);
            return new KeyFile(new KdfParameters(ops, mem), salt, nonce, key);
        }
        catch (Exception ex) when (ex is JsonException or FormatException or InvalidOperationException)
        {
            throw new CryptoException(CryptoError.InvalidKeyFile, "rakeeza.key is malformed.", ex);
        }
    }

    /// <summary>يغلّف مفتاحًا رئيسيًّا بعبارة مرور جديدة (ملحٌ وnonce عشوائيّان).</summary>
    public static KeyFile Create(string passphrase, MasterKey master, KdfParameters? kdf = null)
    {
        var salt = new byte[Sodium.PwHashSaltBytes];
        var nonce = new byte[Sodium.SecretBoxNonceBytes];
        Sodium.RandomBytes(salt);
        Sodium.RandomBytes(nonce);
        return Create(passphrase, master, kdf ?? DefaultKdf, salt, nonce);
    }

    /// <summary>للاختبارات: ملح وnonce ثابتان لمقارنة الناتج بايتًا ببايت بملفّ PHP.</summary>
    internal static KeyFile Create(string passphrase, MasterKey master, KdfParameters parameters, byte[] salt, byte[] nonce)
    {
        ArgumentNullException.ThrowIfNull(master);
        ArgumentException.ThrowIfNullOrEmpty(passphrase);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentOutOfRangeException.ThrowIfNotEqual(salt.Length, Sodium.PwHashSaltBytes, nameof(salt));
        ArgumentOutOfRangeException.ThrowIfNotEqual(nonce.Length, Sodium.SecretBoxNonceBytes, nameof(nonce));

        Span<byte> wrap = stackalloc byte[Sodium.KeyBytes];
        try
        {
            DeriveWrappingKey(wrap, passphrase, salt, parameters);
            var wrapped = new byte[WrappedKeyBytes];
            Sodium.SecretBox(wrapped, master.Bytes, nonce, wrap);
            return new KeyFile(parameters, salt, nonce, wrapped);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(wrap);
        }
    }

    /// <summary>يفكّ المفتاح الرئيسي. عبارة خاطئة ⇒ <see cref="CryptoError.WrongPassphrase"/>.</summary>
    public MasterKey Unlock(string passphrase)
    {
        ArgumentNullException.ThrowIfNull(passphrase);
        Span<byte> wrap = stackalloc byte[Sodium.KeyBytes];
        Span<byte> master = stackalloc byte[Sodium.KeyBytes];
        try
        {
            DeriveWrappingKey(wrap, passphrase, _salt, Kdf);
            if (!Sodium.SecretBoxOpen(master, _wrappedKey, _nonce, wrap))
            {
                throw new CryptoException(CryptoError.WrongPassphrase, "Passphrase does not unlock rakeeza.key.");
            }

            return new MasterKey(master);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(wrap);
            CryptographicOperations.ZeroMemory(master);
        }
    }

    public byte[] ToJsonUtf8()
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
        {
            writer.WriteStartObject();
            writer.WriteNumber("version", CurrentVersion);
            writer.WriteString("kdf", Argon2Id);
            writer.WriteString("salt", Convert.ToBase64String(_salt));
            writer.WriteNumber("ops", Kdf.OpsLimit);
            writer.WriteNumber("mem", Kdf.MemLimit);
            writer.WriteString("nonce", Convert.ToBase64String(_nonce));
            writer.WriteString("key", Convert.ToBase64String(_wrappedKey));
            writer.WriteEndObject();
        }

        return buffer.ToArray();
    }

    /// <summary>
    /// كتابة ذرّية: إلى <c>path.new</c> مع تفريغه على القرص، ثمّ إعادة تسمية فوق الملفّ القديم.
    /// انقطاع الكهرباء في المنتصف يترك إمّا الملفّ القديم سليمًا وإمّا الجديد كاملًا.
    /// </summary>
    public void WriteAtomic(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        var temp = path + ".new";
        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            stream.Write(ToJsonUtf8());
            stream.Flush(flushToDisk: true);
        }

        File.Move(temp, path, overwrite: true);
    }

    private static void DeriveWrappingKey(Span<byte> output, string passphrase, ReadOnlySpan<byte> salt, KdfParameters kdf)
    {
        // عبارة المرور تُمرَّر بايتات UTF-8 كما هي، دون تطبيع Unicode (كما يفعل PHP).
        var passwordBytes = Encoding.UTF8.GetBytes(passphrase);
        try
        {
            if (!Sodium.PwHashArgon2Id(output, passwordBytes, salt, kdf.OpsLimit, (nuint)kdf.MemLimit))
            {
                throw new CryptoException(CryptoError.InvalidKeyFile, "Argon2id rejected the key file's limits.");
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
        }
    }

    private static JsonElement Required(JsonElement root, string name, JsonValueKind kind)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != kind)
        {
            throw Invalid($"'{name}' is missing or has the wrong type");
        }

        return value;
    }

    private static byte[] Base64(JsonElement root, string name, int expectedLength)
    {
        var bytes = Convert.FromBase64String(Required(root, name, JsonValueKind.String).GetString()!);
        if (bytes.Length != expectedLength)
        {
            throw Invalid($"'{name}' must decode to {expectedLength} bytes");
        }

        return bytes;
    }

    private static CryptoException Invalid(string reason) =>
        new(CryptoError.InvalidKeyFile, $"rakeeza.key is invalid: {reason}.");
}

/// <summary>معاملات Argon2id كما تُخزَّن في الملفّ: ‏ops عدد التمريرات، و‏mem بالبايت.</summary>
public sealed record KdfParameters(ulong OpsLimit, ulong MemLimit);
