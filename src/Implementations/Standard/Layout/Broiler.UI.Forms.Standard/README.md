# Compact forms

Reusable compositions of the standard label, edit, button, panel, toolbar, and scroll
controls. No application validation rules, asynchronous operations, or persistence
are implemented here.

```csharp
var name = new StandardEdit();
var field = new FormField("Display name", name, "Shown in the account list.");
var section = new FormSection("Identity");
section.Content.AddChild(field);
var save = new StandardButton { Text = "Save", IsDefault = true };
var feedback = new InlineFeedback();
var surface = new FormSurface(section, FormSurface.ActionBar(save), feedback);

// Application validation owns these calls:
field.SetError("Enter a display name.");
feedback.Set("Review the highlighted field.", FeedbackKind.Error);
surface.Reveal(field);
```

The control, not the field group, carries the field's semantics because it is what takes
focus: the label names it, the description is its `DescribedBy`, and while `SetError`
shows a message that message is its `ErrorMessage`, so the control reports Invalid and
its semantic `Description` starts with the error. `FormField.IsRequired` sets the
control's Required state. A field error is not announced; the form's status is.

Use `FormSection(..., collapsible: true, expanded: false)` for optional fields. Its
`Summary` remains visible when collapsed. Collapsing retains values and returns focus
to the disclosure when necessary. Applications should populate a useful summary when
hidden values affect the operation. The disclosure button reports Expanded or Collapsed
and points at the section through `UiElement.Discloses`, so a host can offer its
expand/collapse pattern where the focus is; the section group reports neither state.
Its `UiElement.Controls` is the section's `Content`, which a host exposes as the element
the button controls while it is shown. `FormSection` implements `IUiExpandable`
(`Expand`, `Collapse`). See ADR 0028.

The disclosure button says "Show " or "Hide " and the section's title, which keeps a
sentence-case title's capital in the middle of the phrase ("Show Keyboard shortcuts").
Set `ShowText` and `HideText` to give it your own text, such as "Show keyboard
shortcuts"; null or blank composes it again. The text is the button's accessible name,
and its state and relations are the same either way. See ADR 0031.

`InlineFeedback.Set` updates literal text, semantic status, theme-colored decoration,
and session announcements. Native hosts still need to map semantic events into their
accessibility bridge and apply visibility/announcement policy. Use a real application
cancellation command beside the actions; a progress banner creates no task or timer.

`FormSurface` allocates up to 112 DIP (at most one quarter of its height) to feedback;
long feedback scrolls separately. Form content is constrained to the viewport width,
less 1 DIP on each side (`StandardScrollView.HorizontalContentInset`): an edit strokes
its 2 DIP focus ring centered on its edge, and a field as wide as the form would
otherwise lose the outer half of the ring's sides to the viewport's clip. In a
`FormSurface` the viewports reach 1 DIP into the 12 DIP margins, so fields and banners
keep the action strip's edges. See ADR 0034.
An unconstrained measure uses a finite 640×480 fallback. The persistent action bar is
intended for application-sized viewports; products must choose a practical minimum
size for their action labels and fonts.

Tests are in `CompactFormsTests` in `Broiler.UI.Standard.Tests`. Native UI Automation,
full RTL layout, OS text-scale integration, and public package release are separate
integration checks, not implied by the neutral semantic/layout tests.
