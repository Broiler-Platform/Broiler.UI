using System.Globalization;
using System.Threading;
using Broiler.Graphics;
using Broiler.Input;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.UI.SpinBox.Standard;
using Broiler.UI.Standard;

namespace Broiler.UI.Standard.Tests;

public sealed class SpinBoxControlTests
{
    [Fact(Timeout = 600000)]
    public void Spin_Box_Steps_Within_Its_Range()
    {
        var spin = new StandardSpinBox { Minimum = 1, Maximum = 3, Value = 2 };

        Assert.True(spin.StepUp());
        Assert.Equal(3, spin.Value);

        // At the top there is nothing to do, and saying so lets a caller stop.
        Assert.False(spin.StepUp());
        Assert.Equal(3, spin.Value);

        Assert.True(spin.PageDown());
        Assert.Equal(1, spin.Value);
    }

    [Fact(Timeout = 600000)]
    public void Spin_Box_Clamps_A_Value_Set_Outside_Its_Range()
    {
        var spin = new StandardSpinBox { Minimum = 8, Maximum = 72, Value = 400 };

        Assert.Equal(72, spin.Value);

        spin.Value = 0;

        Assert.Equal(8, spin.Value);
    }

    /// <summary>
    /// The number is rounded to the decimals the box keeps rather than rejected, so the value and
    /// the text the user sees are never two different numbers.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Spin_Box_Rounds_To_The_Decimals_It_Keeps()
    {
        var spin = new StandardSpinBox { Maximum = 100, DecimalPlaces = 1, Value = 10.46 };

        Assert.Equal(10.5, spin.Value);
        Assert.Equal("10.5", spin.ValueText);

        // Half rounds away from zero, the way a price or a measurement does — not to even, which
        // would turn 10.5 into 10 and 11.5 into 12 for no reason a user could predict.
        spin.DecimalPlaces = 0;

        Assert.Equal(11, spin.Value);
        Assert.Equal("11", spin.ValueText);
    }

    /// <summary>A whole number reads as one: a size box shows "16", never "16.0".</summary>
    [Fact(Timeout = 600000)]
    public void Spin_Box_Shows_No_Trailing_Zeros()
    {
        var spin = new StandardSpinBox { Maximum = 100, DecimalPlaces = 2, Value = 16 };

        Assert.Equal("16", spin.ValueText);
    }

    [Fact(Timeout = 600000)]
    public void Spin_Box_Takes_A_Typed_Number()
    {
        var spin = new StandardSpinBox { Maximum = 100 };

        spin.Edit.Text = "42";

        Assert.Equal(42, spin.Value);
    }

    /// <summary>
    /// Half-typed text is not a reason to move the number. Typing towards "42" passes through "4",
    /// and a box that rejected the whole entry — or wrote its own value back over it — would fight
    /// the caret.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Spin_Box_Leaves_Unparsable_Text_Alone()
    {
        var spin = new StandardSpinBox { Maximum = 100, Value = 12 };

        spin.Edit.Text = "not a number";

        Assert.Equal(12, spin.Value);
        Assert.Equal("not a number", spin.Edit.Text);
    }

    /// <summary>
    /// The box writes "12.5" and a German keyboard produces "12,5". Both are the number.
    /// </summary>
    /// <remarks>
    /// The culture is pinned on a thread of this test's own rather than taken from the host, and
    /// spelled out by its separators rather than by a name: a host running under invariant
    /// globalization resolves every culture name to the invariant one, and the point here is a
    /// culture whose decimal separator is a comma. Taking it from the host is what made an earlier
    /// version of this pass on a German machine and fail on CI.
    /// </remarks>
    [Fact(Timeout = 600000)]
    public void Spin_Box_Takes_Either_Decimal_Separator()
    {
        var comma = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        comma.NumberFormat.NumberDecimalSeparator = ",";
        comma.NumberFormat.NumberGroupSeparator = ".";

        Assert.Equal((true, 12.5), ParseUnder(comma, "12.5"));
        Assert.Equal((true, 12.5), ParseUnder(comma, "12,5"));
    }

    [Fact(Timeout = 600000)]
    public void Spin_Box_Steps_From_The_Keyboard()
    {
        var fixture = SpinFixture.Create(value: 5);

        fixture.Session.DispatchInput(KeyDown("Up"));

        Assert.Equal(6, fixture.Spin.Value);

        fixture.Session.DispatchInput(KeyDown("Down"));
        fixture.Session.DispatchInput(KeyDown("Down"));

        Assert.Equal(4, fixture.Spin.Value);
    }

    /// <summary>
    /// Pressing the up arrow steps up, and the focus lands in the text half so that what the user
    /// types next goes where the caret is.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Spin_Box_Steps_From_Its_Arrows()
    {
        var fixture = SpinFixture.Create(value: 5);

        fixture.Session.DispatchInput(LeftDown(Center(fixture.Spin.UpArrowBounds)));

        Assert.Equal(6, fixture.Spin.Value);
        Assert.Same(fixture.Spin.Edit, fixture.Session.FocusedElement);

        fixture.Session.DispatchInput(LeftDown(Center(fixture.Spin.DownArrowBounds)));

        Assert.Equal(5, fixture.Spin.Value);
    }

