using Xunit;

namespace Broiler.UI.Standard.Tests;

/// <summary>
/// Tests that change the process-wide theme (<see cref="StandardControlPaint.ApplyTheme"/>, directly
/// or through <see cref="StandardThemeController"/>). Standard controls capture the global theme when
/// constructed, and the animation scheduler and painters fall back to it, so any test running in
/// parallel could see another test's theme. This collection runs on its own after the parallel tests.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class GlobalThemeCollection
{
    public const string Name = "Global theme";
}
