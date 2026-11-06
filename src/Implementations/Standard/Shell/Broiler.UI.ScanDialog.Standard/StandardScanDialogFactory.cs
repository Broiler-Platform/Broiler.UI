// Copyright (c) 2026 Broiler Platform contributors. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace Broiler.UI.ScanDialog.Standard;

public sealed class StandardScanDialogFactory : IUiElementFactory
{
    public Type ContractType => typeof(UiScanDialog);

    public UiElement Create(UiElementFactoryContext context) => new StandardScanDialog();
}
