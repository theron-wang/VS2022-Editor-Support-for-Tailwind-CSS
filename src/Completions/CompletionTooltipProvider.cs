using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using System.Linq;
using System.Windows;

namespace TailwindCSSIntellisense.Completions;

[Export(typeof(IUIElementProvider<Completion, ICompletionSession>))]
[Name(nameof(CompletionTooltipCustomizationProvider))]
[Order(Before = "RoslynToolTipProvider")]
[ContentType("css")]
[ContentType("tcss")]
[ContentType("html")]
[ContentType("WebForms")]
[ContentType("razor")]
[ContentType("LegacyRazorCSharp")]
[ContentType("LegacyRazor")]
[ContentType("LegacyRazorCoreCSharp")]
internal class CompletionTooltipCustomizationProvider : IUIElementProvider<Completion, ICompletionSession>
{
    public UIElement GetUIElement(Completion itemToRender, ICompletionSession context, UIElementType elementType)
    {
        if (elementType == UIElementType.Tooltip && itemToRender.Properties.ContainsProperty("tailwind") && !itemToRender.Properties.ContainsProperty("modifier"))
        {
            var fullText = itemToRender.DisplayText;
            
            if (fullText.EndsWith("[]"))
            {
                return null;
            }

            var classText = fullText.Split(':').Last();

            var isImportant = ImportantModifierHelper.IsImportantModifier(classText);

            var desc = itemToRender.Description;

            return DescriptionUIHelper.GetDescriptionAsWPFFormatted(fullText, desc, isImportant);
        }
        else
        {
            return null;
        }
    }
}