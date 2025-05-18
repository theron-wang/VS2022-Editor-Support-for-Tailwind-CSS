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
[ContentType("razor")]
[ContentType("LegacyRazorCSharp")]
[ContentType("LegacyRazor")]
[ContentType("LegacyRazorCoreCSharp")]
[TextViewRole(PredefinedTextViewRoles.Document)]
[TextViewRole(PredefinedTextViewRoles.Analyzable)]
internal sealed class ColorRazorTaggerProvider : IViewTaggerProvider
{
    [Import]
    internal ProjectConfigurationManager ProjectConfigurationManager { get; set; } = null!;

    public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
    {
        return (ITagger<T>)buffer.Properties.GetOrCreateSingletonProperty(() => new ColorRazorTagger(buffer, textView, ProjectConfigurationManager));
    }

    private class ColorRazorTagger(ITextBuffer buffer, ITextView view, ProjectConfigurationManager completionUtilities)
        : ColorTaggerBase(buffer, view, completionUtilities)
    {
        protected override IEnumerable<SnapshotSpan> GetScopes(SnapshotSpan span, ITextSnapshot snapshot)
        {
            foreach (var classAttributeSpan in RazorParser.GetClassAttributeValues(span))
            {
                var text = classAttributeSpan.GetText();

                foreach (var split in ClassRegexHelper.SplitRazorClasses(text))
                {
                    yield return new SnapshotSpan(snapshot, classAttributeSpan.Start + split.Index, split.Value.Length);
                }
            }
        }
    }
}
