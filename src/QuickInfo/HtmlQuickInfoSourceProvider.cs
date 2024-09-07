using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

namespace TailwindCSSIntellisense.QuickInfo;

[Export(typeof(IAsyncQuickInfoSourceProvider))]
[Name("HTML Async Quick Info Provider")]
[ContentType("html")]
[ContentType("WebForms")]
internal sealed class HtmlQuickInfoSourceProvider : IAsyncQuickInfoSourceProvider
{
    [Import]
    public DescriptionGenerator DescriptionGenerator { get; set; }

    public IAsyncQuickInfoSource TryCreateQuickInfoSource(ITextBuffer textBuffer)
    {
        return textBuffer.Properties.GetOrCreateSingletonProperty(() => new HtmlQuickInfoSource(textBuffer, DescriptionGenerator));
    }
}
