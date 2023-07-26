using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using System.Linq;
using TailwindCSSIntellisense.Options;

namespace TailwindCSSIntellisense.Completions
{
    /// <summary>
    /// Represents a set of TailwindCSS completions
    /// </summary>
    internal class TailwindCssCompletionSet : CompletionSet
    {
        private BulkInsertObservableCollection<Completion> _completions = new BulkInsertObservableCollection<Completion>();
        private FilteredObservableCollection<Completion> _filteredCompletions;
        private int _filterBufferTextVersionNumber;
        private string _filterBufferText;

        private string FilterBufferText
        {
            get
            {
                if (ApplicableTo != null)
                {
                    ITextSnapshot currentSnapshot = ApplicableTo.TextBuffer.CurrentSnapshot;
                    if (_filterBufferText == null || _filterBufferTextVersionNumber != currentSnapshot.Version.VersionNumber)
                    {
                        _filterBufferText = ApplicableTo.GetText(currentSnapshot);
                        _filterBufferTextVersionNumber = currentSnapshot.Version.VersionNumber;
                    }
                }

                return _filterBufferText;
            }
        }

        /// <inheritdoc />
        public TailwindCssCompletionSet(string moniker, string displayName, ITrackingSpan applicableTo, IEnumerable<Completion> completions, IEnumerable<Completion> completionBuilders) : base(moniker, displayName, applicableTo, completions, completionBuilders)
        {
            _completions.AddRange(completions);
            Initialize();
        }

        /// <summary>
        /// Adds completions to the existing completion set (used to combine Tailwind completions with shim completions)
        /// </summary>
        /// <param name="completions">The list of completions to add</param>
        public void AddCompletions(IEnumerable<Completion> completions)
        {
            var addToEnd = ThreadHelper.JoinableTaskFactory.Run(General.GetLiveInstanceAsync).TailwindCompletionsComeFirst;

            if (addToEnd)
            {
                _completions.AddRange(completions
                    .Where(c => _completions.Any(c2 => c2.DisplayText == c.DisplayText) == false));
            }
            else
            {
                _completions.AddRangeToBeginning(completions
                    .Where(c => _completions.Any(c2 => c2.DisplayText == c.DisplayText) == false));
            }
        }

        private void Initialize()
        {
            _filteredCompletions = new FilteredObservableCollection<Completion>(_completions);
        }

        /// <inheritdoc />
        public override IList<Completion> Completions => _filteredCompletions;

        public override void Filter()
        {
            if (string.IsNullOrEmpty(FilterBufferText))
            {
                _filteredCompletions.StopFiltering();
                return;
            }

            _filteredCompletions.Filter(c => c.DisplayText.Contains(FilterBufferText));
        }
    }
}
