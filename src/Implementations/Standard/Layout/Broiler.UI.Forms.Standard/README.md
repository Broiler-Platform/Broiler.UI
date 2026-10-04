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

Use `FormSection(..., collapsible: true, expanded: false)` for optional fields. Its
`Summary` remains visible when collapsed. Collapsing retains values and returns focus
to the disclosure when necessary. Applications should populate a useful summary when
hidden values affect the operation. The disclosure button reports Expanded or Collapsed
and points at the section through `UiElement.Discloses`, so a host can offer its
expand/collapse pattern where the focus is; the section group reports neither state.
`FormSection` implements `IUiExpandable` (`Expand`, `Collapse`). See ADR 0028.

`InlineFeedback.Set` updates literal text, semantic status, theme-colored decoration,
and session announcements. Native hosts still need to map semantic events into their
accessibility bridge and apply visibility/announcement policy. Use a real application
cancellation command beside the actions; a progress banner creates no task or timer.

`FormSurface` allocates up to 112 DIP (at most one quarter of its height) to feedback;
long feedback scrolls separately. Form content is constrained to the viewport width.
An unconstrained measure uses a finite 640×480 fallback. The persistent action bar is
intended for application-sized viewports; products must choose a practical minimum
size for their action labels and fonts.

Tests are in `CompactFormsTests` in `Broiler.UI.Standard.Tests`. Native UI Automation,
full RTL layout, OS text-scale integration, and public package release are separate
integration checks, not implied by the neutral semantic/layout tests.
