using FlaUI.Mcp.Core;
using FluentAssertions;
using Xunit;

namespace FlaUI.Mcp.Tests.Core;

public class GridCellFormatterTests
{
    [Fact]
    public void FormatLineRaw_WithName_IncludesRoleAndName()
    {
        GridCellFormatter.FormatLineRaw("w1e42", "text", "Hello")
            .Should().Be("[ref=w1e42] text \"Hello\"");
    }

    [Fact]
    public void FormatLineRaw_EmptyName_OmitsQuoteBlock()
    {
        GridCellFormatter.FormatLineRaw("w1e42", "text", "")
            .Should().Be("[ref=w1e42] text");
    }

    [Fact]
    public void FormatLineRaw_NameWithQuotes_EscapesCorrectly()
    {
        GridCellFormatter.FormatLineRaw("w1e42", "text", "say \"hi\"")
            .Should().Be("[ref=w1e42] text \"say \\\"hi\\\"\"");
    }
}
