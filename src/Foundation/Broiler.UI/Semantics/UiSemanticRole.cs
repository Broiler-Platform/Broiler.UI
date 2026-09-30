namespace Broiler.UI;

public enum UiSemanticRole
{
    Generic = 0,
    Window,
    Panel,
    Label,
    Button,
    Edit,
    CheckBox,
    RadioButton,
    ToggleButton,
    Slider,
    ProgressBar,
    ImageView,
    ScrollView,
    ListView,
    ComboBox,
    TabView,
    Menu,
    Tooltip,
    Dialog,
    Toolbar,
    RichEdit,
    FormatCodeView,
    Splitter,

    /// <summary>
    /// A source editor. Unlike <see cref="Edit"/> and <see cref="RichEdit"/>,
    /// a node with this role does not carry its text in
    /// <see cref="UiSemanticTextInfo.Value"/> — the document can be tens of
    /// megabytes. It exposes <see cref="IUiVirtualizedTextProvider"/> instead,
    /// and every read is bounded. See Broiler.UI ADR 0022.
    /// </summary>
    CodeEditor,

    /// <summary>
    /// A number that is typed or stepped. Distinct from <see cref="Slider"/>, which has no text of
    /// its own, and from <see cref="Edit"/>, which has no range or step to report.
    /// </summary>
    SpinBox,

    /// <summary>An individual item within a list view or collection.</summary>
    ListItem,

    /// <summary>A tab header or tab page within a tab view.</summary>
    TabItem,

    /// <summary>An actionable command item within a menu or menu bar.</summary>
    MenuItem,

    /// <summary>A live region or status announcement that communicates changes to assistive technology.</summary>
    StatusAnnouncement,

    /// <summary>A logical grouping of controls or form fields.</summary>
    Group,

    /// <summary>An actionable navigation link.</summary>
    Hyperlink,
}
