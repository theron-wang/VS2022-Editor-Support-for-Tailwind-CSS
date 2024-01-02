using Microsoft.VisualStudio.Text;

namespace TailwindCSSIntellisense.Linting;
internal class Error(SnapshotSpan span, string errorMessage, ErrorType errorType)
{
    public SnapshotSpan Span { get; set; } = span;
    public string ErrorMessage { get; set; } = errorMessage;
    public ErrorType ErrorType { get; set; } = errorType;
}
