using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
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

        /// <summary>
        /// Gets the configuration settings from the TailwindCSS configuration file.
        /// </summary>
        /// <remarks>Returns null if the configuration file cannot be found, or if the configuration file does not have a 'theme' section.</remarks>
        /// <returns>Returns a <see cref="Task{TailwindConfiguration}" /> of type <see cref="TailwindConfiguration"/> which contains the parsed configuration information</returns>
        internal async Task<TailwindConfiguration> GetConfigurationAsync()
        {
            var path = await Scanner.FindConfigurationFilePathAsync();

            if (path is null)
            {
                return null;
            }

            string fileText;

            using (var fileStream = File.OpenRead(path))
            {
                using (var reader = new StreamReader(fileStream))
                {
                    fileText = await reader.ReadToEndAsync();
                }
            }

            var mainBlock = GetBlockOrValue(fileText, "theme", out _);

            if (mainBlock is null)
            {
                return null;
            }

            var extend = GetBlockOrValue(mainBlock, "extend", out _);

            var config = new TailwindConfiguration();

            if (extend != null)
            {
                mainBlock = mainBlock.Replace(extend, "");

                config.ExtendedValues = GetTotalValue(extend);
            }

            config.OverridenValues = GetTotalValue(mainBlock);

            return config;
        }

        private string GetBlockOrValue(string scope, string key, out bool isBlock)
        {
            var cutoff = scope.IndexOf($"{key}:");

            if (cutoff == -1)
            {
                isBlock = false;
                return null;
            }

            scope = scope.Substring(cutoff);

            var index = scope.IndexOf('{') + 1;
            var nearestComma = scope.IndexOf(',');
            var nearestCloseBracket = scope.IndexOf('}');
            int nearestTerminator;

            if (nearestComma == -1 && nearestCloseBracket != -1)
            {
                nearestTerminator = nearestCloseBracket;
            }
            else if (nearestComma != -1 && nearestCloseBracket == -1)
            {
                nearestTerminator = nearestComma;
            }
            else if (nearestComma != -1 && nearestCloseBracket != -1)
            {
                nearestTerminator = Math.Min(nearestComma, nearestCloseBracket);
            }
            else
            {
                nearestTerminator = scope.Length - 1;
            }

            // Item is innermost element, return value instead of block
            if (index == 0 || nearestTerminator < index)
            {
                isBlock = false;
                var colon = scope.IndexOf(':') + 1;
                return scope.Substring(colon, nearestTerminator - colon).TrimEnd('}').Trim();
            }

            var blocksIn = 1;

            while (blocksIn != 0 && index < scope.Length)
            {
                if (scope[index] == '{')
                {
                    blocksIn++;
                }
                else if (scope[index] == '}')
                {
                    blocksIn--;
                }
                index++;
            }

            if (index < scope.Length)
            {
                index++;
            }
            var start = scope.IndexOf('{');
            isBlock = true;
            return scope.Substring(start == -1 ? 0 : start, index - scope.IndexOf('{') - 1);
        }

        private List<string> GetKeys(string scope)
        {
            var index = scope.IndexOf('{') + 1;
            var blocksIn = 1;

            var keys = new List<string>();

            while (blocksIn != 0 && index < scope.Length)
            {
                if (scope[index] == ':' && blocksIn == 1)
                {
                    char c = scope[index - 1];
                    string key = "";
                    for (int i = index - 1; IsCharAcceptedLetter(c); i--)
                    {
                        c = scope[i];
                        key = c + key;
                    }

                    keys.Add(key.Trim());
                }

                if (scope[index] == '{')
                {
                    blocksIn++;
                }
                else if (scope[index] == '}')
                {
                    blocksIn--;
                }
                index++;
            }

            return keys;
        }

        private bool IsCharAcceptedLetter(char character)
        {
            return char.IsLetterOrDigit(character) || character == '\'' || character == '-' || character == '_';
        }

        private Dictionary<string, object> GetTotalValue(string scope)
        {
            var result = new Dictionary<string, object>();

            foreach (var key in GetKeys(scope))
            {
                var value = GetBlockOrValue(scope, key, out bool isBlock);

                if (isBlock)
                {
                    result[key.Trim('\'')] = GetTotalValue(value);
                }
                else if (string.IsNullOrWhiteSpace(value) == false)
                {
                    result[key.Trim('\'')] = value.Trim('\'');
                }
            }

            return result;
        }
    }
}
