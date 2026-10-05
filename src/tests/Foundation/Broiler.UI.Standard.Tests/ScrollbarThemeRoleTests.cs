using System.Collections.Generic;
using System.Linq;
using Broiler.Documents.FormatCodes;
using Broiler.Documents.Model;
using Broiler.Graphics.Color;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;
using Broiler.UI.CodeEditor;
using Broiler.UI.CodeEditor.Standard;
using Broiler.UI.FormatCodeView.Standard;
using Broiler.UI.ListView;
using Broiler.UI.ListView.Standard;
using Broiler.UI.RichEdit.Standard;
using Broiler.UI.ScrollView.Standard;
using Broiler.UI.TreeView;
using Broiler.UI.TreeView.Standard;

namespace Broiler.UI.Standard.Tests;

/// <summary>
/// The scrollbar roles of ADR 0033: every control that draws scrollbars takes them from the theme's
/// <see cref="StandardThemeTokens.ScrollbarTrack"/> and <see cref="StandardThemeTokens.ScrollbarThumb"/>, the
/// high-contrast palettes give the thumb 3:1 against its track and the surface, and the Light and Dark presets
/// draw every bar as before.
/// </summary>
public sealed class ScrollbarThemeRoleTests
{
    [Fact]
    public void The_Roles_Follow_The_Colors_The_Bars_Were_Drawn_In_Until_A_Theme_Sets_Them()
    {
        foreach (StandardThemeTokens preset in Presets)
        {
            Assert.Equal(preset.SurfaceDisabled, preset.ScrollbarTrack);
            Assert.Equal(preset.BorderStrong, preset.ScrollbarThumb);
        }

        // Unset, so a copy with other surfaces and borders carries them onto the bars.
        BColor surface = BColor.FromArgb(0xFF, 0x10, 0x20, 0x30);
        BColor border = BColor.FromArgb(0xFF, 0xD0, 0xE0, 0xF0);
        StandardThemeTokens recolored = StandardThemeTokens.Dark with { SurfaceDisabled = surface, BorderStrong = border };
        Assert.Equal(surface, recolored.ScrollbarTrack);
        Assert.Equal(border, recolored.ScrollbarThumb);

        // A set value is kept by every copy.
        BColor thumb = BColor.FromArgb(0xFF, 0x80, 0x00, 0x80);
        StandardThemeTokens set = StandardThemeTokens.Light with { ScrollbarThumb = thumb };
        Assert.Equal(thumb, (set with { BorderStrong = border }).ScrollbarThumb);
        Assert.Equal(thumb, set.WithTextScale(2).ScrollbarThumb);
        Assert.Equal(StandardThemeTokens.Light.SurfaceDisabled, set.ScrollbarTrack);
    }

    [Theory]
    [MemberData(nameof(HighContrastThemes))]
    public void High_Contrast_Gives_The_Thumb_Three_To_One_On_Its_Track_And_On_The_Surface(StandardThemeTokens theme)
    {
        Assert.True(theme.IsHighContrast);
        Assert.True(StandardContrast.Ratio(theme.ScrollbarThumb, theme.ScrollbarTrack) >= StandardContrast.AaLargeOrUi,
            $"{theme.Name}: thumb on track {StandardContrast.Ratio(theme.ScrollbarThumb, theme.ScrollbarTrack):F2}:1");
        Assert.True(StandardContrast.Ratio(theme.ScrollbarThumb, theme.Surface) >= StandardContrast.AaLargeOrUi,
            $"{theme.Name}: thumb on surface {StandardContrast.Ratio(theme.ScrollbarThumb, theme.Surface):F2}:1");
        Assert.True(theme.ScrollbarThumbContrast >= StandardContrast.AaLargeOrUi);
    }

    [Fact]
    public void The_Thumb_Contrast_Is_The_Lesser_Of_The_Track_And_The_Surface()
    {
        // A light track on a dark surface: the thumb reads on the track and not beside it.
        StandardThemeTokens theme = StandardThemeTokens.Dark with
        {
            ScrollbarTrack = BColor.White,
            ScrollbarThumb = BColor.Black,
        };

        Assert.Equal(StandardContrast.Ratio(BColor.Black, theme.Surface), theme.ScrollbarThumbContrast, 6);
        Assert.True(theme.ScrollbarThumbContrast < StandardContrast.Ratio(BColor.Black, BColor.White));
    }

