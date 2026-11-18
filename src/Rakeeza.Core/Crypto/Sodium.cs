using System.Runtime.InteropServices;

namespace Rakeeza.Core.Crypto;

/// <summary>
/// ربط مباشر وضيّق بدوالّ libsodium التي يستعملها النظام القديم عبر امتداد sodium في PHP.
/// لا تغليف إضافيًّا: الدوالّ هنا هي البدائيّات نفسها، والصيغ تُبنى فوقها في هذا المجلّد.
/// </summary>
internal static unsafe partial class Sodium
{
    private const string Library = "libsodium";

    public const int KeyBytes = 32;
    public const int PwHashSaltBytes = 16;
    public const int SecretBoxNonceBytes = 24;
    public const int SecretBoxMacBytes = 16;
    public const int AeadNonceBytes = 24;
    public const int AeadTagBytes = 16;
    public const int PwHashAlgArgon2Id13 = 2;

    private static readonly Lazy<bool> Initialized = new(() =>
    {
        // 0 = هُيّئت الآن، 1 = مُهيّأة مسبقًا، -1 = فشل.
        if (sodium_init() < 0)
        {
            throw new InvalidOperationException("libsodium failed to initialize.");
        }

        return true;
    });

    public static void EnsureInitialized() => _ = Initialized.Value;

    public static void RandomBytes(Span<byte> buffer)
    {
        EnsureInitialized();
        fixed (byte* p = buffer)
        {
            randombytes_buf(p, (nuint)buffer.Length);
        }
    }

    /// <summary>crypto_pwhash بخوارزمية Argon2id13. تعيد false إن رفضت libsodium المعاملات أو نفدت الذاكرة.</summary>
    public static bool PwHashArgon2Id(Span<byte> output, ReadOnlySpan<byte> password, ReadOnlySpan<byte> salt, ulong opsLimit, nuint memLimit)
    {
        EnsureInitialized();
        if (salt.Length != PwHashSaltBytes)
        {
            throw new ArgumentException("Salt must be 16 bytes.", nameof(salt));
        }

        fixed (byte* o = output)
        fixed (byte* pw = password)
        fixed (byte* s = salt)
        {
            return crypto_pwhash(o, (ulong)output.Length, pw, (ulong)password.Length, s, opsLimit, memLimit, PwHashAlgArgon2Id13) == 0;
        }
    }

    /// <summary>crypto_secretbox_easy: الناتج = MAC (16) ثمّ النصّ المشفّر، كما يخرجه sodium_crypto_secretbox.</summary>
    public static void SecretBox(Span<byte> cipher, ReadOnlySpan<byte> message, ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> key)
    {
        EnsureInitialized();
        CheckLength(cipher, message.Length + SecretBoxMacBytes, nameof(cipher));
        CheckLength(nonce, SecretBoxNonceBytes, nameof(nonce));
        CheckLength(key, KeyBytes, nameof(key));
        fixed (byte* c = cipher)
        fixed (byte* m = message)
        fixed (byte* n = nonce)
        fixed (byte* k = key)
        {
            if (crypto_secretbox_easy(c, m, (ulong)message.Length, n, k) != 0)
            {
                throw new InvalidOperationException("crypto_secretbox_easy failed.");
            }
        }
    }

    /// <summary>crypto_secretbox_open_easy. تعيد false إن لم يتحقّق الـMAC (مفتاح خاطئ أو بيانات تالفة).</summary>
    public static bool SecretBoxOpen(Span<byte> message, ReadOnlySpan<byte> cipher, ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> key)
    {
        EnsureInitialized();
        CheckLength(message, cipher.Length - SecretBoxMacBytes, nameof(message));
        CheckLength(nonce, SecretBoxNonceBytes, nameof(nonce));
        CheckLength(key, KeyBytes, nameof(key));
        fixed (byte* m = message)
        fixed (byte* c = cipher)
        fixed (byte* n = nonce)
        fixed (byte* k = key)
        {
            return crypto_secretbox_open_easy(m, c, (ulong)cipher.Length, n, k) == 0;
        }
    }

