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
    [ContentType("html")]
    [Name("TailwindCSS HTML Token Completion")]
    internal class HtmlCompletionSourceProvider : ICompletionSourceProvider
    {
        [Import]
        internal CompletionUtilities CompletionUtils { get; set; }
        [Import]
        internal SettingsProvider SettingsProvider { get; set; }

        public ICompletionSource TryCreateCompletionSource(ITextBuffer textBuffer)
        {
            return new HtmlCompletionSource(CompletionUtils, SettingsProvider, textBuffer);
        }
    }
}
