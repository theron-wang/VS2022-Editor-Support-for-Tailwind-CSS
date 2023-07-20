using Microsoft.VisualStudio.Package;
using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Documents;
using TailwindCSSIntellisense.Settings;

namespace TailwindCSSIntellisense.Configuration
{
    /// <summary>
    /// MEF Component Class which provides a method to search for the Tailwind configuration file
    /// </summary>
    [Export]
    internal sealed class ConfigFileScanner
    {
        private string _configFilePath;

        [Import]
        internal FileFinder FileFinder { get; set; }

        [Import]
        internal SettingsProvider SettingsProvider { get; set; }

        internal bool HasConfigurationFile { get; set; }

        /// <summary>
        /// Searches through the solution to find a TailwindCSS configuration file.
        /// </summary>
        /// <returns>A <see cref="Task"/> of type <see cref="string" />, which represents the path to an existing configuration file, or null if one cannot be found</returns>
        internal async Task<string> FindConfigurationFilePathAsync(bool overrideCurrent = false)
        {
            if (_configFilePath != null && !overrideCurrent)
            {
                if (File.Exists(_configFilePath))
                {
                    return _configFilePath;
                }
            }

            HasConfigurationFile = false;

            // Must override default if settings is specified:

            var settings = await SettingsProvider.GetSettingsAsync();
            if (string.IsNullOrEmpty(settings.TailwindConfigurationFile) == false)
            {
                _configFilePath = settings.TailwindConfigurationFile;
                HasConfigurationFile = true;
                return _configFilePath;
            }

            var jsFiles = await FileFinder.GetJavascriptFilesAsync();

            // Best case scenario: user names file tailwind.config.js
            _configFilePath = jsFiles.FirstOrDefault(f => Path.GetFileName(f).Equals("tailwind.config.js"));

            if (_configFilePath != null)
            {
                HasConfigurationFile = true;
                return _configFilePath;
            }

            // Next: search all css files and scrape for @config

            // Check the smallest css files first since tailwind css files (should) be small
            var cssFiles = (await FileFinder.GetCssFilesAsync()).OrderBy(f => new FileInfo(f).Length).ToList();

            string cssTargetFile = null;

            foreach (var file in cssFiles)
            {
                if (await DoesFileContainAsync(file, "@config"))
                {
                    cssTargetFile = file;
                    break;
                }
            }

            if (cssTargetFile != null)
            {
                HasConfigurationFile = true;
                _configFilePath = await ExtractConfigJsPathAsync(cssTargetFile);
            }

            if (_configFilePath != null && File.Exists(_configFilePath))
            {
                HasConfigurationFile = true;
                return _configFilePath;
            }

            // Last case scenario: return first javascript file with "tailwind" in its name, or else, null

            _configFilePath = jsFiles.FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).ToLower().Contains("tailwind"));

            if (_configFilePath != null)
            {
                HasConfigurationFile = true;
                return _configFilePath;
            }
            else
            {
                return null;
            }
        }

        private async Task<bool> DoesFileContainAsync(string filePath, string text)
        {
            using (var fs = File.OpenRead(filePath))
            {
                using (var reader = new StreamReader(fs))
                {
                    var line = await reader.ReadLineAsync();

                    if (line.Contains(text))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private async Task<string> ExtractConfigJsPathAsync(string filePath)
        {
            string configLine = null;
            using (var fs = File.OpenRead(filePath))
            {
                using (var reader = new StreamReader(fs))
                {
                    var line = await reader.ReadLineAsync();

                    if (line.Contains("@config"))
                    {
                        configLine = line.Trim();
                        goto End;
                    }
                }
            }

        End:

            if (configLine == null)
            {
                return null;
            }
            var indexOfConfig = configLine.IndexOf("@config");
            var indexOfSemicolon = configLine.IndexOf(';', indexOfConfig);

            string scanText;
            if (indexOfSemicolon == -1)
            {
                scanText = configLine.Substring(indexOfConfig);
            }
            else
            {
                scanText = configLine.Substring(indexOfConfig, indexOfSemicolon - indexOfConfig);
            }

            try
            {
                var relPath = scanText.Split(' ')[1].Trim('\'').Trim('\"');
                
                // @config provides a relative path to configuration file
                // To find the path of the config file, we must take the relative path in terms
                // of the absolute path of the css file
                return Uri.UnescapeDataString(new Uri(new Uri(filePath), relPath).AbsolutePath);
            }
            catch
            {
                // @config syntax is invalid, cannot parse path
                return null;
            }
        }
    }
}
