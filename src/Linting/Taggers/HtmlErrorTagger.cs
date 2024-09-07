using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;
using TailwindCSSIntellisense.Completions;
using TailwindCSSIntellisense.Linting.Validators;

namespace TailwindCSSIntellisense.Linting.Taggers;

[Export(typeof(ITaggerProvider))]
[TagType(typeof(IErrorTag))]
[ContentType("html")]
[ContentType("WebForms")]
[TextViewRole(PredefinedTextViewRoles.Document)]
[TextViewRole(PredefinedTextViewRoles.Analyzable)]
internal class HtmlErrorTaggerProvider : ITaggerProvider
{
    [Import]
    public LinterUtilities LinterUtilities { get; set; }
    [Import]
    public CompletionUtilities CompletionUtilities { get; set; }

    public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
    {
        return buffer.Properties.GetOrCreateSingletonProperty(() => new HtmlErrorTagger(buffer, LinterUtilities, CompletionUtilities)) as ITagger<T>;
    }

    internal sealed class HtmlErrorTagger : ErrorTaggerBase, IDisposable
    {
        public HtmlErrorTagger(ITextBuffer buffer, LinterUtilities linterUtils, CompletionUtilities completionUtilities) : base(buffer, linterUtils)
        {
            _errorChecker = HtmlValidator.Create(buffer, linterUtils, completionUtilities);
            _errorChecker.Validated += UpdateErrors;
        }

        public void Dispose()
        {
            _errorChecker.Validated -= UpdateErrors;
        }
    }
}