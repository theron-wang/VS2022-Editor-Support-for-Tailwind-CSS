using System.ComponentModel.Composition;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using TailwindCSSIntellisense.Configuration;
using TailwindCSSIntellisense.Settings;

namespace TailwindCSSIntellisense.Node
{
    [Export]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal sealed class PackageJsonReader
    {
        [Import]
        internal ConfigFileScanner ConfigFileScanner { get; set; }

        [Import]
        internal SettingsProvider SettingsProvider { get; set; }

        internal async Task<(bool exists, string fileName)> ScriptExistsAsync(string scriptName)
        {
            if (scriptName == null)
            {
                return (exists: false, fileName: null);
            }

            var settings = await SettingsProvider.GetSettingsAsync();
            string packageJsonFileName;

            if (settings.PackageConfigurationFile is not null && File.Exists(settings.PackageConfigurationFile))
            {
                packageJsonFileName = settings.PackageConfigurationFile;
            }
            else
            {
                var configFileName = await ConfigFileScanner.FindConfigurationFilePathAsync();

                if (configFileName == null)
                {
                    return (exists: false, fileName: null);
                }

                packageJsonFileName = Path.Combine(Path.GetDirectoryName(configFileName), "package.json");

                if (packageJsonFileName == null || File.Exists(packageJsonFileName) == false)
                {
                    return (exists: false, fileName: null);
                }
            }

            try
            {
                JsonObject file;

                using (var fs = File.Open(packageJsonFileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    file = await JsonSerializer.DeserializeAsync<JsonObject>(fs);
                }

                return (exists: file["scripts"]?[scriptName] != null, fileName: packageJsonFileName);
            }
            catch
            {
                return (exists: false, fileName: packageJsonFileName);
            }
        }
    }
}
