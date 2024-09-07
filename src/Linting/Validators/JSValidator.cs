using Microsoft.VisualStudio.Text;
using TailwindCSSIntellisense.Completions;

namespace TailwindCSSIntellisense.Linting.Validators;
internal class JSValidator : HtmlValidator
{
    protected override string SearchFor => $"className=";

    protected JSValidator(ITextBuffer buffer, LinterUtilities linterUtils, CompletionUtilities completionUtilities) : base(buffer, linterUtils, completionUtilities)
    {

    }

    public static new Validator Create(ITextBuffer buffer, LinterUtilities linterUtils, CompletionUtilities completionUtilities)
    {
        return buffer.Properties.GetOrCreateSingletonProperty<Validator>(() => new JSValidator(buffer, linterUtils, completionUtilities));
    }
}
