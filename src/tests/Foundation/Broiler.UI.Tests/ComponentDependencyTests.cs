using System.Xml.Linq;

namespace Broiler.UI.Tests;

public sealed class ComponentDependencyTests
{
    [Fact]
    public void Shipping_Projects_Use_Packages_For_External_Components()
    {
        string repository = RepositoryRoot();
        var projects = Directory.EnumerateFiles(Path.Combine(repository, "src"), "*.csproj", SearchOption.AllDirectories)
            .Where(path => !Path.GetRelativePath(repository, path).Replace('\\', '/').StartsWith("src/tests/", StringComparison.Ordinal))
            .Where(path => !Path.GetRelativePath(repository, path).Replace('\\', '/').StartsWith("src/samples/", StringComparison.Ordinal))
            .Where(path => !path.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj"))
            .ToArray();
        Assert.True(projects.Length >= 50, "Expected the complete shipping project set.");

        foreach (string path in projects)
        {
            XDocument project = XDocument.Load(path);
            foreach (XElement reference in project.Descendants("ProjectReference"))
            {
                string include = (string)reference.Attribute("Include")!;
                Assert.DoesNotContain("$(", include);
                string target = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, include.Replace('\\', Path.DirectorySeparatorChar)));
                string relative = Path.GetRelativePath(repository, target).Replace('\\', '/');
                Assert.StartsWith("src/", relative);
                Assert.StartsWith("Broiler.UI", Path.GetFileNameWithoutExtension(target));
                Assert.True(File.Exists(target), $"Missing project reference: {path} -> {target}");
            }

            foreach (XElement package in project.Descendants("PackageReference"))
            {
                string id = (string)package.Attribute("Include")!;
                Assert.DoesNotContain("Windows", id, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("Linux", id, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("WebAssembly", id, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("Direct2D", id, StringComparison.OrdinalIgnoreCase);
                Assert.False(string.IsNullOrWhiteSpace((string?)package.Attribute("Version")), $"Unversioned dependency: {path} -> {id}");
            }
        }
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Broiler.UI.slnx")))
                return directory.FullName;
        throw new DirectoryNotFoundException("Broiler.UI component root not found.");
    }
}
