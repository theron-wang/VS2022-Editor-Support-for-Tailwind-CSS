using System;
using System.IO;

namespace TailwindCSSIntellisense.Helpers;
public static class PathHelpers
{
    // https://stackoverflow.com/questions/703281/getting-path-relative-to-the-current-working-directory
    /// <summary>
    /// Gets the relative path of a file with respect to a specified folder.
    /// </summary>
    /// <param name="file">The full path of the file.</param>
    /// <param name="folder">The full path of the folder.</param>
    /// <returns>
    /// A string representing the relative path of the file with respect to the folder.
    /// If the file parameter is null, the method returns null.
    /// </returns>
    public static string GetRelativePath(string file, string folder)
    {
        if (file is null)
        {
            return null;
        }

        var pathUri = new Uri(file);
        // Folders must end in a slash
        if (!folder.EndsWith(Path.DirectorySeparatorChar.ToString()))
        {
            folder += Path.DirectorySeparatorChar;
        }
        var folderUri = new Uri(folder);
        return Uri.UnescapeDataString(folderUri.MakeRelativeUri(pathUri).ToString().Replace('/', Path.DirectorySeparatorChar));
    }

    /// <summary>
    /// Gets the absolute path by combining a base directory with a relative path.
    /// </summary>
    /// <param name="dir">The base directory path.</param>
    /// <param name="rel">The relative path to be combined with the base directory.</param>
    /// <returns>
    /// A string representing the absolute path obtained by combining the base directory
    /// and the relative path. If the relative path parameter is null, the method returns null.
    /// </returns>
    public static string GetAbsolutePath(string dir, string rel)
    {
        if (rel is null)
        {
            return null;
        }
        // Folders must end in a slash
        if (!dir.EndsWith(Path.DirectorySeparatorChar.ToString()))
        {
            dir += Path.DirectorySeparatorChar;
        }

        var dirUri = new Uri(dir);
        var absUri = new Uri(dirUri, rel);

        return Uri.UnescapeDataString(absUri.AbsolutePath.Replace('/', Path.DirectorySeparatorChar));
    }
}
