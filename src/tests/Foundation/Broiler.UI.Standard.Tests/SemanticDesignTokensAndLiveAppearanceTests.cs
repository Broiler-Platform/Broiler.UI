using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Broiler.Graphics;
using Broiler.Graphics.Color;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;
using Broiler.Graphics.Text;
using Broiler.UI.Button.Standard;
using Broiler.UI.Edit.Standard;
using Broiler.UI.Label.Standard;
using Broiler.UI.ListView.Standard;
using Xunit;

namespace Broiler.UI.Standard.Tests;

[Collection(GlobalThemeCollection.Name)]
public sealed class SemanticDesignTokensAndLiveAppearanceTests : IDisposable
{
    // Several tests switch the global theme through StandardThemeController; xUnit creates one
    // instance per test, so restoring here keeps each test independent of the order they run in.
    private readonly StandardThemeTokens _originalTheme = StandardControlPaint.Theme;

    public void Dispose() => StandardControlPaint.ApplyTheme(_originalTheme);

    [Fact]
    public void StandardLabel_Retains_Semantic_Roles_Across_Theme_Switches()
    {
        var warningLabel = StandardLabel.Warning("Disk space critically low");
        var mutedLabel = StandardLabel.Muted("Last updated 5m ago");
        var dangerLabel = StandardLabel.Danger("Connection failed");
        var successLabel = StandardLabel.Success("Changes saved");
        var accentLabel = StandardLabel.Accent("Recommended action");
        var defaultLabel = new StandardLabel("Regular body text");

        Assert.Equal(StandardLabelRole.Warning, warningLabel.Role);
        Assert.Equal(StandardLabelRole.Muted, mutedLabel.Role);
        Assert.Equal(StandardLabelRole.Danger, dangerLabel.Role);
        Assert.Equal(StandardLabelRole.Success, successLabel.Role);
        Assert.Equal(StandardLabelRole.Accent, accentLabel.Role);
        Assert.Equal(StandardLabelRole.Default, defaultLabel.Role);

        // Apply Light theme
        warningLabel.ApplyTheme(StandardThemeTokens.Light);
        mutedLabel.ApplyTheme(StandardThemeTokens.Light);
        dangerLabel.ApplyTheme(StandardThemeTokens.Light);
        successLabel.ApplyTheme(StandardThemeTokens.Light);
        accentLabel.ApplyTheme(StandardThemeTokens.Light);
        defaultLabel.ApplyTheme(StandardThemeTokens.Light);

        Assert.Equal(StandardThemeTokens.Light.Warning, warningLabel.Foreground);
        Assert.Equal(StandardThemeTokens.Light.TextMuted, mutedLabel.Foreground);
        Assert.Equal(StandardThemeTokens.Light.Danger, dangerLabel.Foreground);
        Assert.Equal(StandardThemeTokens.Light.Success, successLabel.Foreground);
        Assert.Equal(StandardThemeTokens.Light.AccentText, accentLabel.Foreground);
        Assert.Equal(StandardThemeTokens.Light.Text, defaultLabel.Foreground);

        // Switch to Dark theme — semantic roles must retain their status colors, NOT revert to generic Text
        warningLabel.ApplyTheme(StandardThemeTokens.Dark);
        mutedLabel.ApplyTheme(StandardThemeTokens.Dark);
        dangerLabel.ApplyTheme(StandardThemeTokens.Dark);
        successLabel.ApplyTheme(StandardThemeTokens.Dark);
        accentLabel.ApplyTheme(StandardThemeTokens.Dark);
        defaultLabel.ApplyTheme(StandardThemeTokens.Dark);

        Assert.Equal(StandardThemeTokens.Dark.Warning, warningLabel.Foreground);
        Assert.Equal(StandardThemeTokens.Dark.TextMuted, mutedLabel.Foreground);
        Assert.Equal(StandardThemeTokens.Dark.Danger, dangerLabel.Foreground);
        Assert.Equal(StandardThemeTokens.Dark.Success, successLabel.Foreground);
        // Accent text, not the accent fill, which reads at only 3.4:1 on the dark surface (ADR 0031).
        Assert.Equal(StandardThemeTokens.Dark.AccentText, accentLabel.Foreground);
        Assert.Equal(StandardThemeTokens.Dark.Text, defaultLabel.Foreground);

        // Switch to HighContrastDark theme
        warningLabel.ApplyTheme(StandardThemeTokens.HighContrastDark);
        mutedLabel.ApplyTheme(StandardThemeTokens.HighContrastDark);
        defaultLabel.ApplyTheme(StandardThemeTokens.HighContrastDark);

        Assert.Equal(StandardThemeTokens.HighContrastDark.Warning, warningLabel.Foreground);
        Assert.Equal(StandardThemeTokens.HighContrastDark.TextMuted, mutedLabel.Foreground);
        Assert.Equal(StandardThemeTokens.HighContrastDark.Text, defaultLabel.Foreground);
    }

