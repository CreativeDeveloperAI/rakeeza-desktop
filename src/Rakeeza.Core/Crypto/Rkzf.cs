using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Rakeeza.Core.Crypto;

/// <summary>
/// صيغة ملفّات المرفقات المشفّرة RKZF (الإصدار 1)، تُقرأ وتُكتب على أجزاء دون تحميل الملفّ كلّه.
/// <code>
/// header = "RKZF" 0x01 | chunk_size: uint32 big-endian (1048576) | file_nonce: 16 bytes   (25 bytes)
/// chunk i = crypto_aead_xchacha20poly1305_ietf_encrypt(plain_i, ad: "", nonce: file_nonce ‖ uint64le(i), key: media)
/// </code>
/// كلّ كتلة نصّها الأصلي <c>chunk_size</c> بايتًا إلّا الأخيرة، ويزيد كلّ نصّ مشفّر 16 بايتًا (الوسم).
/// </summary>
public static class Rkzf
{
    public const int HeaderLength = 25;
    public const int DefaultChunkSize = 1_048_576;
    public const byte Version = 1;

    /// <summary>حدّ أعلى دفاعي لحجم الكتلة المقروء من ملفّ، كي لا يطلب ملفّ تالف ذاكرة هائلة.</summary>
    public const int MaxChunkSize = 64 * 1_048_576;

    private const int FileNonceLength = 16;

    internal static ReadOnlySpan<byte> Magic => "RKZF"u8;

    /// <summary>هل تبدأ البيانات بترويسة RKZF من الإصدار 1؟ (تكفي أوّل 5 بايتات)</summary>
    public static bool HasSignature(ReadOnlySpan<byte> start) =>
        start.Length >= Magic.Length + 1 && start[..Magic.Length].SequenceEqual(Magic) && start[Magic.Length] == Version;

    public static async Task EncryptAsync(Stream plaintext, Stream ciphertext, MediaKey key, CancellationToken cancellationToken = default) =>
        await EncryptAsync(plaintext, ciphertext, key, DefaultChunkSize, fileNonce: null, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// للاختبارات فقط: حجم كتلة غير افتراضي، وnonce ثابت لمقارنة الناتج بايتًا ببايت بملفّات PHP.
    /// الملفّات الحقيقية تُكتب دائمًا بـ 1048576 وnonce عشوائي.
    /// </summary>
    internal static async Task EncryptAsync(Stream plaintext, Stream ciphertext, MediaKey key, int chunkSize, byte[]? fileNonce, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        ArgumentNullException.ThrowIfNull(ciphertext);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chunkSize);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(chunkSize, MaxChunkSize);

        var header = new byte[HeaderLength];
        Magic.CopyTo(header);
        header[4] = Version;
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(5, 4), (uint)chunkSize);
        if (fileNonce is null)
        {
            Sodium.RandomBytes(header.AsSpan(9, FileNonceLength));
        }
        else
        {
            ArgumentOutOfRangeException.ThrowIfNotEqual(fileNonce.Length, FileNonceLength, nameof(fileNonce));
            fileNonce.CopyTo(header, 9);
        }
        await ciphertext.WriteAsync(header, cancellationToken).ConfigureAwait(false);

        var plain = ArrayPool<byte>.Shared.Rent(chunkSize);
        var cipher = ArrayPool<byte>.Shared.Rent(chunkSize + Sodium.AeadTagBytes);
        var nonce = new byte[Sodium.AeadNonceBytes];
        header.AsSpan(9, FileNonceLength).CopyTo(nonce);
        try
        {
            for (ulong index = 0; ; index++)
            {
                var read = await ReadFullAsync(plaintext, plain.AsMemory(0, chunkSize), cancellationToken).ConfigureAwait(false);

                // لا كتلة فارغة في النهاية: ملفّ فارغ ⇒ ترويسة فقط، وملفّ بحجم مضاعف للكتلة ⇒ لا كتلة زائدة.
                if (read == 0)
                {
                    break;
                }

                BinaryPrimitives.WriteUInt64LittleEndian(nonce.AsSpan(FileNonceLength), index);
                var cipherLength = read + Sodium.AeadTagBytes;
                Sodium.AeadEncrypt(cipher.AsSpan(0, cipherLength), plain.AsSpan(0, read), nonce, key.Bytes);
                await ciphertext.WriteAsync(cipher.AsMemory(0, cipherLength), cancellationToken).ConfigureAwait(false);

                if (read < chunkSize)
                {
                    break;
                }
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
            ArrayPool<byte>.Shared.Return(plain);
            ArrayPool<byte>.Shared.Return(cipher);
        }
    }

    /// <summary>
    /// يفكّ ملفًّا ويكتب نصّه إلى <paramref name="plaintext"/> كتلةً كتلة، ولا تُكتب كتلة قبل التحقّق من وسمها.
    /// </summary>
    /// <remarks>
    /// قيد في الصيغة القديمة نفسها: لا علامة للكتلة الأخيرة، فحذف كتل كاملة من آخر الملفّ لا يُكتشف.
    /// </remarks>
    public static async Task DecryptAsync(Stream ciphertext, Stream plaintext, MediaKey key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ciphertext);
        ArgumentNullException.ThrowIfNull(plaintext);
        ArgumentNullException.ThrowIfNull(key);

        var header = new byte[HeaderLength];
        if (await ReadFullAsync(ciphertext, header, cancellationToken).ConfigureAwait(false) != HeaderLength || !HasSignature(header))
        {
            throw new CryptoException(CryptoError.InvalidFileFormat, "Not an RKZF v1 file.");
        }

        var chunkSize = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(5, 4));
        if (chunkSize == 0 || chunkSize > MaxChunkSize)
        {
            throw new CryptoException(CryptoError.InvalidFileFormat, $"Unsupported RKZF chunk size {chunkSize}.");
        }

        var cipherChunk = (int)chunkSize + Sodium.AeadTagBytes;
        var cipher = ArrayPool<byte>.Shared.Rent(cipherChunk);
        var plain = ArrayPool<byte>.Shared.Rent((int)chunkSize);
        var nonce = new byte[Sodium.AeadNonceBytes];
        header.AsSpan(9, FileNonceLength).CopyTo(nonce);
        try
        {
            for (ulong index = 0; ; index++)
            {
                var read = await ReadFullAsync(ciphertext, cipher.AsMemory(0, cipherChunk), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                if (read < Sodium.AeadTagBytes)
                {
                    throw new CryptoException(CryptoError.CorruptedFile, $"RKZF chunk {index} is truncated.");
                }

                BinaryPrimitives.WriteUInt64LittleEndian(nonce.AsSpan(FileNonceLength), index);
                var plainLength = read - Sodium.AeadTagBytes;
                if (!Sodium.AeadDecrypt(plain.AsSpan(0, plainLength), cipher.AsSpan(0, read), nonce, key.Bytes))
                {
                    throw new CryptoException(CryptoError.CorruptedFile, $"RKZF chunk {index} failed authentication.");
                }

                await plaintext.WriteAsync(plain.AsMemory(0, plainLength), cancellationToken).ConfigureAwait(false);

                if (read < cipherChunk)
                {
                    break;
                }
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
            ArrayPool<byte>.Shared.Return(plain);
            ArrayPool<byte>.Shared.Return(cipher);
        }
    }

    /// <summary>يقرأ حتى يمتلئ المخزن أو ينتهي التدفّق، لأنّ ReadAsync قد يعيد أقلّ من المطلوب.</summary>
    private static async Task<int> ReadFullAsync(Stream stream, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer[total..], cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return total;
    }
}
