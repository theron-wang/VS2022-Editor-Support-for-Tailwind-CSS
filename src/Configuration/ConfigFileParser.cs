using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;
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

            var stringBuilder = new StringBuilder();
            var isInComment = false;

            foreach (var l in fileText.Split('\n'))
            {
                var line = l.Trim();
                var alreadyProcessed = false;
                if (l.Contains("/*") && isInComment == false)
                {
                    line = line.Split(new string[] { "/*" }, StringSplitOptions.None)[0];

                    if (string.IsNullOrWhiteSpace(line) == false)
                    {
                        stringBuilder.AppendLine(line);
                    }

                    alreadyProcessed = true;
                    isInComment = true;
                }
                if (l.Contains("//") && isInComment == false)
                {
                    line = line.Split(new string[] { "//" }, StringSplitOptions.None)[0];

                    if (string.IsNullOrWhiteSpace(line) == false)
                    {
                        stringBuilder.AppendLine(line);
                    }
                    alreadyProcessed = true;
                }
                if (l.Contains("*/") && isInComment) 
                {
                    line = l.Split(new string[] { "*/" }, StringSplitOptions.None).Last();

                    if (string.IsNullOrWhiteSpace(line) == false)
                    {
                        stringBuilder.AppendLine(line);
                    }

                    alreadyProcessed = true;
                    isInComment = false;
                }
                if (alreadyProcessed == false && isInComment == false)
                {

                    stringBuilder.AppendLine(line);
                }
            }

            fileText = stringBuilder.ToString();

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


            /*
             * backgroundSize: ({ theme }) => ({
                auto: 'auto',
                cover: 'cover',
                contain: 'contain',
                ...theme('spacing')
            })
             */

            scope = scope.Substring(cutoff);

            /*({ theme }) => ({
                auto: 'auto',
                cover: 'cover',
                contain: 'contain',
                ...theme('spacing')
            }),
            borderRadius: {
            }
             */

            var nearestColon = scope.IndexOf(':');
            var nearestArrow = scope.IndexOf("=>");
            var nearestTheme = scope.IndexOf("theme");
            var needToTrimEndingParenthesis = false;
            // If we are looking at the theme base block then this will be true and we don't want that
            if (nearestArrow != -1 && nearestTheme != -1 && nearestColon < nearestTheme && nearestTheme < nearestArrow)
            {
                var nextOpenBracket = scope.IndexOf('{', nearestTheme);
                scope = scope.Substring(nextOpenBracket);
                needToTrimEndingParenthesis = true;
            }

            var index = scope.IndexOf('{') + 1;

            int nearestTerminator = scope.IndexOfAny(new char[] { ',', '}' });

            var nearestStartQuote = scope.IndexOfAny(new char[] { '\'', '"', '`' }, nearestColon);
            if (nearestStartQuote < nearestTerminator && nearestStartQuote != -1)
            {
                var nearestEndQuote = scope.IndexOfAny(new char[] { '\'', '"', '`' }, nearestStartQuote + 1);

                if (nearestEndQuote != -1)
                {
                    nearestTerminator = scope.IndexOfAny(new char[] { ',', '}' }, nearestEndQuote);
                }
            }

            if (nearestTerminator == -1)
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

            /*{
                auto: 'auto',
                cover: 'cover',
                contain: 'contain',
                ...theme('spacing')
            })
             */
            if (needToTrimEndingParenthesis)
            {
                scope = scope.Trim().TrimEnd(')');
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
            char? punct = null;

            while (blocksIn != 0 && index < scope.Length)
            {
                if (scope[index] == ':' && blocksIn == 1)
                {
                    string key = "";
                    for (int i = index - 1; IsCharAcceptedLetter(scope[i]); i--)
                    {
                        key = scope[i] + key;
                    }

                    keys.Add(key.Trim());
                }

                if (scope[index] == '{' && punct == null)
                {
                    blocksIn++;
                }
                else if (scope[index] == '}' && punct == null)
                {
                    blocksIn--;
                }
                else if (scope[index] == '\'')
                {
                    if (punct == null)
                    {
                        punct = '\'';
                    }
                    else if (punct == '\'')
                    {
                        punct = null;
                    }
                }
                else if (scope[index] == '"')
                {
                    if (punct == null)
                    {
                        punct = '"';
                    }
                    else if (punct == '"')
                    {
                        punct = null;
                    }
                }
                else if (scope[index] == '`')
                {
                    if (punct == null)
                    {
                        punct = '`';
                    }
                    else if (punct == '`')
                    {
                        punct = null;
                    }
                }
                index++;
            }

            return keys;
        }

        private bool IsCharAcceptedLetter(char character)
        {
            return char.IsLetterOrDigit(character) || character == '\'' || character == '"' || character == '-' || character == '_' || character == '/';
        }

        private Dictionary<string, object> GetTotalValue(string scope)
        {
            var result = new Dictionary<string, object>();

            foreach (var key in GetKeys(scope))
            {
                var value = GetBlockOrValue(scope, key, out bool isBlock);

                if (isBlock)
                {
                    result[key.Trim('\'', '"')] = GetTotalValue(value);
                }
                else if (string.IsNullOrWhiteSpace(value) == false)
                {
                    result[key.Trim('\'', '"')] = value.Trim('\'', '"');
                }
            }

            return result;
        }
    }
}