    [Fact]
    public void StandardLabel_Custom_Role_Preserves_Explicit_Foreground()
    {
        var label = new StandardLabel("Custom text")
        {
            Foreground = BColor.FromArgb(0xFF, 0x12, 0x34, 0x56),
            Role = StandardLabelRole.Custom,
        };

        label.ApplyTheme(StandardThemeTokens.Dark);
        Assert.Equal(BColor.FromArgb(0xFF, 0x12, 0x34, 0x56), label.Foreground);

        label.ApplyTheme(StandardThemeTokens.Light);
        Assert.Equal(BColor.FromArgb(0xFF, 0x12, 0x34, 0x56), label.Foreground);
    }

    [Fact]
    public void TextControl_Preserves_Focus_Caret_And_Selection_Across_Theme_Switches()
    {
        using UiSession session = CreateSession(out _);
        var edit = new StandardEdit
        {
            Text = "Hello, design token system!",
        };
        session.AddRoot(edit);

        // Set selection and focus
        session.SetFocus(edit);
        edit.SetSelection(7, 6); // Selects "design"

        Assert.Same(edit, session.FocusedElement);
        Assert.Equal(7, edit.SelectionStart);
        Assert.Equal(6, edit.SelectionLength);
        Assert.Equal("design", edit.Text.Substring(edit.SelectionStart, edit.SelectionLength));

        // Apply Dark theme live through the controller
        StandardThemeController.Apply(session, StandardThemeTokens.Dark);

        // Assert colors changed
        Assert.Equal(StandardThemeTokens.Dark.Surface, edit.Background);
        Assert.Equal(StandardThemeTokens.Dark.Text, edit.Foreground);
        Assert.Equal(StandardThemeTokens.Dark.AccentSoft, edit.SelectionBackground);

        // Assert focus, caret, and selection are 100% preserved
        Assert.Same(edit, session.FocusedElement);
        Assert.Equal(7, edit.SelectionStart);
        Assert.Equal(6, edit.SelectionLength);
        Assert.Equal("Hello, design token system!", edit.Text);
        Assert.Equal("design", edit.Text.Substring(edit.SelectionStart, edit.SelectionLength));

        // Switch to HighContrastLight
        StandardThemeController.Apply(session, StandardThemeTokens.HighContrastLight);

        Assert.Same(edit, session.FocusedElement);
        Assert.Equal(7, edit.SelectionStart);
        Assert.Equal(6, edit.SelectionLength);
        Assert.Equal("design", edit.Text.Substring(edit.SelectionStart, edit.SelectionLength));
    }

    [Fact]
    public void Immediate_Theme_Updates_Across_Controls_And_Popups()
    {
        using UiSession session = CreateSession(out SettingsThemeHost host);
        var button = new StandardButton { Text = "Submit" };
        var label = StandardLabel.Warning("Caution");
        var list = new StandardListView();

        session.AddRoot(button);
        session.AddRoot(label);
        session.AddRoot(list);

        host.Invalidations.Clear();
        int themedCount = StandardThemeController.Apply(session, StandardThemeTokens.Dark);

        Assert.Equal(3, themedCount);
        Assert.Equal(StandardThemeTokens.Dark.Surface, button.Background);
        Assert.Equal(StandardThemeTokens.Dark.Warning, label.Foreground);
        Assert.Equal(StandardThemeTokens.Dark.Surface, list.Background);
        Assert.NotEmpty(host.Invalidations);

        // Create a popup subtree after the session theme was applied
        var popupRoot = new StandardButton { Text = "Popup Action" };
        var popupLabel = StandardLabel.Muted("Popup info");
        popupRoot.AddChild(popupLabel);

        // Apply to subtree without specifying a theme; should inherit session theme
        StandardThemeController.ApplyToSubtree(popupRoot, StandardThemeTokens.Dark);
        Assert.Equal(StandardThemeTokens.Dark.Surface, popupRoot.Background);
        Assert.Equal(StandardThemeTokens.Dark.TextMuted, popupLabel.Foreground);
    }