    /// <summary>crypto_generichash (BLAKE2b) بمفتاح، كما في sodium_crypto_generichash($message, $key, $length).</summary>
    public static void GenericHash(Span<byte> output, ReadOnlySpan<byte> message, ReadOnlySpan<byte> key)
    {
        EnsureInitialized();
        fixed (byte* o = output)
        fixed (byte* m = message)
        fixed (byte* k = key)
        {
            if (crypto_generichash(o, (nuint)output.Length, m, (ulong)message.Length, k, (nuint)key.Length) != 0)
            {
                throw new InvalidOperationException("crypto_generichash failed.");
            }
        }
    }

    /// <summary>crypto_aead_xchacha20poly1305_ietf_encrypt بلا بيانات مرافقة (ad = "").</summary>
    public static void AeadEncrypt(Span<byte> cipher, ReadOnlySpan<byte> message, ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> key)
    {
        EnsureInitialized();
        CheckLength(cipher, message.Length + AeadTagBytes, nameof(cipher));
        CheckLength(nonce, AeadNonceBytes, nameof(nonce));
        CheckLength(key, KeyBytes, nameof(key));
        fixed (byte* c = cipher)
        fixed (byte* m = message)
        fixed (byte* n = nonce)
        fixed (byte* k = key)
        {
            ulong written;
            if (crypto_aead_xchacha20poly1305_ietf_encrypt(c, &written, m, (ulong)message.Length, null, 0, null, n, k) != 0
                || written != (ulong)cipher.Length)
            {
                throw new InvalidOperationException("crypto_aead_xchacha20poly1305_ietf_encrypt failed.");
            }
        }
    }

    /// <summary>فكّ كتلة AEAD. تعيد false إن لم يتحقّق الوسم.</summary>
    public static bool AeadDecrypt(Span<byte> message, ReadOnlySpan<byte> cipher, ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> key)
    {
        EnsureInitialized();
        CheckLength(message, cipher.Length - AeadTagBytes, nameof(message));
        CheckLength(nonce, AeadNonceBytes, nameof(nonce));
        CheckLength(key, KeyBytes, nameof(key));
        fixed (byte* m = message)
        fixed (byte* c = cipher)
        fixed (byte* n = nonce)
        fixed (byte* k = key)
        {
            ulong written;
            return crypto_aead_xchacha20poly1305_ietf_decrypt(m, &written, null, c, (ulong)cipher.Length, null, 0, n, k) == 0
                && written == (ulong)message.Length;
        }
    }

    private static void CheckLength(ReadOnlySpan<byte> span, int expected, string name)
    {
        if (span.Length != expected)
        {
            throw new ArgumentException($"Expected {expected} bytes, got {span.Length}.", name);
        }
    }

    [LibraryImport(Library)]
    private static partial int sodium_init();

    [LibraryImport(Library)]
    private static partial void randombytes_buf(byte* buf, nuint size);

    [LibraryImport(Library)]
    private static partial int crypto_pwhash(byte* output, ulong outlen, byte* passwd, ulong passwdlen, byte* salt, ulong opslimit, nuint memlimit, int alg);

    [LibraryImport(Library)]
    private static partial int crypto_secretbox_easy(byte* c, byte* m, ulong mlen, byte* n, byte* k);

    [LibraryImport(Library)]
    private static partial int crypto_secretbox_open_easy(byte* m, byte* c, ulong clen, byte* n, byte* k);

    [LibraryImport(Library)]
    private static partial int crypto_generichash(byte* output, nuint outlen, byte* input, ulong inlen, byte* key, nuint keylen);

    [LibraryImport(Library)]
    private static partial int crypto_aead_xchacha20poly1305_ietf_encrypt(byte* c, ulong* clen, byte* m, ulong mlen, byte* ad, ulong adlen, byte* nsec, byte* npub, byte* k);

    [LibraryImport(Library)]
    private static partial int crypto_aead_xchacha20poly1305_ietf_decrypt(byte* m, ulong* mlen, byte* nsec, byte* c, ulong clen, byte* ad, ulong adlen, byte* npub, byte* k);
}
