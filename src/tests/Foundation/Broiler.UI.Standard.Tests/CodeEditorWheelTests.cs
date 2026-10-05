using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;
using Broiler.Input;
using Broiler.Input.Mouse;
using Broiler.UI.CodeEditor;
using Broiler.UI.CodeEditor.Standard;

namespace Broiler.UI.Standard.Tests;

/// <summary>
/// Which way the wheel moves a code editor's long lines. A wheel tilted right scrolls right; Shift
/// with the wheel turned towards the user scrolls right too, however the host reports it.
/// </summary>
[Collection(GlobalThemeCollection.Name)]
public sealed class CodeEditorWheelTests
{
    [Theory]
    [InlineData(MouseWheelAxis.Vertical)]
    [InlineData(MouseWheelAxis.Horizontal)]
    public void Shift_With_The_Wheel_Turned_Towards_The_User_Scrolls_Right_However_The_Host_Reports_It(MouseWheelAxis axis)
    {
        // Broiler.Input and Broiler.Graphics report Shift with the wheel as a vertical notch with Shift;
        // Broiler.Hosting.Windows turns it into a horizontal one that keeps Shift and the vertical sign.
        using UiSession session = Create(out StandardCodeEditor editor);

        Assert.True(editor.DispatchInput(Wheel(-1, axis, InputModifiers.Shift)));
        double right = editor.Viewport.HorizontalOffset;
        Assert.True(right > 0);
        Assert.Equal(0, editor.Viewport.FirstVisibleLine);

        Assert.True(editor.DispatchInput(Wheel(1, axis, InputModifiers.Shift)));
        Assert.Equal(0, editor.Viewport.HorizontalOffset);
        Assert.Equal(0, editor.Viewport.FirstVisibleLine);
    }

    [Fact]
    public void A_Wheel_Tilted_Right_Scrolls_Right()
    {
        using UiSession session = Create(out StandardCodeEditor editor);

        Assert.True(editor.DispatchInput(Wheel(1, MouseWheelAxis.Horizontal, InputModifiers.None)));
        Assert.True(editor.Viewport.HorizontalOffset > 0);
        Assert.True(editor.DispatchInput(Wheel(-1, MouseWheelAxis.Horizontal, InputModifiers.None)));
        Assert.Equal(0, editor.Viewport.HorizontalOffset);
    }

    private static UiSession Create(out StandardCodeEditor editor)
    {
        string text = string.Join('\n', Enumerable.Range(0, 40).Select(line => $"line {line} " + new string('x', 160)));
        editor = new StandardCodeEditor { PreferredSize = new BSize(240, 160), Document = new ReadOnlyDocument(text) };
        UiSession session = new StandardUiSessionBuilder().Build(new Host(new BSize(240, 160)));
        session.AddRoot(editor);

        // Two frames: the first learns how wide the lines are while drawing them, and the second
        // is laid out knowing.
        session.RenderFrame();
        session.RenderFrame();
        Assert.True(editor.HasHorizontalScrollbar, "The test needs lines wider than the editor.");
        return session;
    }

    private static UiInputEvent Wheel(double notches, MouseWheelAxis axis, InputModifiers modifiers) =>
        UiInputEvent.FromMouseWheel(new MouseWheelEvent(
            new InputEventHeader(InputDeviceId.FromOpaqueValue("mouse"), new InputTimestamp(1, TimeSpan.TicksPerSecond, "code-editor"), 1),
            InputPoint.ClientDeviceIndependentPixels(120, 60),
            MouseButtons.None,
            axis,
            notches,
            InputEventSource.Synthetic,
            modifiers));

    private sealed class ReadOnlyDocument(string text) : ICodeDocument
    {
        public ICodeTextSnapshot Snapshot { get; } = new TextSnapshot(text);
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

    private sealed class TextSnapshot : ICodeTextSnapshot
    {
        private readonly string _text;
        private readonly int[] _lineStarts;

        public TextSnapshot(string text)
        {
            _text = text;
            var starts = new List<int> { 0 };
            for (int index = 0; index < text.Length; index++)
            {
                if (text[index] == '\n')
                    starts.Add(index + 1);
            }
            _lineStarts = [.. starts];
        }

        public int Version => 1;
        public int Length => _text.Length;
        public int LineCount => _lineStarts.Length;
        public int GetLineStart(int line) => _lineStarts[line];

        public int GetLineLength(int line) =>
            (line + 1 < _lineStarts.Length ? _lineStarts[line + 1] - 1 : _text.Length) - _lineStarts[line];

        public int GetLineFromPosition(int position)
        {
            int index = Array.BinarySearch(_lineStarts, position);
            return index >= 0 ? index : ~index - 1;
        }

        public string GetText(int start, int length) => _text.Substring(start, length);
        public void CopyTo(int start, int length, Span<char> destination) => _text.AsSpan(start, length).CopyTo(destination);
        public int GetNextCaretPosition(int position) => Math.Min(_text.Length, position + 1);
        public int GetPreviousCaretPosition(int position) => Math.Max(0, position - 1);
    }

    private sealed class Host(BSize size) : IUiHost
    {
        public BSize ViewportSize { get; } = size;
        public double Scale => 1;
        public BRenderList CreateRenderList(int capacity = 0) => new(capacity);
        public void Invalidate(UiInvalidation invalidation) { }
        public void Present(BRenderList renderList) { }
    }
}