    [Fact]
    public void A_Control_With_Bars_Of_Its_Own_Keeps_Them_Until_The_Theme_Gives_Scrollbars_Colors()
    {
        BColor track = BColor.FromArgb(0x33, 0x01, 0x02, 0x03);
        BColor thumb = BColor.FromArgb(0xAA, 0x04, 0x05, 0x06);

        Assert.Equal((track, thumb), StandardControlPaint.ScrollbarColors(StandardThemeTokens.Light, track, thumb));
        Assert.Equal((track, thumb), StandardControlPaint.ScrollbarColors(StandardThemeTokens.Dark.WithTextScale(2), track, thumb));
        Assert.Equal((track, thumb), StandardControlPaint.ScrollbarColors(StandardThemeTokens.Light with { SurfaceDisabled = BColor.White }, track, thumb));

        StandardThemeTokens contrast = StandardThemeTokens.HighContrastDark;
        Assert.Equal((contrast.ScrollbarTrack, contrast.ScrollbarThumb), StandardControlPaint.ScrollbarColors(contrast, track, thumb));

        // Setting either role gives the theme's pair, so the bar is never half the control's and half the theme's.
        BColor custom = BColor.FromArgb(0xFF, 0x80, 0x00, 0x80);
        StandardThemeTokens onlyThumb = StandardThemeTokens.Light with { ScrollbarThumb = custom };
        Assert.Equal((onlyThumb.SurfaceDisabled, custom), StandardControlPaint.ScrollbarColors(onlyThumb, track, thumb));
        Assert.Throws<ArgumentNullException>(() => StandardControlPaint.ScrollbarColors(null!, track, thumb));

        // A role set to the very color it follows is still set: the theme spoke about scrollbars.
        StandardThemeTokens explicitRoles = StandardThemeTokens.Light with
        {
            ScrollbarTrack = StandardThemeTokens.Light.SurfaceDisabled,
            ScrollbarThumb = StandardThemeTokens.Light.BorderStrong,
        };
        Assert.Equal((explicitRoles.ScrollbarTrack, explicitRoles.ScrollbarThumb), StandardControlPaint.ScrollbarColors(explicitRoles, track, thumb));
        Assert.Equal((explicitRoles.ScrollbarTrack, explicitRoles.ScrollbarThumb), StandardControlPaint.ScrollbarColors(explicitRoles.WithTextScale(2), track, thumb));

        // A palette whose surface and text sit at the extremes reads as high contrast without saying so, as it
        // does to the list, the tree and the code editor.
        StandardThemeTokens extremes = Extremes;
        Assert.False(extremes.IsHighContrast);
        Assert.Equal((extremes.ScrollbarTrack, extremes.ScrollbarThumb), StandardControlPaint.ScrollbarColors(extremes, track, thumb));
    }

    [Theory]
    [MemberData(nameof(Controls))]
    public void Every_Scrollbar_Draws_The_High_Contrast_Roles(string control)
    {
        foreach (StandardThemeTokens theme in HighContrastThemes.Cast<object[]>().Select(row => (StandardThemeTokens)row[0]))
        {
            (BColor track, BColor thumb) = RenderBar(control, theme);

            Assert.Equal(theme.ScrollbarTrack, track);
            Assert.Equal(theme.ScrollbarThumb, thumb);
            Assert.True(StandardContrast.Ratio(thumb, track) >= StandardContrast.AaLargeOrUi, $"{control} in {theme.Name}");
        }
    }

    [Theory]
    [MemberData(nameof(Controls))]
    public void Light_And_Dark_Draw_Every_Scrollbar_As_Before(string control)
    {
        foreach (StandardThemeTokens preset in new[] { StandardThemeTokens.Light, StandardThemeTokens.Dark, StandardThemeTokens.Dark.WithTextScale(2) })
        {
            // The scroll view, rich edit and formatting code view keep their translucent bars; the list, tree and
            // code editor keep the disabled surface and the strong border they were themed with.
            (BColor Track, BColor Thumb) expected = HasTranslucentBars(control)
                ? (TranslucentTrack, TranslucentThumb)
                : (preset.SurfaceDisabled, preset.BorderStrong);

            Assert.Equal(expected, RenderBar(control, preset));
        }
    }

