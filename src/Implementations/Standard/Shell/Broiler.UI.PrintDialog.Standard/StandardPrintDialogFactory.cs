using System;

namespace Broiler.UI.PrintDialog.Standard;

public sealed class StandardPrintDialogFactory : IUiElementFactory
{
    public Type ContractType => typeof(UiPrintDialog);

    public UiElement Create(UiElementFactoryContext context) => new StandardPrintDialog();
}
