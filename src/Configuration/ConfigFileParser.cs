using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.PlatformUI.OleComponentSupport;
using Microsoft.VisualStudio.Threading;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace TailwindCSSIntellisense.Configuration;

/// <summary>
/// Parses the TailwindCSS configuration file
/// </summary>
internal static class ConfigFileParser
{
    internal static async Task<JsonObject> GetConfigJsonNodeAsync(string path)
    {
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

        var nodePath = await GetNodeModulesFromConfigFilePathAsync(path);

        var globalPath = await GetGlobalPackageLocationAsync();

        var nodePathEnvironmentVariable = processInfo.EnvironmentVariables["NODE_PATH"];

        if (!string.IsNullOrWhiteSpace(nodePathEnvironmentVariable))
        {
            nodePathEnvironmentVariable += ";";
        }

        if (nodePath is not null && Directory.Exists(nodePath))
        {
            nodePathEnvironmentVariable += nodePath + ";";
        }

        if (!string.IsNullOrWhiteSpace(globalPath))
        {
            nodePathEnvironmentVariable += globalPath + ";";
        }

        if (!string.IsNullOrWhiteSpace(nodePathEnvironmentVariable))
        {
            nodePathEnvironmentVariable = nodePathEnvironmentVariable.TrimEnd(';');
        }

        processInfo.EnvironmentVariables["NODE_PATH"] = nodePathEnvironmentVariable;

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

    private static async Task<string> GetGlobalPackageLocationAsync()
    {
        ProcessStartInfo processStartInfo = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/C npm root -g",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using Process process = Process.Start(processStartInfo);

        var output = await process.StandardOutput.ReadToEndAsync();

        await process.WaitForExitAsync();

        return output.Trim();
    }

    /// <summary>
    /// Gets the configuration settings from the Tailwind CSS configuration file.
    /// </summary>
    /// <remarks>Returns null if the configuration file cannot be found, or if the configuration file does not have a 'theme' section.</remarks>
    /// <returns>Returns a <see cref="Task{TailwindConfiguration}" /> of type <see cref="TailwindConfiguration"/> which contains the parsed configuration information</returns>
    internal static Task<TailwindConfiguration> GetConfigurationAsync(string path)
    {
        if (Path.GetExtension(path) == ".css")
        {
            return GetCssConfigurationAsync(path);
        }
        else
        {
            return GetJavaScriptConfigurationAsync(path);
        }
    }

    private static async Task<TailwindConfiguration> GetCssConfigurationAsync(string path)
    {
        var fullText = "";

        using (var fileStream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            using var reader = new StreamReader(fileStream);

            fullText = await reader.ReadToEndAsync();
        }

        // Remove any text inside {}, since all theme values are in the base theme block
        // We need this because there may be multiple blocks on one line
        var themeTrimmed = new StringBuilder();

        var imports = new List<string>();
        var utilities = new Dictionary<string, string>();
        var variants = new Dictionary<string, string>();

        var level = 0;
        var inComment = false;

        var directive = "";
        var buildingDirective = false;

        var directiveParameter = "";
        var buildingDirectiveParameter = false;

        for (int i = 0; i < fullText.Length; i++)
        {
            var current = fullText[i];

            if (current == '/')
            {
                if (i + 1 < fullText.Length && fullText[i + 1] == '*')
                {
                    inComment = true;
                }
                else if (i > 0 && fullText[i - 1] == '*')
                {
                    inComment = false;
                    continue;
                }
            }

            if (inComment)
            {
                continue;
            }

            if (current == '{')
            {
                level++;
            }
            else if (current == '}')
            {
                level--;

                if (level == 0)
                {
                    directive = "";
                    directiveParameter = "";
                }
            }

            if (current == '@' && level == 0 && !buildingDirectiveParameter)
            {
                directive = "";
                buildingDirective = true;
                continue;
            }

            if (buildingDirective)
            {
                if (char.IsLetter(current) || current == '-')
                {
                    directive += current;
                }
                else
                {
                    buildingDirective = false;
                    buildingDirectiveParameter = true;
                    directiveParameter = "";
                }
                continue;
            }

            if (buildingDirectiveParameter)
            {
                if (current == ';')
                {
                    directiveParameter = directiveParameter.Trim();
                    buildingDirectiveParameter = false;

                    if (directive == "import")
                    {
                        var import = directiveParameter.Replace("\"", "").Replace("'", "").TrimEnd(';').Trim();
                        if (import != "tailwindcss" && !import.StartsWith("url"))
                        {
                            import = PathHelpers.GetAbsolutePath(Path.GetDirectoryName(path), import);
                            imports.Add($"@import{import}");
                        }
                        continue;
                    }
                    else if (directive == "config")
                    {
                        var import = directiveParameter.Replace("\"", "").Replace("'", "").TrimEnd(';').Trim();

                        import = PathHelpers.GetAbsolutePath(Path.GetDirectoryName(path), import);
                        imports.Add($"@config{import}");
                        continue;
                    }
                    // Handle short-hand:
                    // @custom-variant pointer-coarse (@media (pointer: coarse))
                    else if (directive == "custom-variant")
                    {
                        var splitAt = directiveParameter.IndexOf(' ');

                        if (splitAt == -1)
                        {
                            continue;
                        }

                        var variantName = directiveParameter.Substring(0, splitAt);
                        var variantValue = directiveParameter.Substring(splitAt + 1).Trim();

                        if (variantValue.StartsWith("(") && variantValue.EndsWith(")"))
                        {
                            variantValue = variantValue.Substring(1, variantValue.Length - 2);
                            variants[variantName] = $"{variantValue} {{ @slot; }}";
                        }

                        continue;
                    }

                    directive = "";
                    directiveParameter = "";
                }
                else if (current == '{')
                {
                    directiveParameter = directiveParameter.Trim();
                    buildingDirectiveParameter = false;
                }
                else
                {
                    directiveParameter += current;
                }
                continue;
            }

            if (level >= 1)
            {
                if (directive == "theme" && level == 1 && current != '{' && current != '}')
                {
                    themeTrimmed.Append(current);
                }
                else if (directive == "utility")
                {
                    if (!utilities.ContainsKey(directiveParameter))
                    {
                        utilities[directiveParameter] = "";
                    }

                    utilities[directiveParameter] += current.ToString();
                }
                else if (directive == "custom-variant")
                {
                    if (!variants.ContainsKey(directiveParameter))
                    {
                        variants[directiveParameter] = "";
                    }

                    variants[directiveParameter] += current.ToString();
                }
            }
        }

        var themeValuePairs = CssConfigSplitter.Split(themeTrimmed.ToString())
            .Select(s =>
            {
                var split = s.IndexOf(':');
                return new KeyValuePair<string, string>(s.Substring(0, split), s.Substring(split + 1));
            });

        TailwindConfiguration imported = null;

        foreach (var import in imports)
        {
            // Skips the @import or @config appended at the beginning
            var importPath = import.Substring(7);
            var prev = imported;

            if (import.StartsWith("@import"))
            {
                imported = await GetCssConfigurationAsync(importPath);
            }
            else if (import.StartsWith("@config"))
            {
                imported = await GetJavaScriptConfigurationAsync(importPath);
            }
            else
            {
                continue;
            }

            if (prev is not null)
            {
                DictionaryHelpers.MergeDictionaries(imported.OverridenValues, prev.OverridenValues);
                DictionaryHelpers.MergeDictionaries(imported.ExtendedValues, prev.ExtendedValues);
                DictionaryHelpers.MergeDictionaries(imported.ThemeVariables, prev.ThemeVariables);
                imported.PluginClasses.AddRange(prev.PluginClasses);
                imported.PluginVariants.AddRange(prev.PluginVariants);
                DictionaryHelpers.MergeDictionaries(imported.PluginDescriptions, prev.PluginDescriptions);
                DictionaryHelpers.MergeDictionaries(imported.PluginVariantDescriptions, prev.PluginVariantDescriptions);
            }
        }

        if (imported is not null)
        {
            imported.PluginClasses = imported.PluginClasses.Distinct().ToList();
            imported.PluginVariants = imported.PluginVariants.Distinct().ToList();
        }

        var config = new TailwindConfiguration
        {
            OverridenValues = [],
            ExtendedValues = [],
            ThemeVariables = [],
            PluginClasses = [..utilities.Keys],
            PluginVariants = [..variants.Keys],
            PluginDescriptions = utilities,
            // For variants, put everything on the same line so it looks fine in completion tooltip
            PluginVariantDescriptions = variants.ToDictionary(v => v.Key, v => 
                string.Join(" ", v.Value.Split(new char[] { }, StringSplitOptions.RemoveEmptyEntries)))
        };

        foreach (var pair in themeValuePairs)
        {
            var stem = GetConfigurationClassStemFromCssVariable(pair.Key);
            var namespaceStem = GetCssVariableNamespace(pair.Key);

            if (stem is null || namespaceStem is null)
            {
                config.ThemeVariables[pair.Key.Trim()] = pair.Value.Trim();
                continue;
            }

            var valueKey = pair.Key.Trim().Substring(namespaceStem.Length);

            if (pair.Key.EndsWith("*"))
            {
                config.OverridenValues[stem] = new Dictionary<string, object>();
                config.ExtendedValues[stem] = new Dictionary<string, object>();
                continue;
            }

            Dictionary<string, object> dict;

            if (config.OverridenValues.ContainsKey(stem))
            {
                dict = config.OverridenValues[stem] as Dictionary<string, object>;
            }
            else
            {
                if (!config.ExtendedValues.ContainsKey(stem))
                {
                    config.ExtendedValues[stem] = new Dictionary<string, object>();
                }
                dict = config.ExtendedValues[stem] as Dictionary<string, object>;
            }

            dict[valueKey] = pair.Value.Trim();
        }

        if (imported is not null)
        {
            DictionaryHelpers.MergeDictionaries(config.OverridenValues, imported.OverridenValues);
            DictionaryHelpers.MergeDictionaries(config.ExtendedValues, imported.ExtendedValues);
            DictionaryHelpers.MergeDictionaries(config.ThemeVariables, imported.ThemeVariables);
            config.PluginClasses = config.PluginClasses.Concat(imported.PluginClasses).Distinct().ToList();
            config.PluginVariants = config.PluginVariants.Concat(imported.PluginVariants).Distinct().ToList();
            DictionaryHelpers.MergeDictionaries(config.PluginDescriptions, imported.PluginDescriptions);
            DictionaryHelpers.MergeDictionaries(config.PluginVariantDescriptions, imported.PluginVariantDescriptions);
        }

        return config;
    }

    private static string GetCssVariableNamespace(string variable)
    {
        if (variable.StartsWith("--color-"))
        {
            return "--color-";
        }
        else if (variable.StartsWith("--font-weight-"))
        {
            return "--font-weight-";
        }
        else if (variable.StartsWith("--font-"))
        {
            return "--font-";
        }
        else if (variable.StartsWith("--text-"))
        {
            return "--text-";
        }
        else if (variable.StartsWith("--tracking-"))
        {
            return "--tracking-";
        }
        else if (variable.StartsWith("--leading-"))
        {
            return "--leading-";
        }
        else if (variable.StartsWith("--breakpoint-"))
        {
            return "--breakpoint-";
        }
        else if (variable.StartsWith("--container-"))
        {
            // v4
            return "--container-";
        }
        else if (variable.StartsWith("--spacing-"))
        {
            return "--spacing-";
        }
        else if (variable.StartsWith("--radius-"))
        {
            return "--radius-";
        }
        else if (variable.StartsWith("--shadow-"))
        {
            return "--shadow-";
        }
        else if (variable.StartsWith("--inset-shadow-"))
        {
            // v4
            return "--inset-shadow-";
        }
        else if (variable.StartsWith("--drop-shadow-"))
        {
            return "--drop-shadow-";
        }
        else if (variable.StartsWith("--blur-"))
        {
            return "--blur-";
        }
        else if (variable.StartsWith("--perspective-"))
        {
            // v4
            return "--perspective-";
        }
        else if (variable.StartsWith("--aspect-"))
        {
            return "--aspect-";
        }
        else if (variable.StartsWith("--ease-"))
        {
            return "--ease-";
        }
        else if (variable.StartsWith("--animate-"))
        {
            return "--animate-";
        }

        return null;
    }

    private static string GetConfigurationClassStemFromCssVariable(string variable)
    {
        if (variable.StartsWith("--color-"))
        {
            return "colors";
        }
        else if (variable.StartsWith("--font-weight-"))
        {
            return "fontWeight";
        }
        else if (variable.StartsWith("--font-"))
        {
            return "fontFamily";
        }
        else if (variable.StartsWith("--text-"))
        {
            return "fontSize";
        }
        else if (variable.StartsWith("--tracking-"))
        {
            return "letterSpacing";
        }
        else if (variable.StartsWith("--leading-"))
        {
            return "lineHeight";
        }
        else if (variable.StartsWith("--breakpoint-"))
        {
            return "screens";
        }
        else if (variable.StartsWith("--container-"))
        {
            // v4
            return "v4-container";
        }
        else if (variable.StartsWith("--spacing-"))
        {
            return "spacing";
        }
        else if (variable.StartsWith("--radius-"))
        {
            return "borderRadius";
        }
        else if (variable.StartsWith("--shadow-"))
        {
            return "boxShadow";
        }
        else if (variable.StartsWith("--inset-shadow-"))
        {
            // v4
            return "v4-insetShadow";
        }
        else if (variable.StartsWith("--drop-shadow-"))
        {
            return "dropShadow";
        }
        else if (variable.StartsWith("--blur-"))
        {
            return "blur";
        }
        else if (variable.StartsWith("--perspective-"))
        {
            // v4
            return "v4-perspective";
        }
        else if (variable.StartsWith("--aspect-"))
        {
            return "aspectRatio";
        }
        else if (variable.StartsWith("--ease-"))
        {
            return "transitionTimingFunction";
        }
        else if (variable.StartsWith("--animate-"))
        {
            return "animation";
        }

        return null;
    }

    private static async Task<TailwindConfiguration> GetJavaScriptConfigurationAsync(string path)
    {
        var obj = await GetConfigJsonNodeAsync(path);

        if (obj is null)
        {
            return new TailwindConfiguration
            {
                OverridenValues = [],
                ExtendedValues = []
            };
        }

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
            config.PluginVariants = plugins.ContainsKey("variants") ? (List<string>)plugins["variants"] : null;

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

    private static async Task<string> GetNodeModulesFromConfigFilePathAsync(string configPath)
    {
        var file = await PhysicalFile.FromFileAsync(configPath);

        if (file?.ContainingProject is null)
        {
            return null;
        }

        var project = file.ContainingProject;

        // Search for the node_modules folder in the project; start from the configuration file and
        // go up the directory tree (stopping at the project root) until the node_modules folder is found.

        var currentDirectory = Path.GetDirectoryName(configPath).ToLower();

        var endDirectory = Path.GetDirectoryName(project.FullPath).ToLower();

        while (currentDirectory.StartsWith(endDirectory))
        {
            var nodeModulesPath = Path.Combine(currentDirectory, "node_modules");

            if (Directory.Exists(nodeModulesPath))
            {
                return nodeModulesPath;
            }

            currentDirectory = Path.GetDirectoryName(currentDirectory).ToLower();
        }

        return null;
    }

    private static Dictionary<string, object> GetTotalValue(JsonNode node, string ignoreKey = null)
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

    private static ICollection<string> GetKeys(JsonNode obj)
    {
        return ((IDictionary<string, JsonNode>)obj).Keys;
    }

    private static JsonValueKind GetValueKind(JsonNode node)
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
