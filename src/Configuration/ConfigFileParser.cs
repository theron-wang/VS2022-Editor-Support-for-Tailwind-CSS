using Microsoft.VisualStudio.Threading;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
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

            var scriptLocation = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Resources", "parser.js");

            var processInfo = new ProcessStartInfo()
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                FileName = "cmd",
                Arguments = $"/c node \"{scriptLocation}\" \"{Path.GetFileName(path)}\"",
                WorkingDirectory = Path.GetDirectoryName(path)
            };

            var nodePath = processInfo.WorkingDirectory;

            if (Directory.Exists(Path.Combine(nodePath, "node_modules")))
            {
                processInfo.EnvironmentVariables.Add("NODE_PATH", Path.Combine(nodePath, "node_modules"));
            }
            using var process = Process.Start(processInfo);
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

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

            if (obj.Count == 1 && obj.ContainsKey("default"))
            {
                obj = obj["default"].AsObject();
            }

            var theme = obj["theme"];

            var plugins = GetTotalValue(obj["plugins"]) ?? [];  

            var config = new TailwindConfiguration
            {
                OverridenValues = theme is null ? [] : GetTotalValue(theme, "extend") ?? [],
                ExtendedValues = theme is null ? [] : GetTotalValue(theme["extend"]) ?? [],
                Prefix = obj["prefix"]?.ToString(),
            };

            try
            {
                config.PluginModifiers = plugins.ContainsKey("modifiers") ? (List<string>)plugins["modifiers"] : null;

                if (plugins.ContainsKey("classes"))
                {
                    config.PluginClasses = new List<string>();
                    var classes = (List<string>)plugins["classes"];

                    foreach (var item in classes)
                    {
                        if (item.Contains("@media") || item.Contains("@font-face") || item.Contains("@keyframes") || item.Contains("@supports"))
                        {
                            continue;
                        }
                        var commaSplitClasses = item.Split(new string[] { ",", " .", "." }, StringSplitOptions.RemoveEmptyEntries);

                        foreach (var className in commaSplitClasses)
                        {
                            string toAdd = className.Trim();
                            if (toAdd.StartsWith("[") && toAdd.EndsWith("]"))
                            {
                                continue;
                            }
                            if (toAdd.Contains('[') && toAdd.IndexOf('[') != toAdd.IndexOf("-[") + 1)
                            {
                                toAdd = toAdd.Substring(0, toAdd.IndexOf('['));
                            }
                            if (toAdd.Contains(':'))
                            {
                                toAdd = toAdd.Substring(0, toAdd.IndexOf(':'));
                            }
                            if (toAdd.Contains(' '))
                            {
                                toAdd = toAdd.Substring(0, toAdd.IndexOf(' '));
                            }

                            toAdd = toAdd.TrimEnd(')');

                            if (config.PluginClasses.Contains(toAdd) == false && string.IsNullOrEmpty(toAdd) == false)
                            {
                                config.PluginClasses.Add(toAdd);
                            }
                        }
                    }
                }
                else
                {
                    config.PluginClasses = null;
                }

                if (obj["plugins"]?["descriptions"] is not null)
                {
                    config.PluginDescriptions = JsonSerializer.Deserialize<Dictionary<string, string>>(obj["plugins"]["descriptions"]);
                }
            }
            catch (Exception ex)
            {
                await ex.LogAsync();
            }

            try
            {
                if (obj["blocklist"] is not null)
                {
                    config.Blocklist = JsonSerializer.Deserialize<List<string>>(obj["blocklist"]);
                }
            }
            catch (Exception ex)
            {
                await ex.LogAsync();
            }
            

            try
            {
                if (obj["corePlugins"] is not null)
                {
                    if (GetValueKind(obj["corePlugins"]) == JsonValueKind.Array)
                    {
                        config.EnabledCorePlugins = JsonSerializer.Deserialize<List<string>>(obj["corePlugins"]);
                    }
                    else
                    {
                        config.DisabledCorePlugins = JsonSerializer.Deserialize<Dictionary<string, bool>>(obj["corePlugins"])
                            .Where(kvp => !kvp.Value)
                            .Select(kvp => kvp.Key)
                            .ToList();
                    }
                }
            }
            catch (Exception ex)
            {
                await ex.LogAsync();
            }

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
