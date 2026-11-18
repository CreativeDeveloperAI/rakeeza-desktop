using System.Buffers.Binary;
using System.Security.Cryptography;
using Rakeeza.Core.Crypto;

namespace Rakeeza.Core.Tests.Crypto;

public sealed class RkzfTests : IDisposable
{
    private const int SmallChunk = 64;

    private readonly MediaKey _key = new(Convert.FromHexString(PhpFixture.Data.MediaKeyHex));

    public void Dispose() => _key.Dispose();

    public static TheoryData<string> PhpFiles() => [.. PhpFixture.Data.Files.Select(f => f.Name)];

    [Theory]
    [MemberData(nameof(PhpFiles))]
    public async Task Php_files_decrypt_to_the_expected_plaintext(string name)
    {
        var entry = PhpFixture.Data.Files.Single(f => f.Name == name);
        await using var input = File.OpenRead(PhpFixture.Path_(name));
        using var output = new MemoryStream();

        await Rkzf.DecryptAsync(input, output, _key);

        Assert.Equal(entry.PlainLength, output.Length);
        Assert.Equal(entry.PlainSha256, Convert.ToHexStringLower(SHA256.HashData(output.ToArray())));
    }

    [Theory]
    [MemberData(nameof(PhpFiles))]
    public async Task Encrypting_with_the_php_nonce_reproduces_the_php_file_byte_for_byte(string name)
    {
        var entry = PhpFixture.Data.Files.Single(f => f.Name == name);
        var php = await File.ReadAllBytesAsync(PhpFixture.Path_(name));
        using var output = new MemoryStream();

        await Rkzf.EncryptAsync(new MemoryStream(PhpFixture.Plain(entry.PlainLength)), output, _key, entry.ChunkSize, php[9..25], CancellationToken.None);

        Assert.Equal(php, output.ToArray());
    }

    [Fact]
    public async Task Header_is_25_bytes_with_big_endian_default_chunk_size()
    {
        var encrypted = await EncryptAsync([1, 2, 3]);

        Assert.True(Rkzf.HasSignature(encrypted));
        Assert.Equal("RKZF\u0001"u8.ToArray(), encrypted[..5]);
        Assert.Equal(new byte[] { 0x00, 0x10, 0x00, 0x00 }, encrypted[5..9]);
        Assert.Equal(Rkzf.HeaderLength + 3 + 16, encrypted.Length);
    }

    [Fact]
    public async Task File_nonce_is_random_per_file()
    {
        var a = await EncryptAsync([1, 2, 3]);
        var b = await EncryptAsync([1, 2, 3]);

        Assert.NotEqual(a[9..25], b[9..25]);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(SmallChunk - 1, 1)]
    [InlineData(SmallChunk, 1)]
    [InlineData(SmallChunk + 1, 2)]
    [InlineData((3 * SmallChunk) + 5, 4)]
    public async Task Round_trips_at_chunk_boundaries(int length, int expectedChunks)
    {
        var plain = PhpFixture.Plain(length);

        var encrypted = await EncryptAsync(plain, SmallChunk);

        Assert.Equal(Rkzf.HeaderLength + length + (16 * expectedChunks), encrypted.Length);
        Assert.Equal(plain, await DecryptAsync(encrypted));
    }

    [Fact]
    public async Task Round_trips_a_multi_megabyte_file_with_the_default_chunk_size()
    {
        var plain = new byte[(2 * Rkzf.DefaultChunkSize) + 12_345];
        Random.Shared.NextBytes(plain);

        var encrypted = await EncryptAsync(plain);

        Assert.Equal(Rkzf.HeaderLength + plain.Length + (3 * 16), encrypted.Length);
        Assert.Equal(plain, await DecryptAsync(encrypted));
    }

    [Fact]
    public async Task Reads_from_streams_that_return_short_reads()
    {
        var plain = PhpFixture.Plain(500);
        var encrypted = await EncryptAsync(plain, SmallChunk);
        using var output = new MemoryStream();

        await Rkzf.DecryptAsync(new TrickleStream(encrypted), output, _key);

        Assert.Equal(plain, output.ToArray());
    }

