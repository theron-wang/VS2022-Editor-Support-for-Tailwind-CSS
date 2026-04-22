using TailwindCSSIntellisense.ClassSort;
using TailwindCSSIntellisense.ClassSort.Sorters;
using TailwindCSSIntellisense.Completions;

namespace TailwindExtension.Tests.IntegrationTests;

public class SortingIntegrationTests
{
    [Fact]
    public void SortSegment_OrdersClassesByVariantAndClassOrder()
    {
        var sorter = CreateSorter();

        var sorted = sorter.SortSegmentPublic("hover:font-bold text-red-500 p-4", "index.html");

        Assert.Equal("p-4 text-red-500 hover:font-bold", sorted);
    }

    [Fact]
    public void Sort_UpdatesOnlyClassSegmentAndPreservesMarkup()
    {
        var sorter = CreateSorter();
        const string input = "<div class=\"text-red-500 p-4\">hello</div>";

        var sorted = sorter.SortContent("index.html", input);

        Assert.Equal("<div class=\"p-4 text-red-500\">hello</div>", sorted);
    }

    [Fact]
    public void GetNextIndexOfClass_PicksNearestQuoteVariant()
    {
        var sorter = CreateSorter();
        const string content = "<div class='a b' data-x=\"y\" class=\"c d\"></div>";

        var first = sorter.GetNextIndex(content, 0);
        var second = sorter.GetNextIndex(content, first.index + 1);

        Assert.Equal('\'', first.terminator);
        Assert.Equal('"', second.terminator);
        Assert.True(second.index > first.index);
    }

    private static TestSorter CreateSorter()
    {
        var values = new ProjectCompletionValues
        {
            Version = TailwindVersion.V3,
            ColorMapper =
            {
                ["red-500"] = "#f00"
            },
            SpacingMapper =
            {
                ["4"] = "1rem"
            },
            Breakpoints =
            {
                ["sm"] = "640px"
            }
        };

        var projectManager = new ProjectConfigurationManager(values);
        var classSortUtilities = new ClassSortUtilities(
            classOrder: new Dictionary<string, int>
            {
                ["p-{s}"] = 1,
                ["text-{c}"] = 2,
                ["font-bold"] = 3
            },
            variantOrder: new Dictionary<string, int>
            {
                ["hover"] = 10,
                ["sm"] = 20
            });

        return new TestSorter
        {
            ProjectConfigurationManager = projectManager,
            ClassSortUtilities = classSortUtilities
        };
    }

    private sealed class TestSorter : Sorter
    {
        public override string[] Handled => [".html"];

        public string SortSegmentPublic(string classText, string filePath)
        {
            return SortSegment(classText, filePath);
        }

        public string SortContent(string filePath, string input)
        {
            return Sort(filePath, input);
        }

        public (int index, char terminator) GetNextIndex(string input, int startIndex)
        {
            return GetNextIndexOfClass(input, startIndex);
        }

        protected override IEnumerable<string> GetSegments(string filePath, string input)
        {
            const string marker = "class=\"";
            var classStart = input.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (classStart < 0)
            {
                yield return input;
                yield break;
            }

            var classValueStart = classStart + marker.Length;
            var classValueEnd = input.IndexOf('"', classValueStart);
            if (classValueEnd < 0)
            {
                yield return input;
                yield break;
            }

            yield return input[..classValueStart];
            yield return SortSegment(input[classValueStart..classValueEnd], filePath);
            yield return input[classValueEnd..];
        }
    }
}