    [Fact]
    public void Independent_Session_Theme_Isolation_Without_Cross_Window_Mutation()
    {
        using UiSession sessionA = CreateSession(out _);
        using UiSession sessionB = CreateSession(out _);

        var buttonA = new StandardButton { Text = "Window A" };
        var buttonB = new StandardButton { Text = "Window B" };
        sessionA.AddRoot(buttonA);
        sessionB.AddRoot(buttonB);

        // Apply Dark to session A and Light to session B
        StandardThemeController.Apply(sessionA, StandardThemeTokens.Dark);
        StandardThemeController.Apply(sessionB, StandardThemeTokens.Light);

        // Verify session-scoped theme retrieval
        Assert.Same(StandardThemeTokens.Dark, StandardControlPaint.GetTheme(sessionA));
        Assert.Same(StandardThemeTokens.Light, StandardControlPaint.GetTheme(sessionB));

        // Elements resolve their own session's theme
        Assert.Same(StandardThemeTokens.Dark, StandardControlPaint.GetTheme(buttonA));
        Assert.Same(StandardThemeTokens.Light, StandardControlPaint.GetTheme(buttonB));

        Assert.Equal(StandardThemeTokens.Dark.Surface, buttonA.Background);
        Assert.Equal(StandardThemeTokens.Light.Surface, buttonB.Background);

        // Clean up session theme
        StandardControlPaint.ClearSessionTheme(sessionA);
        StandardControlPaint.ClearSessionTheme(sessionB);
    }

    [Fact]
    public void Concurrent_Multi_Threaded_Session_Theme_Access_Is_ThreadSafe()
    {
        const int sessionCount = 16;
        var sessions = new List<UiSession>();
        for (int i = 0; i < sessionCount; i++)
        {
            sessions.Add(CreateSession(out _));
        }

        try
        {
            Parallel.For(0, sessionCount, i =>
            {
                UiSession s = sessions[i];
                StandardThemeTokens chosen = (i % 2 == 0) ? StandardThemeTokens.Dark : StandardThemeTokens.Light;
                StandardControlPaint.SetSessionTheme(s, chosen);
                Assert.Same(chosen, StandardControlPaint.GetTheme(s));
            });
        }
        finally
        {
            foreach (UiSession s in sessions)
            {
                StandardControlPaint.ClearSessionTheme(s);
                s.Dispose();
            }
        }
    }

    [Fact]
    public void Color_Contrast_Measurements_Across_All_Presets()
    {
        StandardThemeTokens[] presets =
        [
            StandardThemeTokens.Light,
            StandardThemeTokens.Dark,
            StandardThemeTokens.HighContrastLight,
            StandardThemeTokens.HighContrastDark,
        ];

        foreach (StandardThemeTokens preset in presets)
        {
            // Text on Surface must meet WCAG AA normal text (>= 4.5:1)
            Assert.True(preset.MeetsAaNormalText, $"{preset.Name} failed MeetsAaNormalText: ratio was {preset.TextContrast:F2}");
            Assert.True(preset.TextContrast >= StandardContrast.AaNormalText,
                $"{preset.Name} text contrast {preset.TextContrast:F2} is below {StandardContrast.AaNormalText}");

            // Accent text (OnAccent on Accent) must meet WCAG AA normal text (>= 4.5:1)
            Assert.True(preset.AccentContrast >= StandardContrast.AaNormalText,
                $"{preset.Name} accent contrast {preset.AccentContrast:F2} is below {StandardContrast.AaNormalText}");

            // Focus ring must meet WCAG AA large/UI (>= 3.0:1) against surface
            Assert.True(preset.FocusRingContrast >= StandardContrast.AaLargeOrUi,
                $"{preset.Name} focus ring contrast {preset.FocusRingContrast:F2} is below {StandardContrast.AaLargeOrUi}");
        }

        // High contrast themes must achieve higher contrast
        Assert.True(StandardThemeTokens.HighContrastLight.TextContrast >= StandardContrast.AaaNormalText);
        Assert.True(StandardThemeTokens.HighContrastDark.TextContrast >= StandardContrast.AaaNormalText);
        Assert.Equal(2, StandardThemeTokens.HighContrastLight.FocusRingThickness);
        Assert.Equal(2, StandardThemeTokens.HighContrastDark.FocusRingThickness);
    }

