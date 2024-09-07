using System.Linq;

namespace TailwindCSSIntellisense.Helpers;
internal static class ColorHelpers
{
    public static bool IsHex(object value, out string hex)
    {
        var content = value.ToString().Trim('#').ToUpper();
        var hexLetters = "ABCDEF";
        if (content.All(c => char.IsNumber(c) || hexLetters.Contains(c)))
        {
            if (content.Length == 6 || content.Length == 8)
            {
                hex = content.Substring(0, 6);
                return true;
            }
            else if (content.Length == 3)
            {
                hex = content;
                return true;
            }
        }

        hex = null;
        return false;
    }
}
