using System.Xml.Linq;

namespace Rakeeza.Architecture.Tests;

/// <summary>
/// يحرس حدود الطبقات بقراءة ملفّات csproj مباشرة:
/// المكتبات تبقى net10.0 (تعمل على Linux)، ولا يعتمد أيّ مشروع إلّا على ما هو مسموح له.
/// </summary>
public sealed class ProjectLayeringTests
{
    private static readonly Dictionary<string, string[]> AllowedReferences = new()
    {
        ["Rakeeza.Core"] = [],
        ["Rakeeza.Data"] = ["Rakeeza.Core"],
        ["Rakeeza.Commands"] = ["Rakeeza.Core", "Rakeeza.Data"],
        ["Rakeeza.DevHost"] = ["Rakeeza.Commands"],
        ["Rakeeza.Desktop"] = ["Rakeeza.Commands"],
    };

    private static readonly string[] CrossPlatformProjects =
        ["Rakeeza.Core", "Rakeeza.Data", "Rakeeza.Commands", "Rakeeza.DevHost"];

    public static TheoryData<string> AllSourceProjects() => [.. AllowedReferences.Keys];

    public static TheoryData<string> CrossPlatform() => [.. CrossPlatformProjects];

    [Theory]
    [MemberData(nameof(AllSourceProjects))]
    public void Project_references_only_allowed_layers(string project)
    {
        var actual = ProjectReferences(project);

        Assert.Equal(AllowedReferences[project].Order(), actual.Order());
    }

    [Theory]
    [MemberData(nameof(CrossPlatform))]
    public void Cross_platform_projects_target_plain_net10(string project)
    {
        var doc = Load(project);
        var frameworks = doc.Descendants()
            .Where(e => e.Name.LocalName is "TargetFramework" or "TargetFrameworks")
            .Select(e => e.Value.Trim())
            .ToList();

        Assert.Equal(["net10.0"], frameworks);
        Assert.DoesNotContain(doc.Descendants(), e => e.Name.LocalName is "UseWPF" or "UseWindowsForms");
    }

    [Fact]
    public void Every_source_project_is_covered_by_the_rules()
    {
        var onDisk = Directory.GetDirectories(Path.Combine(RepoRoot.Value, "src"))
            .Select(Path.GetFileName)
            .Order();

        Assert.Equal(AllowedReferences.Keys.Order(), onDisk);
    }

    private static IEnumerable<string> ProjectReferences(string project) =>
        Load(project).Descendants()
            .Where(e => e.Name.LocalName == "ProjectReference")
            .Select(e => Path.GetFileNameWithoutExtension(e.Attribute("Include")!.Value.Replace('\\', '/')));

    private static XDocument Load(string project) =>
        XDocument.Load(Path.Combine(RepoRoot.Value, "src", project, project + ".csproj"));

    private static readonly Lazy<string> RepoRoot = new(() =>
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Rakeeza.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Rakeeza.slnx not found above test output.");
    });
}