    [Fact]
    public async Task Tampered_chunk_is_rejected_and_nothing_from_it_is_written()
    {
        var encrypted = await EncryptAsync(PhpFixture.Plain(200), SmallChunk);
        var thirdChunk = Rkzf.HeaderLength + (2 * (SmallChunk + 16));
        encrypted[thirdChunk + 3] ^= 0x80;
        using var output = new MemoryStream();

        var ex = await Assert.ThrowsAsync<CryptoException>(() => Rkzf.DecryptAsync(new MemoryStream(encrypted), output, _key));

        Assert.Equal(CryptoError.CorruptedFile, ex.Error);
        Assert.Equal(2 * SmallChunk, output.Length);
    }

    [Fact]
    public async Task Reordered_chunks_are_rejected()
    {
        var encrypted = await EncryptAsync(PhpFixture.Plain(2 * SmallChunk), SmallChunk);
        var size = SmallChunk + 16;
        var first = encrypted.AsSpan(Rkzf.HeaderLength, size).ToArray();
        encrypted.AsSpan(Rkzf.HeaderLength + size, size).CopyTo(encrypted.AsSpan(Rkzf.HeaderLength));
        first.CopyTo(encrypted.AsSpan(Rkzf.HeaderLength + size));

        await AssertRejected(encrypted, CryptoError.CorruptedFile);
    }

    [Fact]
    public async Task Truncation_inside_a_chunk_is_rejected()
    {
        var encrypted = await EncryptAsync(PhpFixture.Plain(100), SmallChunk);

        await AssertRejected(encrypted[..^1], CryptoError.CorruptedFile);
        await AssertRejected(encrypted[..(Rkzf.HeaderLength + 10)], CryptoError.CorruptedFile);
    }

    [Fact]
    public async Task Wrong_key_is_rejected()
    {
        var encrypted = await EncryptAsync([1, 2, 3]);
        using var other = new MediaKey(new byte[32]);

        var ex = await Assert.ThrowsAsync<CryptoException>(() => Rkzf.DecryptAsync(new MemoryStream(encrypted), Stream.Null, other));
        Assert.Equal(CryptoError.CorruptedFile, ex.Error);
    }

    [Fact]
    public async Task Bad_headers_are_rejected_as_invalid_format()
    {
        var good = await EncryptAsync([1, 2, 3]);

        await AssertRejected([], CryptoError.InvalidFileFormat);
        await AssertRejected(good[..24], CryptoError.InvalidFileFormat);
        await AssertRejected("%PDF-1.7 not encrypted at all......"u8.ToArray(), CryptoError.InvalidFileFormat);

        var version2 = good.ToArray();
        version2[4] = 2;
        await AssertRejected(version2, CryptoError.InvalidFileFormat);

        var zeroChunk = good.ToArray();
        BinaryPrimitives.WriteUInt32BigEndian(zeroChunk.AsSpan(5), 0);
        await AssertRejected(zeroChunk, CryptoError.InvalidFileFormat);

        var hugeChunk = good.ToArray();
        BinaryPrimitives.WriteUInt32BigEndian(hugeChunk.AsSpan(5), uint.MaxValue);
        await AssertRejected(hugeChunk, CryptoError.InvalidFileFormat);
    }

    private async Task<byte[]> EncryptAsync(byte[] plain, int chunkSize = Rkzf.DefaultChunkSize)
    {
        using var output = new MemoryStream();
        await Rkzf.EncryptAsync(new MemoryStream(plain), output, _key, chunkSize, fileNonce: null, CancellationToken.None);
        return output.ToArray();
    }

    private async Task<byte[]> DecryptAsync(byte[] encrypted)
    {
        using var output = new MemoryStream();
        await Rkzf.DecryptAsync(new MemoryStream(encrypted), output, _key);
        return output.ToArray();
    }

    private async Task AssertRejected(byte[] encrypted, CryptoError expected)
    {
        var ex = await Assert.ThrowsAsync<CryptoException>(() => Rkzf.DecryptAsync(new MemoryStream(encrypted), Stream.Null, _key));
        Assert.Equal(expected, ex.Error);
    }

    /// <summary>تدفّق يعيد 7 بايتات على الأكثر في كلّ قراءة، كما تفعل الشبكة أحيانًا.</summary>
    private sealed class TrickleStream(byte[] data) : MemoryStream(data)
    {
        public override int Read(byte[] buffer, int offset, int count) => base.Read(buffer, offset, Math.Min(count, 7));

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            base.ReadAsync(buffer[..Math.Min(buffer.Length, 7)], cancellationToken);
    }
}
