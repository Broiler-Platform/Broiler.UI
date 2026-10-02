using Broiler.UI;
using Broiler.UI.RichEdit.Standard;

namespace Broiler.UI.RichEdit.Standard.Tests;

/// <summary>The document is a rich edit's value; its name comes from a label, an explicit name, or the placeholder.</summary>
public sealed class RichEditAccessibleNameTests
{
    [Fact]
    public void DocumentIsTheValueAndNeverTheName()
    {
        var reader = new StandardRichEdit { IsReadOnly = true };
        reader.SetPlainText("A long message body that must not be read out as the control's name.");
        UiSemanticNode node = reader.GetSemanticNode();
        Assert.Equal("", node.Name);
        Assert.Equal("A long message body that must not be read out as the control's name.", node.TextInfo?.Value);

        reader.AccessibleName = "Message text";
        Assert.Equal("Message text", reader.GetSemanticNode().Name);

        var composer = new StandardRichEdit { PlaceholderText = "Write your message" };
        composer.SetPlainText("Draft");
        Assert.Equal("Write your message", composer.GetSemanticNode().Name);
        Assert.Equal("Draft", composer.GetSemanticNode().TextInfo?.Value);
    }
}
