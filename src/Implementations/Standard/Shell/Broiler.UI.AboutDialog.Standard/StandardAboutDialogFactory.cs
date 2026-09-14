using System;

namespace Broiler.UI.AboutDialog.Standard;

public sealed class StandardAboutDialogFactory : IUiElementFactory
{
    public Type ContractType => typeof(UiAboutDialog);

    public UiElement Create(UiElementFactoryContext context) => new StandardAboutDialog();
}