    [Fact]
    public void Typography_Tokens_Provide_Semantic_Roles_And_Convenience_Factories()
    {
        StandardThemeTokens tokens = StandardThemeTokens.Light;

        Assert.NotNull(tokens.FontFamily);
        Assert.NotNull(tokens.FontBody);
        Assert.NotNull(tokens.FontTitle);
        Assert.NotNull(tokens.FontSubtitle);
        Assert.NotNull(tokens.FontCaption);
        Assert.NotNull(tokens.FontCode);

        Assert.True(tokens.FontTitle.Size > tokens.FontBody.Size);
        Assert.True(tokens.FontSubtitle.Size >= tokens.FontBody.Size);
        Assert.True(tokens.FontCaption.Size < tokens.FontBody.Size);
        Assert.Equal("Consolas", tokens.FontCode.FamilyName);

        // Convenience factories on StandardLabel
        var title = StandardLabel.Title("Heading 1");
        var subtitle = StandardLabel.Subtitle("Heading 2");
        var caption = StandardLabel.Caption("Small note");
        var code = StandardLabel.Code("var x = 42;");

        Assert.Equal(StandardControlPaint.FontTitle, title.Font);
        Assert.Equal(StandardControlPaint.FontSubtitle, subtitle.Font);
        Assert.Equal(StandardControlPaint.FontCaption, caption.Font);
        Assert.Equal(StandardControlPaint.FontCode, code.Font);
        Assert.Equal(StandardLabelRole.Muted, caption.Role);
    }

    [Fact]
    public void Spacing_Tokens_Follow_Scale()
    {
        StandardThemeTokens tokens = StandardThemeTokens.Light;

        Assert.Equal(4, tokens.SpacingXs);
        Assert.Equal(8, tokens.SpacingSm);
        Assert.Equal(12, tokens.SpacingMd);
        Assert.Equal(16, tokens.SpacingLg);
        Assert.Equal(24, tokens.SpacingXl);
        Assert.Equal(32, tokens.SpacingXxl);

        Assert.Equal(0, tokens.Spacing(0));
        Assert.Equal(tokens.SpacingXs, tokens.Spacing(1));
        Assert.Equal(tokens.SpacingSm, tokens.Spacing(2));
        Assert.Equal(tokens.SpacingMd, tokens.Spacing(3));
        Assert.Equal(tokens.SpacingLg, tokens.Spacing(4));
        Assert.Equal(tokens.SpacingXl, tokens.Spacing(5));
        Assert.Equal(tokens.SpacingXxl, tokens.Spacing(6));
        Assert.Equal(tokens.SpacingXxl + tokens.SpacingSm, tokens.Spacing(7));
    }

    [Theory]
    [InlineData(UiDensity.Compact, 0.8, 20.0, 16.0)]
    [InlineData(UiDensity.Comfortable, 1.0, 20.0, 20.0)]
    [InlineData(UiDensity.Spacious, 1.25, 20.0, 25.0)]
    public void Density_Tokens_Resolve_Row_Height_And_Padding(
        UiDensity density, double expectedFactor, double baseHeight, double expectedResolvedHeight)
    {
        var tokens = StandardThemeTokens.Light with { Density = density };

        Assert.Equal(expectedFactor, tokens.DensityFactor);
        Assert.Equal(expectedResolvedHeight, tokens.ResolveRowHeight(baseHeight));

        var (h, v) = tokens.ResolvePadding(10, 5);
        Assert.Equal(Math.Round(10 * expectedFactor), h);
        Assert.Equal(Math.Round(5 * expectedFactor), v);
    }

