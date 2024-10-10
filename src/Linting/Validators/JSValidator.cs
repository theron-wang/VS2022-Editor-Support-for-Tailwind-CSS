using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using TailwindCSSIntellisense.Completions;
using TailwindCSSIntellisense.Parsers;

namespace TailwindCSSIntellisense.Linting.Validators;
internal class JSValidator : HtmlValidator
{
    protected override string SearchFor => $"className=";

    protected JSValidator(ITextBuffer buffer, LinterUtilities linterUtils, CompletionUtilities completionUtilities) : base(buffer, linterUtils, completionUtilities)
    {

    }

    public override IEnumerable<SnapshotSpan> GetScopes(SnapshotSpan span, ITextSnapshot snapshot)
    {
        return JSParser.GetScopes(span, snapshot);
    }

    public static new Validator Create(ITextBuffer buffer, LinterUtilities linterUtils, CompletionUtilities completionUtilities)
    {
        return buffer.Properties.GetOrCreateSingletonProperty<Validator>(() => new JSValidator(buffer, linterUtils, completionUtilities));
    }
}
