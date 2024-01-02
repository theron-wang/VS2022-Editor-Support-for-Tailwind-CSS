using Community.VisualStudio.Toolkit;
using TailwindCSSIntellisense.Linting.Validators;

namespace TailwindCSSIntellisense.Linting.ErrorList;
internal class ErrorListContext
{
    public Validator Validator { get; set; }
    public TableDataSource TableDataSource { get; set; }
    public string File { get; set; }
    public Project Project { get; set; }
}
