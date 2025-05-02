using System.Collections.Generic;
using System.Text.RegularExpressions;

public static class BraceExpander
{
    public static List<string> Expand(string input)
    {
        var results = new HashSet<string>();
        ExpandRecursive(input, results);
        return new List<string>(results);
    }

    private static void ExpandRecursive(string input, HashSet<string> results)
    {
        var match = Regex.Match(input, @"\{([^{}]*)\}");
        if (!match.Success)
        {
            results.Add(input);
            return;
        }

        var prefix = input.Substring(0, match.Index);
        var suffix = input.Substring(match.Index + match.Length);
        var options = match.Groups[1].Value;

        foreach (var option in ExpandOptions(options))
        {
            ExpandRecursive(prefix + option + suffix, results);
        }
    }

    private static IEnumerable<string> ExpandOptions(string options)
    {
        // Handle range with step (e.g., 1..10..2)
        var rangeMatch = Regex.Match(options, @"^(\d+)\.\.(\d+)(?:\.\.(\d+))?$");
        if (rangeMatch.Success)
        {
            int start = int.Parse(rangeMatch.Groups[1].Value);
            int end = int.Parse(rangeMatch.Groups[2].Value);
            int step = rangeMatch.Groups[3].Success ? int.Parse(rangeMatch.Groups[3].Value) : 1;

            for (int i = start; i <= end; i += step)
            {
                yield return i.ToString();
            }
            yield break;
        }

        // Handle comma-separated values (e.g., a,b,c)
        foreach (var option in options.Split(','))
        {
            yield return option.Trim();
        }
    }
}
