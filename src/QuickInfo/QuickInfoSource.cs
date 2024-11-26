using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TailwindCSSIntellisense.Completions;

namespace TailwindCSSIntellisense.QuickInfo;

internal abstract class QuickInfoSource : IAsyncQuickInfoSource
{
    protected ITextBuffer _textBuffer;
    protected DescriptionGenerator _descriptionGenerator;
    private readonly CompletionUtilities _completionUtilities;

    private const string PropertyKey = "tailwindintellisensequickinfoadded";

    public QuickInfoSource(ITextBuffer textBuffer, DescriptionGenerator descriptionGenerator, CompletionUtilities completionUtilities)
    {
        _textBuffer = textBuffer;
        _descriptionGenerator = descriptionGenerator;
        _completionUtilities = completionUtilities;
    }

    public void Dispose()
    {
    }

    public Task<QuickInfoItem> GetQuickInfoItemAsync(IAsyncQuickInfoSession session, CancellationToken cancellationToken)
    {
        // session.Properties is to ensure that quick info is only added once (measure for #17)
        if (session.Content is null || session.Content.Any() || session.State == QuickInfoSessionState.Visible || session.State == QuickInfoSessionState.Dismissed || session.Properties.ContainsProperty(PropertyKey))
        {
            return Task.FromResult<QuickInfoItem>(null);
        }

        var triggerPoint = session.GetTriggerPoint(_textBuffer.CurrentSnapshot);

        if (triggerPoint != null && IsInClassScope(session, out var classSpan) && classSpan != null)
        {
            var fullText = classSpan.Value.GetText();
            var classText = fullText.Split(':').Last();

            var isImportant = ImportantModiferHelper.IsImportantModifier(classText);

            if (isImportant)
            {
                classText = classText.TrimStart('!');
            }

            if (!_completionUtilities.IsClassAllowed(classText))
            {
                return Task.FromResult<QuickInfoItem>(null);
            }

            var desc = _descriptionGenerator.GetDescription(classText);

            var span = _textBuffer.CurrentSnapshot.CreateTrackingSpan(classSpan.Value, SpanTrackingMode.EdgeInclusive);

            if (string.IsNullOrEmpty(desc) == false)
            {
                var classElement = new ContainerElement(
                    ContainerElementStyle.Wrapped,
                    new ClassifiedTextElement(
                        new ClassifiedTextRun(PredefinedClassificationTypeNames.Literal, $".{CssEscape(fullText)} {{", ClassifiedTextRunStyle.UseClassificationFont)
                    ));

                var descriptionLines = new List<ClassifiedTextElement>();

                foreach (var l in desc.Split('\n'))
                {
                    var line = l.Trim();

                    var keyword = line.Substring(0, line.IndexOf(':')).Trim();
                    var value = line.Substring(line.IndexOf(':') + 1).Trim().Trim(';');

                    descriptionLines.Add(
                        new ClassifiedTextElement(
                            new ClassifiedTextRun(PredefinedClassificationTypeNames.WhiteSpace, "  ", ClassifiedTextRunStyle.UseClassificationFont),
                                new ClassifiedTextRun(PredefinedClassificationTypeNames.MarkupAttribute, keyword, ClassifiedTextRunStyle.UseClassificationFont),
                                new ClassifiedTextRun(PredefinedClassificationTypeNames.Identifier, ": ", ClassifiedTextRunStyle.UseClassificationFont),
                                new ClassifiedTextRun(PredefinedClassificationTypeNames.MarkupAttributeValue, value + (isImportant ? " !important" : ""), ClassifiedTextRunStyle.UseClassificationFont),
                                new ClassifiedTextRun(PredefinedClassificationTypeNames.Identifier, ";", ClassifiedTextRunStyle.UseClassificationFont)
                        )
                    );
                }

                var descriptionElement = new ContainerElement(
                    ContainerElementStyle.Stacked,
                    descriptionLines);

                var closingBracket = new ContainerElement(
                    ContainerElementStyle.Wrapped,
                    new ClassifiedTextElement(
                        new ClassifiedTextRun(PredefinedClassificationTypeNames.Identifier, "}", ClassifiedTextRunStyle.UseClassificationFont)
                    )
                );

                session.Properties.AddProperty(PropertyKey, true);

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

    private static string CssEscape(string input)
    {
        string pattern = "(['\"{}()\\[\\]:;,.<>+*~?\\s!@#$%^&*()])";

        string escapedString = Regex.Replace(input, pattern, "\\$1");

        return escapedString;
    }

    protected abstract bool IsInClassScope(IAsyncQuickInfoSession session, out SnapshotSpan? span);
}
