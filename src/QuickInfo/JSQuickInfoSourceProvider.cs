using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using TailwindCSSIntellisense.Completions;

namespace TailwindCSSIntellisense.QuickInfo;

[Export(typeof(IAsyncQuickInfoSourceProvider))]
[Name("JS Async Quick Info Provider")]
[ContentType("JavaScript")]
[ContentType("TypeScript")]
[ContentType("jsx")]
internal sealed class JSQuickInfoSourceProvider : IAsyncQuickInfoSourceProvider
{
    [Import]
    public DescriptionGenerator DescriptionGenerator { get; set; } = null!;

    [Import]
    public ProjectConfigurationManager ProjectConfigurationManager { get; set; } = null!;

    public IAsyncQuickInfoSource TryCreateQuickInfoSource(ITextBuffer textBuffer)
    {
        return textBuffer.Properties.GetOrCreateSingletonProperty(() => new JSQuickInfoSource(textBuffer, DescriptionGenerator, ProjectConfigurationManager));
    }
}
