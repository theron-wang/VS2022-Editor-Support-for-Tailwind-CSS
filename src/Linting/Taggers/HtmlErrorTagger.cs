using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Security.Cryptography;
using TailwindCSSIntellisense.Completions;
using TailwindCSSIntellisense.Configuration;
using TailwindCSSIntellisense.Linting.Validators;
using TailwindCSSIntellisense.Options;

namespace TailwindCSSIntellisense.Linting.Taggers;

[Export(typeof(ITaggerProvider))]
[TagType(typeof(IErrorTag))]
[ContentType("html")]
[ContentType("WebForms")]
[ContentType("razor")]
[ContentType("LegacyRazorCSharp")]
[ContentType("LegacyRazor")]
[ContentType("LegacyRazorCoreCSharp")]
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

    internal sealed class HtmlErrorTagger: ErrorTaggerBase, IDisposable
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