using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Rakeeza.Core.Crypto;

namespace Rakeeza.Core.Tests.Crypto;

public sealed class KeyFileTests : IDisposable
{
    /// <summary>أخفّ معاملات تقبلها libsodium، كي تبقى الاختبارات سريعة. الملفّات الحقيقية تُقرأ بمعاملاتها.</summary>
    private static readonly KdfParameters FastKdf = new(OpsLimit: 1, MemLimit: 8_192);

    private readonly string _dir = Directory.CreateTempSubdirectory("rakeeza-keyfile-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Php_key_file_unlocks_to_the_php_master_key()
    {
        using var master = KeyFile.Read(PhpFixture.KeyPath).Unlock(PhpFixture.Data.Passphrase);

        Assert.Equal(PhpFixture.Data.MasterKeyHex, master.ToSqlCipherHex());
    }

    [Fact]
    public void Media_key_matches_php_generichash()
    {
        using var master = new MasterKey(Convert.FromHexString(PhpFixture.Data.MasterKeyHex));
        using var media = master.DeriveMediaKey();

        Assert.Equal(PhpFixture.Data.MediaKeyHex, Convert.ToHexStringLower(media.Bytes));
    }

    [Fact]
    public void Wrapping_with_php_salt_and_nonce_reproduces_the_php_file_byte_for_byte()
    {
        var php = JsonNode.Parse(File.ReadAllText(PhpFixture.KeyPath))!.AsObject();
        using var master = new MasterKey(Convert.FromHexString(PhpFixture.Data.MasterKeyHex));

        var ours = KeyFile.Create(
            PhpFixture.Data.Passphrase,
            master,
            new KdfParameters((ulong)php["ops"]!, (ulong)php["mem"]!),
            Convert.FromBase64String((string)php["salt"]!),
            Convert.FromBase64String((string)php["nonce"]!));
        var mine = JsonNode.Parse(ours.ToJsonUtf8())!.AsObject();

        // نصّ JSON نفسه يختلف (PHP يهرّب "/")، أمّا القيم فمتطابقة كلّها.
        Assert.Equal(
            php.Select(p => (p.Key, p.Value!.ToJsonString())),
            mine.Select(p => (p.Key, p.Value!.ToJsonString())));
    }

    [Fact]
    public void Wrong_passphrase_is_rejected()
    {
        var file = KeyFile.Read(PhpFixture.KeyPath);

        var ex = Assert.Throws<CryptoException>(() => file.Unlock(PhpFixture.Data.Passphrase + "x"));
        Assert.Equal(CryptoError.WrongPassphrase, ex.Error);
    }

    [Fact]
    public void Passphrase_is_not_unicode_normalized()
    {
        // "ركيزة" مع همزة مركّبة مقابل محرف مسبق التركيب: بايتات مختلفة ⇒ مفتاح مختلف، كما في PHP.
        using var master = MasterKey.Generate();
        var file = KeyFile.Create("أ", master, FastKdf);

        var ex = Assert.Throws<CryptoException>(() => file.Unlock("أ"));
        Assert.Equal(CryptoError.WrongPassphrase, ex.Error);
    }

    [Fact]
    public void Tampered_wrapped_key_is_rejected_like_a_wrong_passphrase()
    {
        var json = JsonNode.Parse(File.ReadAllText(PhpFixture.KeyPath))!.AsObject();
        var key = Convert.FromBase64String((string)json["key"]!);
        key[^1] ^= 0x01;
        json["key"] = Convert.ToBase64String(key);

        var file = KeyFile.Parse(Encoding.UTF8.GetBytes(json.ToJsonString()));

        Assert.Equal(CryptoError.WrongPassphrase, Assert.Throws<CryptoException>(() => file.Unlock(PhpFixture.Data.Passphrase)).Error);
    }

    public static TheoryData<string> MalformedKeyFiles() =>
    [
        "",
        "not json",
        "[]",
        """{"version":2,"kdf":"argon2id","salt":"AAAAAAAAAAAAAAAAAAAAAA==","ops":1,"mem":8192,"nonce":"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA","key":"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"}""",
        """{"version":1,"kdf":"scrypt","salt":"AAAAAAAAAAAAAAAAAAAAAA==","ops":1,"mem":8192,"nonce":"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA","key":"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"}""",
        """{"version":1,"kdf":"argon2id","ops":1,"mem":8192,"nonce":"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA","key":"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"}""",
        """{"version":1,"kdf":"argon2id","salt":"AAAA","ops":1,"mem":8192,"nonce":"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA","key":"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"}""",
        """{"version":1,"kdf":"argon2id","salt":"not base64!","ops":1,"mem":8192,"nonce":"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA","key":"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"}""",
        """{"version":1,"kdf":"argon2id","salt":"AAAAAAAAAAAAAAAAAAAAAA==","ops":"1","mem":8192,"nonce":"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA","key":"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"}""",
        """{"version":1,"kdf":"argon2id","salt":"AAAAAAAAAAAAAAAAAAAAAA==","ops":0,"mem":8192,"nonce":"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA","key":"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"}""",
        """{"version":1,"kdf":"argon2id","salt":"AAAAAAAAAAAAAAAAAAAAAA==","ops":1,"mem":8192,"nonce":"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA","key":"AAAA"}""",
    ];

    [Theory]
    [MemberData(nameof(MalformedKeyFiles))]
    public void Malformed_key_file_is_rejected(string json)
    {
        var ex = Assert.Throws<CryptoException>(() => KeyFile.Parse(Encoding.UTF8.GetBytes(json)));
        Assert.Equal(CryptoError.InvalidKeyFile, ex.Error);
    }

    [Fact]
    public void Kdf_limits_libsodium_rejects_are_reported_as_an_invalid_key_file()
    {
        // mem أقلّ من الحدّ الأدنى لـ Argon2id (8 KiB): الملفّ يُقرأ، لكنّ الاشتقاق يُرفض.
        var json = """{"version":1,"kdf":"argon2id","salt":"AAAAAAAAAAAAAAAAAAAAAA==","ops":1,"mem":1024,"nonce":"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA","key":"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"}""";
        var file = KeyFile.Parse(Encoding.UTF8.GetBytes(json));

        Assert.Equal(CryptoError.InvalidKeyFile, Assert.Throws<CryptoException>(() => file.Unlock("x")).Error);
    }

    [Fact]
    public void Create_then_unlock_round_trips_and_salts_are_random()
    {
        using var master = MasterKey.Generate();

        var a = KeyFile.Create("عبارة مرور", master, FastKdf);
        var b = KeyFile.Create("عبارة مرور", master, FastKdf);
        using var unlocked = a.Unlock("عبارة مرور");

        Assert.Equal(master.ToSqlCipherHex(), unlocked.ToSqlCipherHex());
        Assert.NotEqual(a.ToJsonUtf8(), b.ToJsonUtf8());
    }

    [Fact]
    public void Json_has_the_legacy_shape_and_field_order()
    {
        using var master = MasterKey.Generate();
        var json = KeyFile.Create("p", master, FastKdf).ToJsonUtf8();

        using var doc = JsonDocument.Parse(json);
        Assert.Equal(
            ["version", "kdf", "salt", "ops", "mem", "nonce", "key"],
            doc.RootElement.EnumerateObject().Select(p => p.Name));
        Assert.Equal(1, doc.RootElement.GetProperty("version").GetInt32());
        Assert.Equal("argon2id", doc.RootElement.GetProperty("kdf").GetString());
        Assert.DoesNotContain("\\u", Encoding.UTF8.GetString(json), StringComparison.Ordinal);
    }

    [Fact]
    public void Default_kdf_is_libsodium_moderate()
    {
        Assert.Equal(new KdfParameters(3, 268_435_456), KeyFile.DefaultKdf);
    }

    [Fact]
    public void WriteAtomic_replaces_the_file_and_leaves_no_temp_file()
    {
        var path = Path.Combine(_dir, "rakeeza.key");
        File.WriteAllText(path, "old");
        using var master = MasterKey.Generate();
        var file = KeyFile.Create("p", master, FastKdf);

        file.WriteAtomic(path);

        Assert.Equal(file.ToJsonUtf8(), File.ReadAllBytes(path));
        Assert.False(File.Exists(path + ".new"));
        using var unlocked = KeyFile.Read(path).Unlock("p");
        Assert.Equal(master.ToSqlCipherHex(), unlocked.ToSqlCipherHex());
    }

    [Fact]
    public void WriteAtomic_overwrites_a_stale_temp_file_from_an_interrupted_write()
    {
        var path = Path.Combine(_dir, "rakeeza.key");
        File.WriteAllText(path + ".new", "half-written garbage that is longer than the real file will ever be ............................................................................................................................................................................................");
        using var master = MasterKey.Generate();

        KeyFile.Create("p", master, FastKdf).WriteAtomic(path);

        using var unlocked = KeyFile.Read(path).Unlock("p");
        Assert.Equal(master.ToSqlCipherHex(), unlocked.ToSqlCipherHex());
    }

    [Fact]
    public void Disposed_key_cannot_be_used()
    {
        var master = MasterKey.Generate();
        master.Dispose();

        Assert.Throws<ObjectDisposedException>(master.ToSqlCipherHex);
    }

    [Fact]
    public void Sql_cipher_hex_is_64_lowercase_hex_chars()
    {
        using var master = new MasterKey(Enumerable.Range(0, 32).Select(i => (byte)(i * 8)).ToArray());

        Assert.Equal("0008101820283038404850586068707880889098a0a8b0b8c0c8d0d8e0e8f0f8", master.ToSqlCipherHex());
    }
}
