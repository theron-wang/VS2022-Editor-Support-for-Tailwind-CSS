using System.Collections.Generic;
using System.ComponentModel.Composition;

namespace TailwindCSSIntellisense.Configuration.Descriptions
{
    /// <summary>
    /// --tw-drop-shadow: drop-shadow(0 35px 35px rgba(0, 0, 0, 0.25)) drop-shadow(0 45px 65px rgba(0, 0, 0, 0.15));
    /// filter: var(--tw-blur) var(--tw-brightness) var(--tw-contrast) var(--tw-grayscale) var(--tw-hue-rotate) var(--tw-invert) var(--tw-saturate) var(--tw-sepia) var(--tw-drop-shadow)
    /// </summary>
    [Export(typeof(DescriptionGenerator))]
    internal class DropShadowDescriptionGenerator : DescriptionGenerator
    {
        public override string Handled { get; } = "dropShadow";

        public override string GetDescription(object value)
        {
            if (value is string)
            {
                return $"--tw-drop-shadow: drop-shadow({value});filter: var(--tw-blur) var(--tw-brightness) var(--tw-contrast) var(--tw-grayscale) var(--tw-hue-rotate) var(--tw-invert) var(--tw-saturate) var(--tw-sepia) var(--tw-drop-shadow)";
            }
            else if (value is IEnumerable<string> array)
            {
                var input = "";

                foreach (var v in array)
                {
                    input += $"drop-shadow({v}) ";
                }

                return $"--tw-drop-shadow: {input.Trim()};filter: var(--tw-blur) var(--tw-brightness) var(--tw-contrast) var(--tw-grayscale) var(--tw-hue-rotate) var(--tw-invert) var(--tw-saturate) var(--tw-sepia) var(--tw-drop-shadow)";
            }
            return null;
        }
    }
}
