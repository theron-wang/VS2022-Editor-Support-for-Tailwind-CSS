﻿using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using TailwindCSSIntellisense.Completions;
using TailwindCSSIntellisense.Configuration;

namespace TailwindCSSIntellisense.QuickInfo;

[Export(typeof(IAsyncQuickInfoSourceProvider))]
[Name("CSS Async Quick Info Provider")]
[ContentType("css")]
internal sealed class CssQuickInfoSourceProvider : IAsyncQuickInfoSourceProvider
{
    [Import]
    public DescriptionGenerator DescriptionGenerator { get; set; } = null!;

    [Import]
    public ProjectConfigurationManager ProjectConfigurationManager { get; set; } = null!;

    [Import]
    public CompletionConfiguration CompletionConfiguration { get; set; } = null!;

    public IAsyncQuickInfoSource TryCreateQuickInfoSource(ITextBuffer textBuffer)
    {
        return textBuffer.Properties.GetOrCreateSingletonProperty(() => new CssQuickInfoSource(textBuffer, DescriptionGenerator, ProjectConfigurationManager, CompletionConfiguration));
    }
}