    [Theory]
    [MemberData(nameof(Controls))]
    public void A_Custom_Palette_Reaches_Every_Scrollbar(string control)
    {
        BColor track = BColor.FromArgb(0xFF, 0xFA, 0xF0, 0xE6);
        BColor thumb = BColor.FromArgb(0xFF, 0x6A, 0x1B, 0x9A);
        Assert.Equal((track, thumb), RenderBar(control, StandardThemeTokens.Light with { ScrollbarTrack = track, ScrollbarThumb = thumb }));

        // A palette that sets only the thumb draws it on its own track, the role's default.
        StandardThemeTokens onlyThumb = StandardThemeTokens.Dark with { ScrollbarThumb = thumb };
        Assert.Equal((StandardThemeTokens.Dark.SurfaceDisabled, thumb), RenderBar(control, onlyThumb));
    }

    [Theory]
    [MemberData(nameof(Controls))]
    public void Roles_Set_To_The_Colors_They_Follow_Reach_Every_Scrollbar(string control)
    {
        StandardThemeTokens light = StandardThemeTokens.Light;
        StandardThemeTokens explicitRoles = light with { ScrollbarTrack = light.SurfaceDisabled, ScrollbarThumb = light.BorderStrong };

        Assert.Equal((light.SurfaceDisabled, light.BorderStrong), RenderBar(control, explicitRoles));
    }

    [Theory]
    [MemberData(nameof(Controls))]
    public void A_Palette_At_The_Extremes_Draws_Every_Scrollbar_In_Its_Roles(string control)
    {
        StandardThemeTokens extremes = Extremes;

        (BColor track, BColor thumb) = RenderBar(control, extremes);

        Assert.Equal((extremes.ScrollbarTrack, extremes.ScrollbarThumb), (track, thumb));
        Assert.True(StandardContrast.Ratio(thumb, track) >= StandardContrast.AaLargeOrUi, $"{control}: thumb on track {StandardContrast.Ratio(thumb, track):F2}:1");
    }

    [Theory]
    [MemberData(nameof(TranslucentControls))]
    public void A_Bar_Color_The_Application_Set_Outlives_A_Theme_Change(string control)
    {
        BColor thumb = BColor.FromArgb(0xFF, 0x6A, 0x1B, 0x9A);
        UiElement element = Create(control);
        var themed = (IStandardThemedControl)element;
        SetScrollbarThumb(element, thumb);

        // The thumb the application set stays; the track it left alone follows each theme.
        themed.ApplyTheme(StandardThemeTokens.Light);
        themed.ApplyTheme(StandardThemeTokens.HighContrastDark);
        Assert.Equal((StandardThemeTokens.HighContrastDark.ScrollbarTrack, thumb), VerticalBar(Render(element), control));

        themed.ApplyTheme(StandardThemeTokens.Dark);
        Assert.Equal((TranslucentTrack, thumb), VerticalBar(Render(element), control));
    }

    [Theory]
    [MemberData(nameof(Controls))]
    public void A_Theme_That_Says_Nothing_About_Scrollbars_Brings_Back_The_Bars_A_Preset_Draws(string control)
    {
        UiElement element = Create(control);
        var themed = (IStandardThemedControl)element;
        themed.ApplyTheme(StandardThemeTokens.HighContrastDark);
        themed.ApplyTheme(StandardThemeTokens.Light);

        (BColor Track, BColor Thumb) expected = HasTranslucentBars(control)
            ? (TranslucentTrack, TranslucentThumb)
            : (StandardThemeTokens.Light.SurfaceDisabled, StandardThemeTokens.Light.BorderStrong);
        Assert.Equal(expected, VerticalBar(Render(element), control));
    }

    // The translucent pair the scroll view, the rich edit and the formatting code view have always drawn.
    internal static readonly BColor TranslucentTrack = BColor.FromArgb(0x33, 0x94, 0xA3, 0xB8);
    internal static readonly BColor TranslucentThumb = BColor.FromArgb(0xAA, 0x7D, 0x8D, 0xA3);

    internal static readonly BRect ControlBounds = new(0, 0, 220, 120);

