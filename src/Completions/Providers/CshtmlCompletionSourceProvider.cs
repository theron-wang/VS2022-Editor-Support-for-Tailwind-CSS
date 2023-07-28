using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using TailwindCSSIntellisense.Completions.Sources;
using TailwindCSSIntellisense.Settings;

namespace TailwindCSSIntellisense.Completions.Providers
{
    /// <summary>
    /// A factory for creating <see cref="HtmlCompletionSource"/>; exported via MEF.
    /// </summary>
    [Export(typeof(ICompletionSourceProvider))]
    [ContentType("cshtml")]
    [Name("TailwindCSS CSHTML Token Completion")]
    internal class CshtmlCompletionSourceProvider : ICompletionSourceProvider
    {
        [Import]
        internal CompletionUtilities CompletionUtils { get; set; }
        [Import]
        internal SettingsProvider SettingsProvider { get; set; }

        public ICompletionSource TryCreateCompletionSource(ITextBuffer textBuffer)
        {
            return new CshtmlCompletionSource(CompletionUtils, SettingsProvider, textBuffer);
        }
    }
}
