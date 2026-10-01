using System;

namespace Broiler.UI.Splitter.Standard;

public sealed class StandardSplitContainerFactory : IUiElementFactory
{
    public Type ContractType => typeof(UiSplitContainer);

    public UiElement Create(UiElementFactoryContext context) => new StandardSplitContainer();
}
