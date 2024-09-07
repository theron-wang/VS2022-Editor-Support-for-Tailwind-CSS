﻿using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

namespace TailwindCSSIntellisense.QuickInfo;

[Export(typeof(IAsyncQuickInfoSourceProvider))]
[Name("CSS Async Quick Info Provider")]
[ContentType("css")]
internal sealed class CssQuickInfoSourceProvider : IAsyncQuickInfoSourceProvider
{
    [Import]
    public DescriptionGenerator DescriptionGenerator { get; set; }

    public IAsyncQuickInfoSource TryCreateQuickInfoSource(ITextBuffer textBuffer)
    {
        return textBuffer.Properties.GetOrCreateSingletonProperty(() => new CssQuickInfoSource(textBuffer, DescriptionGenerator));
    }
}
