using System;

namespace Broiler.UI.SpinBox.Standard;

public sealed class StandardSpinBoxFactory : IUiElementFactory
{
    public Type ContractType => typeof(UiSpinBox);

    public UiElement Create(UiElementFactoryContext context) => new StandardSpinBox();
}
