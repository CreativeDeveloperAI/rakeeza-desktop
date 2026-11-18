// الاستعمال:
//   dotnet run --project tools/Rakeeza.TypeGen            يكتب ui/src/bridge/generated.ts
//   dotnet run --project tools/Rakeeza.TypeGen -- --check  يفشل (رمز 1) إن كان الملفّ المحفوظ قديمًا
using Rakeeza.Commands.Bridge;
using Rakeeza.TypeGen;

var check = args.Contains("--check", StringComparer.Ordinal);
var output = args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal))
    ?? Path.Combine(FindRepoRoot(), "ui", "src", "bridge", "generated.ts");

var generated = TypeScriptGenerator.Generate(CommandRegistry.FromCommandsAssembly());

if (check)
{
    var current = File.Exists(output) ? (await File.ReadAllTextAsync(output)).ReplaceLineEndings("\n") : null;
    if (current != generated)
    {
        await Console.Error.WriteLineAsync($"{output} is out of date. Run: dotnet run --project tools/Rakeeza.TypeGen");
        return 1;
    }

    Console.WriteLine($"{output} is up to date.");
    return 0;
}

Directory.CreateDirectory(Path.GetDirectoryName(output)!);
await File.WriteAllTextAsync(output, generated);
Console.WriteLine($"Wrote {output}");
return 0;

static string FindRepoRoot()
{
    var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Rakeeza.slnx")))
    {
        dir = dir.Parent;
    }

    return dir?.FullName ?? throw new InvalidOperationException("Run from inside the repository.");
}