    private static StandardThemeTokens[] Presets =>
    [
        StandardThemeTokens.Light,
        StandardThemeTokens.Dark,
        StandardThemeTokens.HighContrastLight,
        StandardThemeTokens.HighContrastDark,
    ];

    public static TheoryData<string> Controls => new() { "scroll view", "rich edit", "format code view", "list", "tree", "code editor" };

    /// <summary>
    /// The high-contrast presets, and palettes shaped like the one Broiler.Hosting builds from a Windows contrast
    /// theme: the preset with the window color for every surface and the window text for every border and text.
    /// </summary>
    public static TheoryData<StandardThemeTokens> HighContrastThemes => new()
    {
        StandardThemeTokens.HighContrastLight,
        StandardThemeTokens.HighContrastDark,
        StandardThemeTokens.HighContrastLight.WithTextScale(2),
        SystemPalette(StandardThemeTokens.HighContrastDark, BColor.FromArgb(0xFF, 0x20, 0x20, 0x20), BColor.White),
        SystemPalette(StandardThemeTokens.HighContrastLight, BColor.FromArgb(0xFF, 0xFF, 0xFA, 0xEF), BColor.FromArgb(0xFF, 0x3D, 0x3D, 0x3D)),
    };

    public static TheoryData<string> TranslucentControls => new() { "scroll view", "rich edit", "format code view" };

    /// <summary>
    /// The four-color palette in black and white, which does not say it is high contrast: its roles follow the
    /// black surface and the white text.
    /// </summary>
    internal static StandardThemeTokens Extremes =>
        new(BColor.Black, BColor.White, BColor.FromArgb(0xFF, 0x00, 0x78, 0xD7), BColor.White);

    internal static bool HasTranslucentBars(string control) =>
        control is "scroll view" or "rich edit" or "format code view";

