using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using TailwindCSSIntellisense.Completions.Sources;
using TailwindCSSIntellisense.Settings;

namespace TailwindCSSIntellisense.Completions.Providers
{
    /// <summary>
    /// A factory for creating <see cref="CssCompletionSource"/>; exported via MEF.
    /// </summary>
    [Export(typeof(ICompletionSourceProvider))]
    [ContentType("css")]
    [Name("TailwindCSS CSS Token Completion")]
    [Order(After = Priority.Default, Before = Priority.High)]
    internal class CssCompletionSourceProvider : ICompletionSourceProvider
    {
        [Import]
        internal CompletionUtilities TailwindEssentials { get; set; }
        [Import]
        internal SettingsProvider SettingsProvider { get; set; }

        public ICompletionSource TryCreateCompletionSource(ITextBuffer textBuffer)
        {
            return new CssCompletionSource(textBuffer, TailwindEssentials, SettingsProvider);
        }
    }
}
