using Rakeeza.Commands.Bridge;
using Rakeeza.TypeGen;

namespace Rakeeza.Commands.Tests;

public sealed class TypeScriptGeneratorTests
{
    public enum Confidentiality
    {
        Normal,
        TopSecret,
    }

    public sealed record Party(string Name);

    public sealed record SampleRequest(
        string Title,
        string? Note,
        int Count,
        int? Limit,
        bool Flag,
        DateOnly Day,
        Guid Id,
        Confidentiality Level,
        Party Sender,
        Party? Replier,
        IReadOnlyList<Party> Recipients,
        IReadOnlyList<string?> Tags,
        IReadOnlyDictionary<string, int> Counters);

    public sealed record SampleResponse(bool Ok);

    [Fact]
    public void Maps_records_to_typescript_following_bridge_json_rules()
    {
        var registry = new CommandRegistry(
            [new CommandDescriptor("test.sample", typeof(SampleRequest), typeof(SampleResponse), typeof(object))]);

        var ts = TypeScriptGenerator.Generate(registry);

        Assert.Contains("""
            export interface SampleRequest {
              title: string;
              note: string | null;
              count: number;
              limit: number | null;
              flag: boolean;
              day: string;
              id: string;
              level: Confidentiality;
              sender: Party;
              replier: Party | null;
              recipients: Party[];
              tags: (string | null)[];
              counters: Record<string, number>;
            }
            """.ReplaceLineEndings("\n"), ts, StringComparison.Ordinal);
        Assert.Contains("export type Confidentiality = 'normal' | 'topSecret';", ts, StringComparison.Ordinal);
        Assert.Contains("export interface Party {\n  name: string;\n}", ts, StringComparison.Ordinal);
        Assert.Contains("'test.sample': { request: SampleRequest; response: SampleResponse };", ts, StringComparison.Ordinal);
    }

    [Fact]
    public void Committed_generated_file_is_up_to_date()
    {
        var path = Path.Combine(RepoRoot(), "ui", "src", "bridge", "generated.ts");
        var committed = File.ReadAllText(path).ReplaceLineEndings("\n");

        var generated = TypeScriptGenerator.Generate(CommandRegistry.FromCommandsAssembly());

        Assert.True(
            committed == generated,
            "ui/src/bridge/generated.ts is stale. Run: dotnet run --project tools/Rakeeza.TypeGen");
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Rakeeza.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Rakeeza.slnx not found.");
    }
}
