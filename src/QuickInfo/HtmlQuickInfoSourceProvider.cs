using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using TailwindCSSIntellisense.Completions;

namespace TailwindCSSIntellisense.QuickInfo;

[Export(typeof(IAsyncQuickInfoSourceProvider))]
[Name("HTML Async Quick Info Provider")]
[ContentType("html")]
[ContentType("WebForms")]
internal sealed class HtmlQuickInfoSourceProvider : IAsyncQuickInfoSourceProvider
{
    [Import]
    public DescriptionGenerator DescriptionGenerator { get; set; }

    [Import]
    public ProjectConfigurationManager ProjectConfigurationManager { get; set; }

    public IAsyncQuickInfoSource TryCreateQuickInfoSource(ITextBuffer textBuffer)
    {
        // Handle legacy Razor editor; this completion controller is prioritized but
        // we should only use the Razor completion controller in that case
        if (textBuffer.IsLegacyRazorEditor())
        {
            return null;
        }
        return textBuffer.Properties.GetOrCreateSingletonProperty(() => new HtmlQuickInfoSource(textBuffer, DescriptionGenerator, ProjectConfigurationManager));
    }
}
