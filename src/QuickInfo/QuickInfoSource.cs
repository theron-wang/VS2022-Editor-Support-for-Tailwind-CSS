using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TailwindCSSIntellisense.Completions;

namespace TailwindCSSIntellisense.QuickInfo
{
    internal abstract class QuickInfoSource : IAsyncQuickInfoSource
    {
        protected ITextBuffer _textBuffer;
        protected CompletionUtilities _completionUtilities;

        public QuickInfoSource(ITextBuffer textBuffer, CompletionUtilities completionUtilities)
        {
            _textBuffer = textBuffer;
            _completionUtilities = completionUtilities;
        }

        public void Dispose()
        {
        }

        public Task<QuickInfoItem> GetQuickInfoItemAsync(IAsyncQuickInfoSession session, CancellationToken cancellationToken)
        {
            var triggerPoint = session.GetTriggerPoint(_textBuffer.CurrentSnapshot);

            if (triggerPoint != null && IsInClassScope(session, out var classSpan) && classSpan != null)
            {
                var classText = classSpan.Value.GetText().Split(':').Last();
                var desc = GetDescription(classText);
                var span = _textBuffer.CurrentSnapshot.CreateTrackingSpan(classSpan.Value, SpanTrackingMode.EdgeInclusive);

                if (string.IsNullOrEmpty(desc) == false)
                {
                    var classElement = new ContainerElement(
                        ContainerElementStyle.Wrapped,
                        new ClassifiedTextElement(
                            new ClassifiedTextRun(PredefinedClassificationTypeNames.Identifier, $".{classText} {{")
                        ));

                    var descriptionLines = new List<ClassifiedTextElement>();

                    foreach (var l in desc.Split('\n'))
                    {
                        var line = l.Trim();

                        var keyword = line.Substring(0, line.IndexOf(':')).Trim();
                        var value = line.Substring(line.IndexOf(':') + 1).Trim().Trim(';');

                        descriptionLines.Add(
                            new ClassifiedTextElement(
                                new ClassifiedTextRun(PredefinedClassificationTypeNames.Identifier, "  "),
                                new ClassifiedTextRun(PredefinedClassificationTypeNames.MarkupAttribute, keyword),
                                    new ClassifiedTextRun(PredefinedClassificationTypeNames.Identifier, ": "),
                                    new ClassifiedTextRun(PredefinedClassificationTypeNames.MarkupAttributeValue, value),
                                    new ClassifiedTextRun(PredefinedClassificationTypeNames.Identifier, ";")
                            )
                        );
                    }

                    var descriptionElement = new ContainerElement(
                        ContainerElementStyle.Stacked,
                        descriptionLines);

                    var closingBracket = new ContainerElement(
                        ContainerElementStyle.Wrapped,
                        new ClassifiedTextElement(
                            new ClassifiedTextRun(PredefinedClassificationTypeNames.Identifier, "}")
                        )
                    );

                    return Task.FromResult(
                        new QuickInfoItem(span, new ContainerElement(
                            ContainerElementStyle.Stacked,
                            classElement,
                            descriptionElement,
                            closingBracket)));
                }
            }

            return Task.FromResult<QuickInfoItem>(null);
        }

        protected abstract bool IsInClassScope(IAsyncQuickInfoSession session, out SnapshotSpan? span);

        private string GetDescription(string text)
        {
            text = text.Split(':').Last();

            var description = _completionUtilities.GetDescription(text);
            if (string.IsNullOrEmpty(description) == false)
            {
                return description;
            }

            var segments = text.Split(new char[] { '-' }, StringSplitOptions.RemoveEmptyEntries);

            if (segments.Length >= 2)
            {
                string color;
                if (segments.Length >= 3)
                {
                    color = $"{segments[segments.Length - 2]}-{segments[segments.Length - 1]}";
                }
                else
                {
                    color = segments[segments.Length - 1];
                }
                var stem = text.Replace(color, "{0}");

                var opacityText = color.Split('/').Last();
                int? opacity = null;

                if (opacityText != color)
                {
                    color = color.Replace($"/{opacityText}", "");
                    if (int.TryParse(opacityText, out var o))
                    {
                        opacity = o;
                    }
                }

                description = _completionUtilities.GetDescription(stem, color, opacity);
                if (string.IsNullOrEmpty(description) == false)
                {
                    return description;
                }

                var spacing = segments.Last();
                stem = text.Replace(spacing, "{0}");

                return _completionUtilities.GetDescription(stem, spacing);
            }

            return null;
        }
    }
}
