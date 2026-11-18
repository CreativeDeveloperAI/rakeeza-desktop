using System.Text.Json;

namespace Rakeeza.Core.Tests.Crypto;

/// <summary>
/// متّجهات ولّدها امتداد sodium في PHP (tools/fixtures/make-crypto-fixtures.php).
/// تثبت التطابق مع libsodium كما يستعملها PHP، لكنّها ليست ملفّات من النظام القديم نفسه.
/// </summary>
internal static class PhpFixture
{
    public static string Dir { get; } = Path.Combine(AppContext.BaseDirectory, "Fixtures", "php-sodium");

    public static string KeyPath => Path.Combine(Dir, "rakeeza.key");

    private static readonly Lazy<Expected> Value = new(() =>
        JsonSerializer.Deserialize<Expected>(File.ReadAllBytes(Path.Combine(Dir, "expected.json")), JsonSerializerOptions.Web)!);

    public static Expected Data => Value.Value;

    public static string Path_(string name) => Path.Combine(Dir, name);

    /// <summary>النمط الحتمي نفسه الذي يكتبه مولّد PHP.</summary>
    public static byte[] Plain(int length)
    {
        var bytes = new byte[length];
        for (var i = 0; i < length; i++)
        {
            bytes[i] = (byte)((i * 31 + 7) % 256);
        }

        return bytes;
    }

    internal sealed record Expected(string Passphrase, string MasterKeyHex, string MediaKeyHex, IReadOnlyList<FileEntry> Files);

    internal sealed record FileEntry(string Name, int ChunkSize, int PlainLength, string PlainSha256);
}