    /// <summary>A press in the text half is the edit's, not a step.</summary>
    [Fact(Timeout = 600000)]
    public void Spin_Box_Does_Not_Step_From_A_Press_In_The_Text()
    {
        var fixture = SpinFixture.Create(value: 5);

        fixture.Session.DispatchInput(LeftDown(Center(fixture.Spin.Edit.Bounds)));

        Assert.Equal(5, fixture.Spin.Value);
    }

    [Fact(Timeout = 600000)]
    public void Spin_Box_Steps_From_The_Wheel()
    {
        var fixture = SpinFixture.Create(value: 5);

        fixture.Session.DispatchInput(Wheel(Center(fixture.Spin.Bounds), 1));

        Assert.Equal(6, fixture.Spin.Value);

        fixture.Session.DispatchInput(Wheel(Center(fixture.Spin.Bounds), -1));

        Assert.Equal(5, fixture.Spin.Value);
    }

    [Fact(Timeout = 600000)]
    public void Spin_Box_Ignores_Everything_While_Disabled()
    {
        var fixture = SpinFixture.Create(value: 5);
        fixture.Spin.IsEnabled = false;

        fixture.Session.DispatchInput(LeftDown(Center(fixture.Spin.UpArrowBounds)));
        fixture.Session.DispatchInput(KeyDown("Up"));

        Assert.Equal(5, fixture.Spin.Value);
        Assert.False(fixture.Spin.Edit.IsEnabled);
    }

    [Fact(Timeout = 600000)]
    public void Spin_Box_Draws_Both_Arrows()
    {
        var fixture = SpinFixture.Create(value: 5);

        BRenderList renderList = fixture.Session.RenderFrame();

        renderList.Validate();
        Assert.Equal(2, renderList.Commands.OfType<BRenderCommand.FillTriangle>().Count());
    }

    [Fact(Timeout = 600000)]
    public void Spin_Box_Reports_Its_Value_To_Accessibility()
    {
        var spin = new StandardSpinBox { Maximum = 100, Value = 24 };

        UiSemanticNode node = spin.GetSemanticNode();

        Assert.Equal(UiSemanticRole.SpinBox, node.Role);
        Assert.Equal("24", node.Name);
    }

    /// <summary>Parses on a thread of its own, so the culture cannot leak into another test.</summary>
    private static (bool Parsed, double Value) ParseUnder(CultureInfo culture, string text)
    {
        bool parsed = false;
        double value = 0;
        var thread = new Thread(() =>
        {
            CultureInfo.CurrentCulture = culture;
            parsed = UI.SpinBox.UiSpinBox.TryParseValue(text, out value);
        });

        thread.Start();
        thread.Join();
        return (parsed, value);
    }

    private static BPoint Center(BRect bounds) =>
        new(bounds.Left + (bounds.Width / 2), bounds.Top + (bounds.Height / 2));

    private static UiInputEvent LeftDown(BPoint position) =>
        UiInputEvent.FromMouseButton(
            new MouseButtonEvent(
                Header("mouse", 1),
                InputPoint.ClientDeviceIndependentPixels(position.X, position.Y),
                MouseButtons.Left,
                MouseButton.Left,
                MouseButtonTransition.Down,
                InputEventSource.Synthetic));

    private static UiInputEvent Wheel(BPoint position, double notches) =>
        UiInputEvent.FromMouseWheel(
            new MouseWheelEvent(
                Header("mouse", 2),
                InputPoint.ClientDeviceIndependentPixels(position.X, position.Y),
                MouseButtons.None,
                MouseWheelAxis.Vertical,
                notches,
                InputEventSource.Synthetic));

    private static UiInputEvent KeyDown(string name) =>
        UiInputEvent.FromKeyboardKey(
            new KeyboardKeyEvent(
                Header("keyboard", 3),
                KeyboardKey.FromName(name),
                KeyboardKeyTransition.Down,
                KeyboardModifierState.None,
                0,
                0,
                0,
                false,
                false,
                Source: InputEventSource.Synthetic));

    private static InputEventHeader Header(string id, long sequence) =>
        new(
            InputDeviceId.FromOpaqueValue(id),
            new InputTimestamp(sequence, TimeSpan.TicksPerSecond, "spinbox-test"),
            sequence);

    private sealed record SpinFixture(UiSession Session, StandardSpinBox Spin)
    {
        internal static SpinFixture Create(double value)
        {
            UiSession session = new StandardUiSessionBuilder().Build(new TestHost());
            var spin = new StandardSpinBox { Minimum = 0, Maximum = 100, Value = value };
            session.AddRoot(spin);
            spin.Measure(new BSize(200, 40));
            spin.Arrange(new BRect(10, 10, 160, 32));
            session.SetFocus(spin.Edit);
            return new SpinFixture(session, spin);
        }
    }

    private sealed class TestHost : IUiHost
    {
        public BSize ViewportSize { get; } = new(400, 200);

        public double Scale => 1.0;

        public BRenderList CreateRenderList(int capacity = 0) => new(capacity);

        public void Invalidate(UiInvalidation invalidation)
        {
        }

        public void Present(BRenderList renderList)
        {
        }
    }
}