    private static void SetScrollbarThumb(UiElement element, BColor thumb)
    {
        switch (element)
        {
            case StandardScrollView scrollView:
                scrollView.ScrollbarThumb = thumb;
                break;
            case StandardRichEdit edit:
                edit.ScrollbarThumb = thumb;
                break;
            case StandardFormatCodeView view:
                view.ScrollbarThumb = thumb;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(element), element, null);
        }
    }

    internal static (BColor Track, BColor Thumb) RenderBar(string control, StandardThemeTokens theme)
    {
        UiElement element = Create(control);
        ((IStandardThemedControl)element).ApplyTheme(theme);
        return VerticalBar(Render(element), control);
    }

    /// <summary>A control of the kind named, with more content than <see cref="ControlBounds"/> shows.</summary>
    internal static UiElement Create(string control)
    {
        string lines = string.Join('\n', Enumerable.Range(0, 60).Select(line => $"line {line}"));
        switch (control)
        {
            case "scroll view":
                var scrollView = new StandardScrollView();
                scrollView.AddChild(new TallContent());
                return scrollView;
            case "rich edit":
                var edit = new StandardRichEdit();
                edit.SetPlainText(lines);
                return edit;
            case "format code view":
                return new StandardFormatCodeView { Projection = FormatCodeProjector.Project(RichTextDocument.FromPlainText(lines)) };
            case "list":
                var list = new StandardListView();
                list.SetItems(Enumerable.Range(0, 60).Select(index => new UiListItem($"item{index}", $"Item {index}")).ToArray());
                return list;
            case "tree":
                return new StandardTreeView { DataSource = new FlatTreeSource(60) };
            case "code editor":
                return new StandardCodeEditor { Document = new LinesDocument(60) };
            default:
                throw new ArgumentOutOfRangeException(nameof(control), control, null);
        }
    }

    internal static BRenderList Render(UiElement element)
    {
        using UiSession session = new StandardUiSessionBuilder().WithDispatcher(new ImmediateUiDispatcher()).Build(new TestHost(new BSize(400, 300)));
        var root = new FixedRoot((element, ControlBounds));
        session.AddRoot(root);

        // Two frames: a control that learns its extent while drawing is laid out knowing it in the second.
        session.RenderFrame();
        BRenderList list = session.RenderFrame();
        session.RemoveRoot(root);
        root.Release();
        return list;
    }

    /// <summary>
    /// The track and thumb of the vertical bar along the right edge of <paramref name="bounds"/>: the pill fills a
    /// bar's width wide and taller than wide, the track first.
    /// </summary>
    internal static (BColor Track, BColor Thumb) VerticalBar(BRenderList list, string control, BRect? bounds = null)
    {
        BRect area = bounds ?? ControlBounds;
        BRenderCommand.FillRoundedRect[] bar = list.Commands.OfType<BRenderCommand.FillRoundedRect>()
            .Where(command => command.Rect.Width == 12 && command.Rect.Height > command.Rect.Width &&
                command.Rect.Right <= area.Right && command.Rect.Left >= area.Right - 20 &&
                command.Rect.Top >= area.Top && command.Rect.Bottom <= area.Bottom)
            .ToArray();
        Assert.True(bar.Length == 2, $"{control}: expected a track and a thumb, found {bar.Length} bar fills.");
        Assert.True(bar[1].Rect.Height < bar[0].Rect.Height, $"{control}: the thumb is shorter than its track.");
        return (bar[0].Color, bar[1].Color);
    }

    private static StandardThemeTokens SystemPalette(StandardThemeTokens preset, BColor window, BColor windowText) =>
        preset with
        {
            Name = "HighContrastSystem",
            Surface = window,
            SurfaceAlt = window,
            SurfaceDisabled = window,
            Border = windowText,
            BorderStrong = windowText,
            Text = windowText,
            TextMuted = windowText,
        };

    private sealed class TallContent : UiElement
    {
        protected override BSize MeasureCore(BSize availableSize) => new(80, 1000);
    }

    internal sealed class FlatTreeSource(int count) : ITreeDataSource
    {
        public TreeNodeId Root => new("/");
        public int GetChildCount(TreeNodeId node) => node.Value == "/" ? count : 0;
        public TreeNodeId GetChild(TreeNodeId node, int index) => new($"/node{index}");
        public bool CanExpand(TreeNodeId node) => GetChildCount(node) > 0;
        public TreeNodePresentation GetPresentation(TreeNodeId node) => new(node, node.Value[1..]);
    }

    /// <summary>A read-only document of short, numbered lines.</summary>
    internal sealed class LinesDocument(int count) : ICodeDocument
    {
        public ICodeTextSnapshot Snapshot { get; } = new LinesSnapshot(count);
        public bool IsReadOnly => true;
        public string LineEnding => "\n";
        public bool CanUndo => false;
        public bool CanRedo => false;

        public event Action<ICodeTextSnapshot>? SnapshotChanged
        {
            add { }
            remove { }
        }

        public CodeEditOutcome Submit(CodeEditIntent intent) => new(false, Snapshot, CodeEditRejection.ReadOnly);
        public bool Undo() => false;
        public bool Redo() => false;
        public void BreakUndoGroup() { }
    }

    /// <summary>Lines "line 00" to "line NN", each eight characters with its line break.</summary>
    private sealed class LinesSnapshot(int count) : ICodeTextSnapshot
    {
        private const int Stride = 8;
        private readonly string _text = string.Join('\n', Enumerable.Range(0, count).Select(line => $"line {line:00}"));

        public int Version => 1;
        public int Length => _text.Length;
        public int LineCount => count;
        public int GetLineStart(int line) => line * Stride;
        public int GetLineLength(int line) => Stride - 1;
        public int GetLineFromPosition(int position) => Math.Min(count - 1, position / Stride);
        public string GetText(int start, int length) => _text.Substring(start, length);
        public void CopyTo(int start, int length, Span<char> destination) => _text.AsSpan(start, length).CopyTo(destination);
        public int GetNextCaretPosition(int position) => Math.Min(_text.Length, position + 1);
        public int GetPreviousCaretPosition(int position) => Math.Max(0, position - 1);
    }

    /// <summary>Arranges each child into its own fixed rectangle.</summary>
    internal sealed class FixedRoot : UiElement
    {
        private readonly (UiElement Child, BRect Rect)[] _children;

        public FixedRoot(params (UiElement Child, BRect Rect)[] children)
        {
            _children = children;
            foreach ((UiElement child, _) in children)
                AddChild(child);
        }

        /// <summary>Lets the children go, so a later frame can place them in another root.</summary>
        public void Release()
        {
            foreach ((UiElement child, _) in _children)
                RemoveChild(child);
        }

        protected override BSize MeasureCore(BSize availableSize)
        {
            foreach ((UiElement child, BRect rect) in _children)
                child.Measure(new BSize(rect.Width, rect.Height));
            return availableSize;
        }

        protected override void ArrangeCore(BRect finalRect)
        {
            foreach ((UiElement child, BRect rect) in _children)
                child.Arrange(rect);
        }
    }

    internal sealed class TestHost(BSize viewportSize) : IUiHost
    {
        public BSize ViewportSize => viewportSize;
        public double Scale => 1.0;
        public BRenderList CreateRenderList(int capacity = 0) => new(capacity);
        public void Invalidate(UiInvalidation invalidation) { }
        public void Present(BRenderList renderList) { }
    }
}

