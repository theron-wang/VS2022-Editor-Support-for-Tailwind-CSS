using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TailwindCSSIntellisense.Completions;
using TailwindCSSIntellisense.Options;

namespace TailwindCSSIntellisense.Adornments.Directives;

[Export(typeof(IViewTaggerProvider))]
[TagType(typeof(IntraTextAdornmentTag))]
[ContentType("css")]
[ContentType("tcss")]
[TextViewRole(PredefinedTextViewRoles.Document)]
[TextViewRole(PredefinedTextViewRoles.Analyzable)]
internal sealed class DirectiveCssTaggerProvider : IViewTaggerProvider
{
    [Import]
    internal CompletionUtilities CompletionUtilities { get; set; }
    [Import]
    internal ITextStructureNavigatorSelectorService TextStructureNavigatorSelector { get; set; }

    public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
    {
        return buffer.Properties.GetOrCreateSingletonProperty(() => new CssDirectiveTagger(buffer, TextStructureNavigatorSelector, CompletionUtilities)) as ITagger<T>;
    }

    /// <summary>
    /// Adds adornments to CSS directives, like @apply, to prevent confusion when squiggles are present.
    /// If/when VS adds support to intercept these warnings, remove this class.
    /// </summary>
    /// <remarks>See <a href="https://github.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/issues/105">https://github.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/issues/105</a></remarks>
    private class CssDirectiveTagger : ITagger<IntraTextAdornmentTag>, IDisposable
    {
        private readonly ITextBuffer _buffer;
        private readonly ProjectCompletionValues _completionUtilities;
        private readonly ITextStructureNavigator _textStructureNavigator;
        private readonly ImageSource _tailwindLogo;
        private bool _isProcessing;
        private General _generalOptions;

        internal CssDirectiveTagger(ITextBuffer buffer, ITextStructureNavigatorSelectorService textStructureNavigatorSelector, CompletionUtilities completionUtilities)
        {
            _buffer = buffer;
            _completionUtilities = completionUtilities.GetCompletionConfigurationByFilePath(_buffer.GetFileName());
            _tailwindLogo = completionUtilities.TailwindLogo;

            _textStructureNavigator = textStructureNavigatorSelector.GetTextStructureNavigator(buffer);

            _buffer.Changed += OnBufferChanged;
            General.Saved += GeneralSettingsChanged;
        }

        private void OnBufferChanged(object sender, TextContentChangedEventArgs e)
        {
            if (_isProcessing || e.Changes.Count == 0)
            {
                return;
            }

            try
            {
                _isProcessing = true;
                var start = e.Changes.First().NewSpan.Start;
                var end = e.Changes.Last().NewSpan.End;

                var startLine = e.After.GetLineFromPosition(start);
                var endLine = e.After.GetLineFromPosition(end);

                var span = new SnapshotSpan(e.After, Span.FromBounds(startLine.Start, endLine.End));
                TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(span));
            }
            finally
            {
                _isProcessing = false;
            }
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        public void Dispose()
        {
            _buffer.Changed -= OnBufferChanged;
            General.Saved -= GeneralSettingsChanged;
        }
        /// <summary>
        /// Gets relevant @ directives.
        /// </summary>
        protected IEnumerable<SnapshotSpan> GetScopes(SnapshotSpan span)
        {
            int position = span.Start.Position;
            int end = span.End.Position;

            while (position < end)
            {
                var point = new SnapshotPoint(span.Snapshot, position);
                var extent = _textStructureNavigator.GetExtentOfWord(point);

                if (extent.IsSignificant)
                {
                    string text = extent.Span.GetText();
                    if (text == "@apply")
                    {
                        yield return extent.Span;
                    }
                    else if (_completionUtilities.Version == TailwindVersion.V3 && (text == "@tailwind" || text == "@config"))
                    {
                        yield return extent.Span;
                    }
                    else if (text == "@theme" || text == "@source" || text == "@utility" || text == "@custom-variant" || text == "@config" || text == "@plugin" || text == "@variant" || text.StartsWith("@slot"))
                    {
                        yield return extent.Span;
                    }

                    position = extent.Span.End.Position;
                }
                else
                {
                    position++;
                }
            }
        }

        private void GeneralSettingsChanged(General general)
        {
            _generalOptions = general;
            TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(new SnapshotSpan(_buffer.CurrentSnapshot, 0, _buffer.CurrentSnapshot.Length)));
        }

        private bool Enabled()
        {
            _generalOptions ??= ThreadHelper.JoinableTaskFactory.Run(General.GetLiveInstanceAsync);

            return _generalOptions.ShowColorPreviews && _generalOptions.UseTailwindCss;
        }

        public IEnumerable<ITagSpan<IntraTextAdornmentTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            var tags = new List<ITagSpan<IntraTextAdornmentTag>>();

            if (!spans.Any() || !Enabled())
            {
                return tags;
            }

            foreach (var span in spans)
            {
                tags.AddRange(GetAdornments(span));
            }

            return tags;
        }

        private IEnumerable<ITagSpan<IntraTextAdornmentTag>> GetAdornments(SnapshotSpan span)
        {
            foreach (var scope in GetScopes(span))
            {
                var tag = new IntraTextAdornmentTag(new Image() { Source = _tailwindLogo, Margin = new Thickness(4, 0, 0, 0) }, null, PositionAffinity.Successor);

                yield return new TagSpan<IntraTextAdornmentTag>(new SnapshotSpan(scope.Snapshot, scope.End, 0), tag);
            }
        }
    }
}

