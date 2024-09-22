using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using TailwindCSSIntellisense.Completions;

namespace TailwindCSSIntellisense.QuickInfo;

[Export(typeof(IAsyncQuickInfoSourceProvider))]
[Name("Razor Async Quick Info Provider")]
[ContentType("razor")]
[ContentType("LegacyRazorCSharp")]
[ContentType("LegacyRazor")]
[ContentType("LegacyRazorCoreCSharp")]
internal sealed class RazorQuickInfoSourceProvider : IAsyncQuickInfoSourceProvider
{
    [Import]
    public DescriptionGenerator DescriptionGenerator { get; set; }

    [Import]
    public CompletionUtilities CompletionUtilities { get; set; }

    public IAsyncQuickInfoSource TryCreateQuickInfoSource(ITextBuffer textBuffer)
    {
        return textBuffer.Properties.GetOrCreateSingletonProperty(() => new RazorQuickInfoSource(textBuffer, DescriptionGenerator, CompletionUtilities));
    }
}