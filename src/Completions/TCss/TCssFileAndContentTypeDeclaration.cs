using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

namespace TailwindCSSIntellisense.Completions.TCss
{
    internal static class TCssFileAndContentTypeDeclaration
    {
        [Export]
        [Name("tcss")]
        [BaseDefinition("css")]
        internal static ContentTypeDefinition TCssContentTypeDefinition;

        [Export]
        [FileExtension(".tcss")]
        [ContentType("tcss")]
        internal static FileExtensionToContentTypeDefinition TCssFileExtensionDefinition;
    }
}
