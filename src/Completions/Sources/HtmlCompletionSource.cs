using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TailwindCSSIntellisense.Options;
using TailwindCSSIntellisense.Settings;

namespace TailwindCSSIntellisense.Completions.Sources
{
    /// <summary>
    /// Completion provider for all HTML content files to provide Intellisense support for TailwindCSS classes
    /// </summary>
    internal class HtmlCompletionSource : ICompletionSource
    {
        private CompletionUtilities _completionUtils;
        private SettingsProvider _settingsProvider;
        private ITextBuffer _textBuffer;

        private bool? _showAutocomplete;
        private bool _initializeSuccess = true;

        public HtmlCompletionSource(CompletionUtilities completionUtils, SettingsProvider settingsProvider, ITextBuffer textBuffer)
        {
            _completionUtils = completionUtils;
            _settingsProvider = settingsProvider;
            _textBuffer = textBuffer;

            _settingsProvider.OnSettingsChanged += SettingsChangedAsync;
        }

        /// <summary>
        /// Overrides the original completion set to include TailwindCSS classes
        /// </summary>
        /// <param name="session">Provided by Visual Studio</param>
        /// <param name="completionSets">Provided by Visual Studio</param>

        void ICompletionSource.AugmentCompletionSession(ICompletionSession session, IList<CompletionSet> completionSets)
        {
            if (_showAutocomplete == null)
            {
                _showAutocomplete = ThreadHelper.JoinableTaskFactory.Run(_settingsProvider.GetSettingsAsync).EnableTailwindCss;
            }

            if (_showAutocomplete == false)
            {
                return;
            }

            if (!_completionUtils.Initialized || _initializeSuccess == false)
            {
                _initializeSuccess = ThreadHelper.JoinableTaskFactory.Run(() => _completionUtils.InitializeAsync());

                if (_initializeSuccess == false)
                {
                    return;
                }
            }

            if (IsInClassScope(session, out string classAttributeValueUpToPosition) == false)
            {
                return;
            }

            var position = session.TextView.Caret.Position.BufferPosition.Position;
            var snapshot = _textBuffer.CurrentSnapshot;
            var triggerPoint = session.GetTriggerPoint(snapshot);

            if (triggerPoint == null)
            {
                return;
            }

            var currentClassTotal = classAttributeValueUpToPosition.Split(' ').Last();

            var completions = ClassCompletionGeneratorHelper.GetCompletions(currentClassTotal, _completionUtils);

            var applicableTo = GetApplicableTo(triggerPoint.Value, snapshot);

            if (completionSets.Count == 1)
            {
                var defaultCompletionSet = completionSets[0];

                if (defaultCompletionSet.Completions.Count > 0)
                {
                    var addToBeginning = ThreadHelper.JoinableTaskFactory.Run(General.GetLiveInstanceAsync).TailwindCompletionsComeFirst;

                    if (addToBeginning)
                    {
                        // Cast to Completion3 to gain access to IconMoniker
                        // Return new Completion3 so session commit will actually commit the text
                        completions.AddRange(defaultCompletionSet.Completions
                            .Where(c => c.DisplayText.StartsWith(currentClassTotal, StringComparison.InvariantCultureIgnoreCase))
                            .Cast<Completion3>()
                            .Select(c => new Completion3(c.DisplayText, c.InsertionText, c.DisplayText, new ImageMoniker() { Guid = c.IconMoniker.Guid, Id = c.IconMoniker.Id }, c.IconAutomationText)));
                    }
                    else
                    {
                        completions.InsertRange(0, defaultCompletionSet.Completions
                            .Where(c => c.DisplayText.StartsWith(currentClassTotal, StringComparison.InvariantCultureIgnoreCase))
                            .Cast<Completion3>()
                            .Select(c => new Completion3(c.DisplayText, c.InsertionText, c.DisplayText, new ImageMoniker() { Guid = c.IconMoniker.Guid, Id = c.IconMoniker.Id }, c.IconAutomationText)));
                    }
                }

                var overridenCompletionSet = new TailwindCssCompletionSet(
                    defaultCompletionSet.Moniker,
                    defaultCompletionSet.DisplayName,
                    applicableTo,
                    completions,
                    defaultCompletionSet.CompletionBuilders);
                // Overrides the original completion set so there aren't two different completion tabs
                completionSets.Clear();
                completionSets.Add(overridenCompletionSet);
            }
            else
            {
                completionSets.Add(new TailwindCssCompletionSet(
                    "All",
                    "All",
                    applicableTo,
                    completions,
                    new List<Completion>()));
            }
        }

        private bool IsInClassScope(ICompletionSession session, out string classText)
        {
            var startPos = new SnapshotPoint(session.TextView.TextSnapshot, 0);
            var caretPos = session.TextView.Caret.Position.BufferPosition;

            var searchSnapshot = new SnapshotSpan(startPos, caretPos);
            var text = searchSnapshot.GetText();

            var indexOfCurrentClassAttribute = text.LastIndexOf("class=\"");
            if (indexOfCurrentClassAttribute == -1)
            {
                classText = null;
                return false;
            }
            var quotationMarkAfterLastClassAttribute = text.IndexOf('\"', indexOfCurrentClassAttribute);
            var lastQuotationMark = text.LastIndexOf('\"');

            if (lastQuotationMark == quotationMarkAfterLastClassAttribute)
            {
                classText = text.Substring(lastQuotationMark + 1);
                return true;
            }
            else
            {
                classText = null;
                return false;
            }
        }

        private ITrackingSpan GetApplicableTo(SnapshotPoint triggerPoint, ITextSnapshot snapshot)
        {
            SnapshotPoint end = triggerPoint;
            SnapshotPoint start = triggerPoint - 1;

            while (start.GetChar() != '"' && start.GetChar() != ' ')
            {
                start -= 1;
            }

            start += 1;

            return snapshot.CreateTrackingSpan(new SnapshotSpan(start, end), SpanTrackingMode.EdgeInclusive);
        }

        private Task SettingsChangedAsync(TailwindSettings settings)
        {
            _showAutocomplete = settings.EnableTailwindCss;
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _settingsProvider.OnSettingsChanged -= SettingsChangedAsync;
        }
    }
}