/// <summary>
/// The scrollbar roles reached through the shared palette: a control built under it and never themed, and every
/// control in a session the theme controller themes.
/// </summary>
[Collection(GlobalThemeCollection.Name)]
public sealed class ScrollbarSharedPaletteTests
{
    [Theory]
    [MemberData(nameof(ScrollbarThemeRoleTests.Controls), MemberType = typeof(ScrollbarThemeRoleTests))]
    public void A_Control_Built_Under_A_High_Contrast_Palette_Draws_Its_Bars_Without_Being_Themed(string control)
    {
        StandardThemeTokens contrast = StandardThemeTokens.HighContrastDark;
        StandardControlPaint.ApplyTheme(contrast);
        try
        {
            UiElement element = ScrollbarThemeRoleTests.Create(control);
            Assert.Equal(
                (contrast.ScrollbarTrack, contrast.ScrollbarThumb),
                ScrollbarThemeRoleTests.VerticalBar(ScrollbarThemeRoleTests.Render(element), control));
        }
        finally
        {
            StandardControlPaint.ApplyTheme(StandardThemeTokens.Light);
        }

        // Built under the default palette, the control draws what it always drew.
        (BColor Track, BColor Thumb) expected = ScrollbarThemeRoleTests.HasTranslucentBars(control)
            ? (ScrollbarThemeRoleTests.TranslucentTrack, ScrollbarThemeRoleTests.TranslucentThumb)
            : (StandardThemeTokens.Light.SurfaceDisabled, StandardThemeTokens.Light.BorderStrong);
        Assert.Equal(expected, ScrollbarThemeRoleTests.VerticalBar(ScrollbarThemeRoleTests.Render(ScrollbarThemeRoleTests.Create(control)), control));
    }

    [Fact]
    public void The_Theme_Controller_Gives_Every_Scrollbar_In_A_Session_The_Palette_Roles()
    {
        string[] controls = ScrollbarThemeRoleTests.Controls.Cast<object[]>().Select(row => (string)row[0]).ToArray();
        (UiElement Child, BRect Rect)[] children = controls
            .Select((control, index) => (ScrollbarThemeRoleTests.Create(control), new BRect(index % 3 * 240, index / 3 * 140, 220, 120)))
            .ToArray();
        BColor track = BColor.FromArgb(0xFF, 0xFA, 0xF0, 0xE6);
        BColor thumb = BColor.FromArgb(0xFF, 0x6A, 0x1B, 0x9A);

        using UiSession session = new StandardUiSessionBuilder()
            .WithDispatcher(new ImmediateUiDispatcher())
            .Build(new ScrollbarThemeRoleTests.TestHost(new BSize(720, 280)));
        session.AddRoot(new ScrollbarThemeRoleTests.FixedRoot(children));
        try
        {
            StandardThemeController.Apply(session, StandardThemeTokens.Light with { ScrollbarTrack = track, ScrollbarThumb = thumb });
            session.RenderFrame();
            BRenderList list = session.RenderFrame();

            for (int index = 0; index < controls.Length; index++)
                Assert.Equal((track, thumb), ScrollbarThemeRoleTests.VerticalBar(list, controls[index], children[index].Rect));
        }
        finally
        {
            StandardControlPaint.ClearSessionTheme(session);
            StandardControlPaint.ApplyTheme(StandardThemeTokens.Light);
        }
    }
}
