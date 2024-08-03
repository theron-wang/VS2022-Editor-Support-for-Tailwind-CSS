using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using TailwindCSSIntellisense.Configuration;

namespace TailwindCSSIntellisense.ClassSort.Sorters;
[Export(typeof(Sorter))]
internal class RazorSorter : Sorter
{
    public override string[] Handled { get; } = [".razor", ".cshtml"];
    
    protected override IEnumerable<string> GetSegments(string file, TailwindConfiguration config)
    {
        (int indexOfClass, char terminator) = GetNextIndexOfClass(file, 0);

        int lastIndex = 0;

        while (indexOfClass != -1)
        {
            // Verify that we are in an HTML tag
            var closeAngleBracket = file.LastIndexOf('>', indexOfClass);
            var openAngleBracket = file.LastIndexOf('<', indexOfClass);

            if (openAngleBracket == -1 || closeAngleBracket > openAngleBracket)
            {
                (indexOfClass, terminator) = GetNextIndexOfClass(file, indexOfClass + 1);
                continue;
            }

            yield return file.Substring(lastIndex, indexOfClass - lastIndex);

            lastIndex = file.IndexOf(terminator, indexOfClass) + 1;

            bool isInRazor = false;
            int depth = 0;
            // Number of quotes (excluding \")
            // Odd if in string context, even if not
            int numberOfQuotes = 0;
            bool isEscaping = false;

            List<Token> tokens = [];
            string totalText = "";
            // A list of numbers that represent where a new line should be inserted;
            // each number represents a text token in tokens
            var newLines = new HashSet<int>();

            while (lastIndex < file.Length && (depth != 0 || numberOfQuotes % 2 == 1 || terminator != file[lastIndex]))
            {
                var character = file[lastIndex];

                totalText += character;

                if (!isInRazor)
                {
                    if (char.IsWhiteSpace(character))
                    {
                        if (character == '\n')
                        {
                            // Insert directly after the current token
                            // If the current token count is 1, then it should
                            // apply after index 0
                            newLines.Add(tokens.Count - 1);
                        }
                        if (string.IsNullOrWhiteSpace(totalText) == false)
                        {
                            tokens.Add(new Token(totalText.Trim(), isInRazor));
                        }
                        totalText = "";
                    }
                }

                if (character == '@')
                {
                    isInRazor = true;
                }
                else if (isInRazor)
                {
                    bool escape = isEscaping;
                    isEscaping = false;

                    if (numberOfQuotes % 2 == 1)
                    {
                        if (character == '\\')
                        {
                            isEscaping = true;
                        }
                    }
                    else
                    {
                        if (character == '(')
                        {
                            depth++;
                        }
                        else if (character == ')')
                        {
                            depth--;
                        }
                    }

                    if (character == '"' && !escape)
                    {
                        numberOfQuotes++;
                    }

                    if (depth == 0 && numberOfQuotes % 2 == 0 && char.IsWhiteSpace(character))
                    {
                        tokens.Add(new Token(totalText.Trim(), isInRazor));
                        isInRazor = false;
                        totalText = "";
                    }
                }

                lastIndex++;
            }

            if (string.IsNullOrWhiteSpace(totalText) == false)
            {
                tokens.Add(new Token(totalText.Trim(), isInRazor));
            }

            if (lastIndex >= file.Length)
            {
                yield return file.Substring(indexOfClass);
                yield break;
            }

            // return class=" or class='
            yield return file.Substring(indexOfClass, 7);

            bool inside = false;
            bool sortedYet = false;

            char lookFor = file[indexOfClass + 6] == '"' ? '\'' : '"';
            int token = 0;

            var textTokens = new List<string>();
            var razorIndices = new HashSet<int>();

            int startToken = -1;

            while (token < tokens.Count)
            {
                int index = tokens[token].Text.IndexOf(lookFor);
                if (index == -1 || tokens[token].IsInRazor)
                {
                    textTokens.Add(tokens[token].Text);

                    if (tokens[token].IsInRazor)
                    {
                        razorIndices.Add(textTokens.Count - 1);
                    }

                    token++;
                    continue;
                }

                var from = 0;
                while (index != -1)
                {
                    if (index == 0 || tokens[token].Text[index - 1] != '\\')
                    {
                        if (inside)
                        {
                            if (index == 0)
                            {
                                yield return lookFor + string.Join(" ", SortRazorSegment(textTokens, razorIndices, config));
                            }
                            else
                            {
                                var f = (from == 0 && startToken != token) ? 0 : from + 1;
                                textTokens.Add(tokens[token].Text.Substring(f, index - f));
                                yield return lookFor + string.Join(" ", SortRazorSegment(textTokens, razorIndices, config));
                            }
                            sortedYet = true;
                            inside = false;
                            startToken = -1;
                            textTokens.Clear();
                            razorIndices.Clear();
                        }
                        else
                        {
                            textTokens.Add(tokens[token].Text.Substring(from, index - from));
                            yield return string.Join(" ", textTokens);
                            inside = true;
                            startToken = token;
                            textTokens.Clear();
                            razorIndices.Clear();
                        }
                        from = index;
                    }

                    index = tokens[token].Text.IndexOf(lookFor, index + 1);

                    if (index == -1)
                    {
                        if (inside)
                        {
                            textTokens.Add(tokens[token].Text.Substring(from + 1));
                        }
                        else
                        {
                            textTokens.Add(tokens[token].Text.Substring(from));
                        }
                    }
                }

                token++;
            }

            if (sortedYet)
            {
                var text = new StringBuilder();

                for (int i = 0; i < textTokens.Count; i++)
                {
                    text.Append(textTokens[i]);
                    if (newLines.Contains(i))
                    {
                        text.AppendLine();
                    }
                    else
                    {
                        text.Append(' ');
                    }
                }

                yield return text.ToString().Trim();
            }
            else
            {
                var sorted = SortRazorSegment(textTokens, razorIndices, config).ToList();
                var text = new StringBuilder();

                for (int i = 0; i < sorted.Count; i++)
                {
                    text.Append(sorted[i]);
                    if (newLines.Contains(i))
                    {
                        text.AppendLine();
                    }
                    else
                    {
                        text.Append(' ');
                    }
                }

                yield return text.ToString().Trim();
            }

            (indexOfClass, terminator) = GetNextIndexOfClass(file, indexOfClass + 1);
        }

        yield return file.Substring(lastIndex);
    }

    private class Token(string text, bool isInRazor)
    {
        public string Text { get; set; } = text;
        public bool IsInRazor { get; set; } = isInRazor;
    }
}
