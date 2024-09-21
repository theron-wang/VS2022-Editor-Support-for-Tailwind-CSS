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
    /// Completion provider for all CSS files to provide Intellisense support for TailwindCSS classes, functions, and directives
    /// </summary>
    internal class CssCompletionSource(ITextBuffer textBuffer, CompletionUtilities completionUtils, ColorIconGenerator colorIconGenerator, DescriptionGenerator descriptionGenerator, SettingsProvider settingsProvider) :
        ClassCompletionGenerator(textBuffer, completionUtils, colorIconGenerator, descriptionGenerator, settingsProvider), ICompletionSource
    {
        private bool _initializeSuccess = true;

        /// <summary>
        /// Provides relevant TailwindCSS completions
        /// </summary>
        /// <param name="session">VS provided</param>
        /// <param name="completionSets">VS provided</param>
        void ICompletionSource.AugmentCompletionSession(ICompletionSession session, IList<CompletionSet> completionSets)
        {
            if (_showAutocomplete == null)
            {
                _showAutocomplete = ThreadHelper.JoinableTaskFactory.Run(_settingsProvider.GetSettingsAsync).EnableTailwindCss;
            }

            if (_showAutocomplete == false || _completionUtils.Scanner.HasConfigurationFile == false)
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

            var completions = new List<Completion>();

            var position = session.TextView.Caret.Position.BufferPosition.Position;
            var snapshot = _textBuffer.CurrentSnapshot;
            var triggerPoint = session.GetTriggerPoint(snapshot);
            var applicableTo = GetApplicableTo(triggerPoint.Value, snapshot);

            if (triggerPoint == null)
            {
                return;
            }

            var line = triggerPoint.Value.GetContainingLine();

            if (!IsCaretInBlock(session, out bool isInBaseDirectiveBlock))
            {
                if (IsUsingDirective(session, out string directive))
                {
                    switch (directive)
                    {
                        case "@tailwind":
                            completions = new List<Completion>()
                            {
                                new Completion("base", "base", "base", _completionUtils.TailwindLogo, null),
                                new Completion("components", "components", "components", _completionUtils.TailwindLogo, null),
                                new Completion("utilities", "utilities", "utilities", _completionUtils.TailwindLogo, null),
                                new Completion("variants", "variants", "variants", _completionUtils.TailwindLogo, null)
                            };
                            break;
                        case "@layer":
                            completions = new List<Completion>()
                            {
                                new Completion("base", "base", "base", _completionUtils.TailwindLogo, null),
                                new Completion("components", "components", "components", _completionUtils.TailwindLogo, null),
                                new Completion("utilities", "utilities", "utilities", _completionUtils.TailwindLogo, null)
                            };
                            break;
                        case "@media":
                            completions = [];
                            foreach (var screen in _completionUtils.Screen)
                            {
                                completions.Add(new($"screen({screen})", $"screen({screen})", $"screen({screen})", _completionUtils.TailwindLogo, null));
                            }
                            break;
                    }
                }
                else
                {
                    completions = new List<Completion>()
                    {
                        // Directive completions are hard-coded in as there are only two of them
                        new Completion("@tailwind", "@tailwind", "Use the @tailwind directive to insert Tailwind’s base, components, utilities and variants styles into your CSS.", _completionUtils.TailwindLogo, null),
                        new Completion("@config", "@config", "Use the @config directive to specify which config file Tailwind should use when compiling that CSS file. This is useful for projects that need to use different configuration files for different CSS entry points.", _completionUtils.TailwindLogo, null)
                    };
                }
            }
            else if (!isInBaseDirectiveBlock)
            {
                if (IsUsingApplyDirective(session))
                {
                    var classRaw = applicableTo.GetText(snapshot).Split(new[] { "@apply" }, StringSplitOptions.None).Last().TrimStart().Split(' ').Last() ?? "";

                    completions = GetCompletions(classRaw);
                }
                else if (CanShowApplyDirective(session))
                {
                    completions.Add(
                        // @apply completion is the only completion in this context
                        new Completion("@apply", "@apply", "Use @apply to inline any existing utility classes into your own custom CSS.", _completionUtils.TailwindLogo, null)
                    );
                }
                else
                {
                    completions = new List<Completion>()
                    {
                        new Completion("theme()", "theme()", "Use the theme() function to access your Tailwind config values using dot notation.", _completionUtils.TailwindLogo, null)
                    };
                }
            }

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
                            .Cast<Completion3>()
                            .Select(c => new Completion3(c.DisplayText, c.InsertionText, c.DisplayText, new ImageMoniker() { Guid = c.IconMoniker.Guid, Id = c.IconMoniker.Id }, c.IconAutomationText)));
                    }
                    else
                    {
                        completions.InsertRange(0, defaultCompletionSet.Completions
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

        private ITrackingSpan GetApplicableTo(SnapshotPoint triggerPoint, ITextSnapshot snapshot)
        {
            SnapshotPoint end = triggerPoint;
            SnapshotPoint start;
            if (end.Position == 0)
            {
                start = end;
            }
            else
            {
                start = triggerPoint - 1;
            }

            while (start.Position > 0 && start.GetChar() != '"' && !char.IsWhiteSpace(start.GetChar()))
            {
                start -= 1;
            }

            while (end.Position < snapshot.Length && !"\"';".Contains(end.GetChar()) && !char.IsWhiteSpace(end.GetChar()))
            {
                end += 1;
            }

            if (start.Position != 0)
            {
                start += 1;
            }

            return snapshot.CreateTrackingSpan(new SnapshotSpan(start, end), SpanTrackingMode.EdgeInclusive);
        }

        private bool IsCaretInBlock(ICompletionSession session, out bool isInBaseDirectiveBlock)
        {
            var startPos = new SnapshotPoint(session.TextView.TextSnapshot, 0);
            var caretPos = session.TextView.Caret.Position.BufferPosition;

            var searchSnapshot = new SnapshotSpan(startPos, caretPos);
            var text = searchSnapshot.GetText();

            var lastIndexOfOpenBrace = text.LastIndexOf('{');
            var lastIndexOfCloseBrace = text.LastIndexOf('}');

            var lastIndexOfCloserDirective = Math.Max(text.LastIndexOf("@media"), text.LastIndexOf("@layer"));

            // If no open brace ('{') is found or close brace ('{') is closer to caret than open brace
            // AND also there are no @media or @layer directives
            if ((lastIndexOfOpenBrace == -1 || lastIndexOfCloseBrace > lastIndexOfOpenBrace) && lastIndexOfCloserDirective == -1)
            {
                isInBaseDirectiveBlock = false;
                return false;
            }
            else if (lastIndexOfCloserDirective != -1)
            {
                var indexOfOpeningBraceClosestToCloserDirective = text.IndexOf('{', lastIndexOfCloserDirective);

                if (indexOfOpeningBraceClosestToCloserDirective == lastIndexOfOpenBrace)
                {
                    isInBaseDirectiveBlock = true;
                    return true;
                }
                else
                {
                    if (lastIndexOfCloseBrace < lastIndexOfOpenBrace)
                    {
                        isInBaseDirectiveBlock = false;
                        return true;
                    }

                    // Caret is after @ directive and before the opening brace
                    if (lastIndexOfCloserDirective < caretPos.Position && indexOfOpeningBraceClosestToCloserDirective == -1)
                    {
                        isInBaseDirectiveBlock = false;
                        return false;
                    }

                    // There is another declaration in the @media/@layer block
                    // Therefore, check for two closing braces in a row to determine if the block is ended

                    isInBaseDirectiveBlock = true;

                    var indexOfSecondToLastCloseBrace = text.LastIndexOf('}', lastIndexOfCloseBrace - 1);

                    if (indexOfSecondToLastCloseBrace == -1)
                    {
                        return true;
                    }

                    return !string.IsNullOrWhiteSpace(text.Substring(indexOfSecondToLastCloseBrace, lastIndexOfCloseBrace - indexOfSecondToLastCloseBrace));
                }
            }
            else
            {
                isInBaseDirectiveBlock = false;
                // If no close brace is found or open brace is after close brace
                return (lastIndexOfCloseBrace < lastIndexOfOpenBrace);
            }
        }

        private bool CanShowApplyDirective(ICompletionSession session)
        {
            // Function is called and ensured that the caret is inside a block

            var startPos = new SnapshotPoint(session.TextView.TextSnapshot, 0);
            var caretPos = session.TextView.Caret.Position.BufferPosition;

            var searchSnapshot = new SnapshotSpan(startPos, caretPos);
            var text = searchSnapshot.GetText();

            var lastIndexOfSemicolon = text.LastIndexOf(";");
            var lastIndexOfOpeningBrace = text.LastIndexOf("{");

            if (lastIndexOfSemicolon == -1 && lastIndexOfOpeningBrace == -1)
            {
                return false;
            }
            else if (lastIndexOfOpeningBrace > lastIndexOfSemicolon)
            {
                return true;
            }
            else
            {
                return text.EndsWith(";") || string.IsNullOrWhiteSpace(text.Substring(lastIndexOfSemicolon + 1));
            }
        }

        private bool IsUsingApplyDirective(ICompletionSession session)
        {
            return IsUsingDirective(session, out string directive) && directive == "@apply";
        }

        private bool IsUsingDirective(ICompletionSession session, out string directive)
        {
            var startPos = new SnapshotPoint(session.TextView.TextSnapshot, 0);
            var caretPos = session.TextView.Caret.Position.BufferPosition;

            var searchSnapshot = new SnapshotSpan(startPos, caretPos);
            var text = searchSnapshot.GetText();

            var lastIndexOfSemicolon = text.LastIndexOf(";");
            var lastIndexOfAt = text.LastIndexOf('@');

            if (lastIndexOfAt != -1 && lastIndexOfAt > lastIndexOfSemicolon)
            {
                directive = text.Substring(lastIndexOfAt).Split(' ')[0];

                return text.EndsWith(directive) == false;
            }
            else
            {
                directive = null;
                return false;
            }
        }
    }
}
