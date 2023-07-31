using System.ComponentModel.Composition;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using TailwindCSSIntellisense.Configuration;

namespace TailwindCSSIntellisense.Node
{
    [Export]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal sealed class PackageJsonReader
    {
        [Import]
        internal ConfigFileScanner ConfigFileScanner { get; set; }

        internal async Task<(string script, string fileName)> GetScriptAsync(string scriptName)
        {
            if (scriptName == null)
            {
                return (script: null, fileName: null);
            }

            var configFileName = await ConfigFileScanner.FindConfigurationFilePathAsync();

            if (configFileName == null)
            {
                return (script: null, fileName: null);
            }

            var packageJsonFileName = Path.Combine(Path.GetDirectoryName(configFileName), "package.json");

            if (packageJsonFileName == null || File.Exists(packageJsonFileName) == false)
            {
                return (script: null, fileName: null);
            }

            JsonObject file;
            
            using (var fs = File.Open(packageJsonFileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                file = await JsonSerializer.DeserializeAsync<JsonObject>(fs);
            }

            return (script: file["scripts"]?[scriptName]?.ToString(), fileName: packageJsonFileName);
        }
    }
}
