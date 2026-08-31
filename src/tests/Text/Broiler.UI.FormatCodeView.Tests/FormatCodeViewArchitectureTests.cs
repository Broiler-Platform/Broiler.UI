using System.Xml.Linq;

namespace Broiler.UI.FormatCodeView.Tests;

public sealed class FormatCodeViewArchitectureTests
{
    [Fact(Timeout = 600000)]
    public void Abstraction_References_Only_Projector_Ui_And_Graphics()
    {
        XDocument project = XDocument.Load(ProjectPath(
            "src", "Abstractions", "Text", "Broiler.UI.FormatCodeView", "Broiler.UI.FormatCodeView.csproj"));
        string[] references = References(project);

        Assert.Equal(
        [
            "$(BroilerDocumentsRoot)/src/Broiler.Documents.FormatCodes/Broiler.Documents.FormatCodes.csproj",
            "../../../../Broiler.Graphics/src/Broiler.Graphics/Broiler.Graphics.csproj",
            "../../../Foundation/Broiler.UI/Broiler.UI.csproj",
        ],
            references);
        Assert.Empty(project.Descendants("PackageReference"));
        Assert.DoesNotContain(references, reference =>
            reference.Contains("Standard", StringComparison.Ordinal) ||
            reference.Contains("DOM", StringComparison.OrdinalIgnoreCase) ||
            reference.Contains("Windows", StringComparison.OrdinalIgnoreCase));
    }

    private static string[] References(XDocument project) => project
        .Descendants("ProjectReference")
        .Select(reference => ((string?)reference.Attribute("Include"))?.Replace('\\', '/'))
        .Where(reference => reference is not null)
        .Cast<string>()
        .OrderBy(reference => reference, StringComparer.Ordinal)
        .ToArray();

    // The component is its own repository, so the solution file marks the root. Walking up
    // from the test binary keeps this working from bin/, from the IDE, and from CI.
    internal static string ProjectPath(params string[] parts)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Broiler.UI.slnx")))
                return Path.Combine([directory.FullName, .. parts]);

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Broiler.UI root not found.");
    }
}
