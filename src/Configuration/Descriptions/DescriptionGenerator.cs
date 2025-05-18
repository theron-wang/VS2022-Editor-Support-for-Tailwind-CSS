namespace TailwindCSSIntellisense.Configuration.Descriptions;

internal abstract class DescriptionGenerator
{
    public abstract string Handled { get; }

    public abstract string? GetDescription(object value);
}
