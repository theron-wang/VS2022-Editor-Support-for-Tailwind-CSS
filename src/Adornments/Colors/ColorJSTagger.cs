﻿using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using TailwindCSSIntellisense.Completions;
using TailwindCSSIntellisense.Configuration;
using TailwindCSSIntellisense.Parsers;

namespace TailwindCSSIntellisense.Adornments.Colors;

[Export(typeof(IViewTaggerProvider))]
[TagType(typeof(IntraTextAdornmentTag))]
[ContentType("JavaScript")]
[ContentType("TypeScript")]
[ContentType("jsx")]
[TextViewRole(PredefinedTextViewRoles.Document)]
[TextViewRole(PredefinedTextViewRoles.Analyzable)]
internal sealed class ColorJSTaggerProvider : IViewTaggerProvider
{
    [Import]
    internal ProjectConfigurationManager ProjectConfigurationManager { get; set; } = null!;
    [Import]
    public CompletionConfiguration CompletionConfiguration { get; set; } = null!;

    public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
    {
        return (ITagger<T>)buffer.Properties.GetOrCreateSingletonProperty(() => new ColorJSTagger(buffer, textView, ProjectConfigurationManager, CompletionConfiguration));
    }

    private class ColorJSTagger(ITextBuffer buffer, ITextView view, ProjectConfigurationManager completionUtilities, CompletionConfiguration completionConfiguration)
        : ColorTaggerBase(buffer, view, completionUtilities, completionConfiguration)
    {
        protected override IEnumerable<SnapshotSpan> GetScopes(SnapshotSpan span, ITextSnapshot snapshot)
        {
            foreach (var classAttributeSpan in JSParser.GetClassAttributeValues(span))
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
