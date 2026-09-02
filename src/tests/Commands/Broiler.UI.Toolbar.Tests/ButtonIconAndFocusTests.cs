using Broiler.Graphics;
using Broiler.Input;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.UI.Button.Standard;
using Broiler.UI.Standard;
using Broiler.UI.ToggleButton.Standard;

namespace Broiler.UI.Toolbar.Tests;

/// <summary>
/// Coverage for a button that draws a picture instead of a word, and for a focus ring that only
/// appears when the keyboard put it there.
/// </summary>
public sealed class ButtonIconAndFocusTests
{
    [Fact(Timeout = 600000)]
    public void An_Icon_Button_Draws_Its_Icon_Instead_Of_Its_Caption()
    {
        var host = new TestHost(new BSize(200, 80));
        using UiSession session = CreateSession(host);
        var button = new StandardButton
        {
            Text = "Save",
            ToolTipText = "Save (Ctrl+S)",
            PreferredSize = new BSize(30, 30),
            IconPainter = static (list, box, color) => list.FillRect(box, color),
        };
        session.AddRoot(button);

        BRenderList rendered = session.RenderFrame();

        // The caption is gone from the frame but not from the control: it is still the name the
        // button answers to, which is what keeps an icon-only bar usable without sight of it.
        Assert.DoesNotContain(rendered.Commands.OfType<BRenderCommand.DrawText>(), command => command.Text.Text == "Save");
        Assert.Equal("Save", button.GetSemanticNode().Name);
        Assert.Equal("Save (Ctrl+S)", button.ToolTipText);
    }

    [Fact(Timeout = 600000)]
    public void An_Icon_Button_Is_Measured_By_Its_Icon_Not_Its_Caption()
    {
        var host = new TestHost(new BSize(400, 80));
        using UiSession session = CreateSession(host);
        // PreferredSize is a floor rather than a cap, so it is zeroed here: what is under test is
        // what the content contributes, and the default floor would hide the difference entirely.
        var captioned = new StandardButton
        {
            Text = "Strikethrough",
            PaddingX = 6,
            PreferredSize = BSize.Empty,
        };
        var icon = new StandardButton
        {
            Text = "Strikethrough",
            PaddingX = 6,
            IconExtent = 16,
            PreferredSize = BSize.Empty,
            IconPainter = static (list, box, color) => list.FillRect(box, color),
        };

        BSize captionedSize = captioned.Measure(new BSize(400, 80));
        BSize iconSize = icon.Measure(new BSize(400, 80));

        // A long caption must not keep making the button wide once the caption stopped being drawn.
        Assert.True(
            iconSize.Width < captionedSize.Width,
            $"Expected the icon button to be narrower than the captioned one; got {iconSize.Width} and {captionedSize.Width}.");
        Assert.Equal(16 + (6 * 2), iconSize.Width, 3);
    }

    [Fact(Timeout = 600000)]
    public void The_Icon_Takes_The_Colour_The_Button_Resolved()
    {
        var host = new TestHost(new BSize(200, 80));
        using UiSession session = CreateSession(host);
        var button = new StandardButton
        {
            Text = "Save",
            Foreground = BColor.FromArgb(0xFF, 0x11, 0x22, 0x33),
            DisabledForeground = BColor.FromArgb(0xFF, 0x99, 0x99, 0x99),
            PreferredSize = new BSize(30, 30),
            IconPainter = static (list, box, color) => list.FillRect(box, color),
        };
        session.AddRoot(button);

        Assert.Contains(session.RenderFrame().Commands.OfType<BRenderCommand.FillRect>(), c => c.Color == button.Foreground);

        button.IsEnabled = false;
        Assert.Contains(session.RenderFrame().Commands.OfType<BRenderCommand.FillRect>(), c => c.Color == button.DisabledForeground);
    }

    [Fact(Timeout = 600000)]
    public void A_Toggle_Button_Takes_An_Icon_Too()
    {
        var host = new TestHost(new BSize(200, 80));
        using UiSession session = CreateSession(host);
        var toggle = new StandardToggleButton
        {
            Text = "Bold",
            PreferredSize = new BSize(30, 30),
            IconPainter = static (list, box, color) => list.FillRect(box, color),
        };
        session.AddRoot(toggle);

        BRenderList rendered = session.RenderFrame();

        Assert.DoesNotContain(rendered.Commands.OfType<BRenderCommand.DrawText>(), command => command.Text.Text == "Bold");
        Assert.Equal("Bold", toggle.GetSemanticNode().Name);
    }

