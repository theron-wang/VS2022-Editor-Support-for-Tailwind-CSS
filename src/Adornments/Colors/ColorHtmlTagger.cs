using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using TailwindCSSIntellisense.Completions;
using TailwindCSSIntellisense.Parsers;

namespace TailwindCSSIntellisense.Adornments.Colors;

[Export(typeof(IViewTaggerProvider))]
[TagType(typeof(IntraTextAdornmentTag))]
[ContentType("html")]
[ContentType("WebForms")]
[TextViewRole(PredefinedTextViewRoles.Document)]
[TextViewRole(PredefinedTextViewRoles.Analyzable)]
internal sealed class ColorHtmlTaggerProvider : IViewTaggerProvider
{
    [Import]
    internal ProjectConfigurationManager ProjectConfigurationManager { get; set; } = null!;

    public ITagger<T>? CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
    {
        // Handle legacy Razor editor; this completion controller is prioritized but
        // we should only use the Razor completion controller in that case
        if (buffer.IsLegacyRazorEditor())
        {
            return null;
        }
        return buffer.Properties.GetOrCreateSingletonProperty(() => new ColorHtmlTagger(buffer, textView, ProjectConfigurationManager)) as ITagger<T>;
    }

    private class ColorHtmlTagger(ITextBuffer buffer, ITextView view, ProjectConfigurationManager completionUtilities)
        : ColorTaggerBase(buffer, view, completionUtilities)
    {
        protected override IEnumerable<SnapshotSpan> GetScopes(SnapshotSpan span, ITextSnapshot snapshot)
        {
            foreach (var classAttributeSpan in HtmlParser.GetClassAttributeValues(span))
            {
                var text = classAttributeSpan.GetText();

                foreach (var split in ClassRegexHelper.SplitNonRazorClasses(text))
                {
                    yield return new SnapshotSpan(snapshot, classAttributeSpan.Start + split.Index, split.Value.Length);
                }
            }
        }
    }
}