    [Fact]
    public void Motion_Policy_Respects_Reduced_Motion()
    {
        var normalMotion = StandardThemeTokens.Light with { ReducedMotion = false };
        var reducedMotion = StandardThemeTokens.Light with { ReducedMotion = true };

        Assert.False(normalMotion.ReducedMotion);
        Assert.True(reducedMotion.ReducedMotion);

        Assert.True(normalMotion.AnimationDurationFast > TimeSpan.Zero);
        Assert.True(normalMotion.AnimationDurationNormal > TimeSpan.Zero);
        Assert.True(normalMotion.AnimationDurationSlow > TimeSpan.Zero);

        Assert.Equal(TimeSpan.Zero, reducedMotion.AnimationDurationFast);
        Assert.Equal(TimeSpan.Zero, reducedMotion.AnimationDurationNormal);
        Assert.Equal(TimeSpan.Zero, reducedMotion.AnimationDurationSlow);
    }

    [Fact]
    public void System_Settings_Host_Integration_And_Change_Notifications()
    {
        var host = new SettingsThemeHost(
            new BSize(200, 200),
            new UiSystemSettings(
                UiContrastPreference.More,
                1.5,
                ReducedMotion: true,
                UiFlowDirection.LeftToRight,
                UiColorScheme.Dark,
                UiDensity.Compact));

        using UiSession session = new StandardUiSessionBuilder()
            .WithDispatcher(new ImmediateUiDispatcher())
            .WithClock(new FixedClock())
            .Build(host);

        var label = StandardLabel.Warning("Low Memory");
        session.AddRoot(label);

        // Apply without explicit arguments queries host settings
        StandardThemeController.Apply(session);

        StandardThemeTokens current = StandardControlPaint.GetTheme(session);
        Assert.True(current.IsDark);
        Assert.Equal(UiDensity.Compact, current.Density);
        Assert.True(current.ReducedMotion);
        Assert.Equal(StandardThemeTokens.HighContrastDark.Name, current.Name);
        Assert.Equal(StandardThemeTokens.HighContrastDark.Warning, label.Foreground);

        // Now test host event firing
        bool eventFired = false;
        host.SettingsChanged += (sender, args) =>
        {
            eventFired = true;
            StandardThemeController.Apply(session, args.Settings);
        };

        var nextSettings = new UiSystemSettings(
            UiContrastPreference.NoPreference,
            1.0,
            ReducedMotion: false,
            UiFlowDirection.LeftToRight,
            UiColorScheme.Light,
            UiDensity.Spacious);

        host.NotifySettingsChanged(nextSettings);

        Assert.True(eventFired);
        StandardThemeTokens updated = StandardControlPaint.GetTheme(session);
        Assert.False(updated.IsDark);
        Assert.Equal(UiDensity.Spacious, updated.Density);
        Assert.False(updated.ReducedMotion);
        Assert.Equal(StandardThemeTokens.Light.Name, updated.Name);
        Assert.Equal(StandardThemeTokens.Light.Warning, label.Foreground);
    }

    private static UiSession CreateSession(out SettingsThemeHost host)
    {
        host = new SettingsThemeHost(new BSize(120, 80));
        return new StandardUiSessionBuilder()
            .WithDispatcher(new ImmediateUiDispatcher())
            .WithClock(new FixedClock())
            .Build(host);
    }

    private sealed class SettingsThemeHost(BSize viewportSize, UiSystemSettings? settings = null) : IUiHost, IUiSystemSettingsHost
    {
        public BSize ViewportSize { get; } = viewportSize;
        public double Scale => 1.0;
        public List<UiInvalidation> Invalidations { get; } = [];
        public UiSystemSettings Settings { get; set; } = settings ?? UiSystemSettings.Default;

        public event EventHandler<UiSystemSettingsChangedEventArgs>? SettingsChanged;

        public void NotifySettingsChanged(UiSystemSettings newSettings)
        {
            Settings = newSettings;
            SettingsChanged?.Invoke(this, new UiSystemSettingsChangedEventArgs(newSettings));
        }

        public BRenderList CreateRenderList(int capacity = 0) => new(capacity);
        public void Invalidate(UiInvalidation invalidation) => Invalidations.Add(invalidation);
        public void Present(BRenderList renderList) { }
    }

    private sealed class FixedClock : IUiClock
    {
        public UiTimestamp Now => default;
    }
}
