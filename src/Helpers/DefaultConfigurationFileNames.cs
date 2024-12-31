using System.Linq;

namespace TailwindCSSIntellisense.Helpers;
public static class DefaultConfigurationFileNames
{
    public static readonly string[] Extensions = [".js", ".cjs", ".mjs", ".ts", ".cts", ".mts"];

    public static readonly string[] Names = Extensions.Select(e => $"tailwind.config{e}").ToArray();
}
