using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TailwindCSSIntellisense.Completions;

namespace Community.VisualStudio.Toolkit
{
    public static class VS
    {
        public static MessageBoxProxy MessageBox { get; } = new();

        public sealed class MessageBoxProxy
        {
            public void ShowError(string message)
            {
            }
        }
    }

    public static class ExceptionLoggingExtensions
    {
        public static void Log(this Exception ex, string message)
        {
        }
    }

    public static class StringExtensions
    {
        public static string TrimPrefix(this string value, string prefix, StringComparison comparison)
        {
            return value.StartsWith(prefix, comparison) ? value[prefix.Length..] : value;
        }
    }
}

namespace Microsoft.VisualStudio.Shell
{
    public static class ThreadHelper
    {
        public static JoinableTaskFactory JoinableTaskFactory { get; } = new();
    }

    public sealed class JoinableTaskFactory
    {
        public T Run<T>(Func<Task<T>> asyncMethod)
        {
            return asyncMethod().GetAwaiter().GetResult();
        }
    }
}

namespace TailwindCSSIntellisense.Settings
{
    public class TailwindSettings
    {
        public bool UseCli { get; set; }
        public string? TailwindCliPath { get; set; }
        public CustomRegexes? CustomRegexes { get; set; } = new();
    }

    public class CustomRegexes
    {
        public CustomRegex Razor { get; set; } = new();
        public CustomRegex HTML { get; set; } = new();
        public CustomRegex JavaScript { get; set; } = new();

        public class CustomRegex
        {
            public bool Override { get; set; }
            public List<string> Values { get; set; } = [];
        }
    }
}

namespace TailwindCSSIntellisense.Completions
{
    public sealed class ProjectConfigurationManager(ProjectCompletionValues values)
    {
        private readonly ProjectCompletionValues _values = values;

        public ProjectCompletionValues GetCompletionConfigurationByFilePath(string? filePath)
        {
            return _values;
        }
    }
}

namespace TailwindCSSIntellisense.ClassSort
{
    public sealed class ClassSortUtilities(Dictionary<string, int> classOrder, Dictionary<string, int> variantOrder)
    {
        public Dictionary<string, int> GetClassOrder(ProjectCompletionValues project)
        {
            return classOrder;
        }

        public Dictionary<string, int> GetVariantOrder(ProjectCompletionValues project)
        {
            return variantOrder;
        }
    }
}

namespace TailwindCSSIntellisense.ClassSort.Sorters
{
    internal static class SorterStringExtensions
    {
        public static string TrimPrefix(this string value, string prefix, StringComparison comparison)
        {
            return value.StartsWith(prefix, comparison) ? value[prefix.Length..] : value;
        }
    }

    internal sealed class CssSorter : Sorter
    {
        public override string[] Handled { get; } = [".css"];

        protected override IEnumerable<string> GetSegments(string filePath, string input)
        {
            yield return input;
        }
    }
}
