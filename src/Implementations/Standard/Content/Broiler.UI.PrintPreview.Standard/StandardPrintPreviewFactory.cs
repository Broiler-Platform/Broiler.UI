using System;

namespace Broiler.UI.PrintPreview.Standard;

public sealed class StandardPrintPreviewFactory : IUiElementFactory
{
    public Type ContractType => typeof(UiPrintPreview);

    public UiElement Create(UiElementFactoryContext context) => new StandardPrintPreview();
}
