using Microsoft.VisualStudio.Threading;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace TailwindCSSIntellisense.Configuration
{
    /// <summary>
    /// Parses the TailwindCSS configuration file
    /// </summary>
    [Export]
    internal sealed class ConfigFileParser
    {
        [Import]
        internal ConfigFileScanner Scanner { get; set; }

        internal async Task<JsonObject> GetConfigJsonNodeAsync()
        {
            var path = await Scanner.FindConfigurationFilePathAsync();

            if (path is null)
            {
                return null;
            }

            var processInfo = new ProcessStartInfo()
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                FileName = "cmd",
                Arguments = "/c node",
                WorkingDirectory = Path.GetDirectoryName(path)
            };

            var process = Process.Start(processInfo);
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            var command = $@"console.log(
    JSON.stringify(require('{path.Replace('\\', '/')}'),
        (key, value) => {{
            if (key.toLowerCase() === 'plugins') {{
                return undefined;
            }}
            return typeof value === 'function' ? value({{
                theme: (key) => {{
                    var defaultTheme = require('tailwindcss/defaultTheme');
                    var custom = require('{path.Replace('\\', '/')}');

                    return {{ ...defaultTheme[key], ...custom.theme[key], ...custom.theme.extend[key] }};
                }}
            }}) : value
        }})
);";

            var file = new StringBuilder();

            process.OutputDataReceived += (object sender, DataReceivedEventArgs e) =>
            {
                if (string.IsNullOrEmpty(e.Data) == false)
                {
                    file.AppendLine(e.Data);
                }
            };
            var hasError = false;
            var error = new StringBuilder();
            process.ErrorDataReceived += (object sender, DataReceivedEventArgs e) =>
            {
                if (string.IsNullOrEmpty(e.Data) == false && e.Data.Contains("warn") == false)
                {
                    hasError = true;
                    error.AppendLine(e.Data);
                }
            };
            await process.StandardInput.WriteLineAsync(command);
            process.StandardInput.Close();
            await process.WaitForExitAsync();

            if (hasError)
            {
                throw new InvalidOperationException("Error occurred while parsing configuration file: " + error.ToString().Trim());
            }

            var fileText = file.ToString().Trim();

            return JsonSerializer.Deserialize<JsonObject>(fileText);
        }

        /// <summary>
        /// Gets the configuration settings from the TailwindCSS configuration file.
        /// </summary>
        /// <remarks>Returns null if the configuration file cannot be found, or if the configuration file does not have a 'theme' section.</remarks>
        /// <returns>Returns a <see cref="Task{TailwindConfiguration}" /> of type <see cref="TailwindConfiguration"/> which contains the parsed configuration information</returns>
        internal async Task<TailwindConfiguration> GetConfigurationAsync()
        {
            var obj = await GetConfigJsonNodeAsync();

            var theme = obj["theme"];

            if (theme == null)
            {
                return null;
            }

            var config = new TailwindConfiguration
            {
                OverridenValues = GetTotalValue(theme, "extend") ?? new Dictionary<string, object>(),
                ExtendedValues = GetTotalValue(theme["extend"]) ?? new Dictionary<string, object>(),
                Prefix = obj["prefix"]?.ToString()
            };

            return config;
        }

        private Dictionary<string, object> GetTotalValue(JsonNode node, string ignoreKey = null)
        {
            if (node == null)
            {
                return null;
            }

            var result = new Dictionary<string, object>();

            if (GetValueKind(node) == JsonValueKind.Object)
            {
                foreach (var key in GetKeys(node))
                {
                    if (key == ignoreKey)
                    {
                        continue;
                    }
                    var valueKind = GetValueKind(node[key]);
                    if (valueKind == JsonValueKind.Object)
                    {
                        result[key] = GetTotalValue(node[key]);
                    }
                    else if (valueKind == JsonValueKind.Array)
                    {
                        result[key] = node[key].AsArray().Select(n => n.ToString()).ToList();
                    }
                    else if (valueKind != JsonValueKind.Null)
                    {
                        result[key] = node[key].ToString().Trim();
                    }
                }
            }

            return result;
        }

        private ICollection<string> GetKeys(JsonNode obj)
        {
            return ((IDictionary<string, JsonNode>)obj).Keys;
        }

        private JsonValueKind GetValueKind(JsonNode node)
        {
            if (node is JsonObject)
            {
                return JsonValueKind.Object;
            }
            else if (node is JsonArray)
            {
                return JsonValueKind.Array;
            }
            else if (node is null)
            {
                return JsonValueKind.Null;
            }

            var value = node.GetValue<JsonElement>();

            return value.ValueKind;
        }
    }
}
