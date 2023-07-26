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
        [Description("Enables or disables Tailwind CSS extension features (Intellisense, build)")]
        [DefaultValue(true)]
        public bool UseTailwindCss { get; set; } = true;
        [Category("General")]
        [DisplayName("Tailwind CSS completions before all")]
        [Description("True if Tailwind CSS completions come before all others; false if after")]
        [DefaultValue(true)]
        public bool TailwindCompletionsComeFirst { get; set; } = true;
        [Category("General")]
        [DisplayName("Automatically apply library updates")]
        [Description("Set to true if the Tailwind CSS module should update on project load; false if not")]
        [DefaultValue(true)]
        public bool AutomaticallyUpdateLibrary { get; set; } = true;
        [Category(category: "Build")]
        [DisplayName("Default output file name")]
        [Description("Sets the default name of the built Tailwind CSS file; use {0} if you want to reference the content file name")]
        [DefaultValue("{0}.output.css")]
        public string TailwindOutputFileName { get; set; } = "{0}.output.css";
        [Category(category: "Build")]
        [DisplayName("Build type")]
        [Description("Files can be built in three ways: Default (Tailwind JIT), OnSave (manually on file save, more reliable but may come with a performance cost), and None (no building)")]
        [TypeConverter(typeof(EnumConverter))]
        [DefaultValue(BuildProcessOptions.Default)]
        public BuildProcessOptions BuildProcessType { get; set; } = BuildProcessOptions.Default;
    }

    public enum BuildProcessOptions
    {
        Default,
        OnSave,
        None
    }
}
