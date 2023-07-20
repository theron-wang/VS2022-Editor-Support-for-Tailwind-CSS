using Community.VisualStudio.Toolkit;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace TailwindCSSIntellisense.Options
{
    internal partial class OptionsProvider
    {
        [ComVisible(true)]
        public class GeneralOptions : BaseOptionPage<General> { }
    }

    public class General : BaseOptionModel<General>
    {
        [Category("General")]
        [DisplayName("Enable extension")]
        [Description("Enables or disables TailwindCSS extension features (Intellisense, build)")]
        [DefaultValue(true)]
        public bool UseTailwindCss { get; set; } = true;
        [Category("General")]
        [DisplayName("Default output file name")]
        [Description("Sets the default name of the built TailwindCSS file; use {0} if you want to reference the content file name (do not include extension)")]
        [DefaultValue("{0}.output.css")]
        public string TailwindOutputFileName { get; set; } = "{0}.output.css";
        [Category("General")]
        [DisplayName("TailwindCSS completions before all")]
        [Description("True if TailwindCSS completions come before all others; false if after")]
        [DefaultValue(true)]
        public bool TailwindCompletionsComeFirst { get; set; } = true;
        [Category("General")]
        [DisplayName("Automatically apply library updates")]
        [Description("Set to true if the TailwindCSS module should update on project load; false if not")]
        [DefaultValue(true)]
        public bool AutomaticallyUpdateLibrary { get; set; } = true;
    }
}
