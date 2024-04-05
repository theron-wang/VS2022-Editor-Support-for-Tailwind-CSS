using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using TailwindCSSIntellisense.Completions;
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

            List<string> tokens = [];
            string totalText = "";

            while (lastIndex < file.Length && (depth != 0 || numberOfQuotes % 2 == 1 || terminator != file[lastIndex]))
            {
                var character = file[lastIndex];

                totalText += character;

                if (!isInRazor)
                {
                    if (char.IsWhiteSpace(character))
                    {
                        if (string.IsNullOrWhiteSpace(totalText) == false)
                        {
                            tokens.Add(totalText.Trim());
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

                    if (depth == 0 && numberOfQuotes % 2 == 0 && character == ' ')
                    {
                        isInRazor = false;
                        tokens.Add(totalText.Trim());
                        totalText = "";
                    }
                }

                lastIndex++;
            }

            if (string.IsNullOrWhiteSpace(totalText) == false)
            {
                tokens.Add(totalText.Trim());
            }

            if (lastIndex >= file.Length)
            {
                yield return file.Substring(indexOfClass);
                yield break;
            }

            // return class=" or class='
            yield return file.Substring(indexOfClass, 7);
            yield return SortSegment(tokens, config);
            (indexOfClass, terminator) = GetNextIndexOfClass(file, indexOfClass + 1);
        }

        yield return file.Substring(lastIndex);
    }
}