    [Fact(Timeout = 600000)]
    public void A_Click_Focuses_Without_Drawing_A_Ring()
    {
        var host = new TestHost(new BSize(200, 80));
        using UiSession session = CreateSession(host);
        var button = new StandardButton { Text = "Save", FocusRing = BColor.FromArgb(0xFF, 0x2A, 0x73, 0xC5) };
        session.AddRoot(button);
        session.RenderFrame();

        session.DispatchInput(PointerDown(new BPoint(10, 10)));

        Assert.Same(button, session.FocusedElement);
        Assert.False(session.IsFocusVisible);
        Assert.DoesNotContain(
            session.RenderFrame().Commands.OfType<BRenderCommand.StrokeRoundedRect>(),
            command => command.Color == button.FocusRing);
    }

    [Fact(Timeout = 600000)]
    public void A_Key_Press_Brings_The_Ring_Back()
    {
        var host = new TestHost(new BSize(200, 80));
        using UiSession session = CreateSession(host);
        var button = new StandardButton { Text = "Save", FocusRing = BColor.FromArgb(0xFF, 0x2A, 0x73, 0xC5) };
        session.AddRoot(button);
        session.RenderFrame();

        session.DispatchInput(PointerDown(new BPoint(10, 10)));
        Assert.False(session.IsFocusVisible);

        session.DispatchInput(UiInputEvent.FromKeyboardKey(Key("Tab", BVirtualKey.Tab)));

        Assert.True(session.IsFocusVisible);
        Assert.Contains(
            session.RenderFrame().Commands.OfType<BRenderCommand.StrokeRoundedRect>(),
            command => command.Color == button.FocusRing);
    }

    [Fact(Timeout = 600000)]
    public void A_Session_That_Has_Seen_No_Input_Still_Rings_Its_Focus()
    {
        var host = new TestHost(new BSize(200, 80));
        using UiSession session = CreateSession(host);
        var button = new StandardButton { Text = "Save", FocusRing = BColor.FromArgb(0xFF, 0x2A, 0x73, 0xC5) };
        session.AddRoot(button);
        session.SetFocus(button);

        // A keyboard-only session starts here, and a ring is the safer thing to be wrong about.
        Assert.True(session.IsFocusVisible);
        Assert.Contains(
            session.RenderFrame().Commands.OfType<BRenderCommand.StrokeRoundedRect>(),
            command => command.Color == button.FocusRing);
    }

    private static UiSession CreateSession(TestHost host) =>
        new StandardUiSessionBuilder()
            .WithDispatcher(new ImmediateUiDispatcher())
            .Build(host);

    private static UiInputEvent PointerDown(BPoint position) =>
        UiInputEvent.FromMouseButton(
            new MouseButtonEvent(
                new InputEventHeader(InputDeviceId.FromOpaqueValue("mouse"), new InputTimestamp(1, TimeSpan.TicksPerSecond, "button"), 1),
                InputPoint.ClientDeviceIndependentPixels(position.X, position.Y),
                MouseButtons.Left,
                MouseButton.Left,
                MouseButtonTransition.Down,
                InputEventSource.Synthetic));

    private static KeyboardKeyEvent Key(string name, int nativeKeyCode) =>
        new(
            new InputEventHeader(InputDeviceId.FromOpaqueValue("keyboard"), new InputTimestamp(1, TimeSpan.TicksPerSecond, "button"), 1),
            KeyboardKey.FromName(name),
            KeyboardKeyTransition.Down,
            KeyboardModifierState.None,
            nativeKeyCode,
            0,
            0,
            false,
            false,
            Source: InputEventSource.Synthetic);

    private sealed class TestHost(BSize viewportSize) : IUiHost
    {
        public BSize ViewportSize { get; } = viewportSize;

        public double Scale => 1;

        public BRenderList CreateRenderList(int capacity = 0) => new(capacity);

        public void Invalidate(UiInvalidation invalidation)
        {
        }

        public void Present(BRenderList renderList)
        {
        }
    }
}
