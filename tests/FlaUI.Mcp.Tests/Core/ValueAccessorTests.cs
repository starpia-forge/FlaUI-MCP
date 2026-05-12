using FluentAssertions;
using FlaUI.Mcp.Core;
using Xunit;

namespace FlaUI.Mcp.Tests.Core;

public class ValueAccessorTests : IDisposable
{
    private readonly SessionManager _session;

    public ValueAccessorTests()
    {
        _session = new SessionManager();
    }

    public void Dispose() => _session.Dispose();

    [Fact]
    public void Read_DesktopRoot_ReturnsNull()
    {
        var desktop = _session.Automation.GetDesktop();
        var result = ValueAccessor.Read(desktop);
        result.Should().BeNull();
    }

    [Fact]
    public void SetNumber_PatternlessElement_ThrowsNotSupported()
    {
        var desktop = _session.Automation.GetDesktop();
        var act = () => ValueAccessor.SetNumber(desktop, "w0e0", 42.0);
        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void SetString_PatternlessElement_ThrowsNotSupported()
    {
        var desktop = _session.Automation.GetDesktop();
        var act = () => ValueAccessor.SetString(desktop, "w0e0", "hello");
        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void SetBool_PatternlessElement_ThrowsNotSupported()
    {
        var desktop = _session.Automation.GetDesktop();
        var act = () => ValueAccessor.SetBool(desktop, "w0e0", true);
        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Format_ValueResult_ContainsExpectedLines()
    {
        var result = new ValueReadResult("Value", "hello", ReadOnly: false);
        var text = ValueAccessor.Format(result);
        text.Should().Contain("pattern: Value");
        text.Should().Contain("value: hello");
        text.Should().Contain("readonly: false");
    }

    [Fact]
    public void Format_RangeValueResult_ContainsMinMax()
    {
        var result = new ValueReadResult("RangeValue", "50", Min: 0, Max: 100);
        var text = ValueAccessor.Format(result);
        text.Should().Contain("pattern: RangeValue");
        text.Should().Contain("value: 50");
        text.Should().Contain("min: 0");
        text.Should().Contain("max: 100");
    }

    [Fact]
    public void Format_ToggleResult_NoReadonlyNoMinMax()
    {
        var result = new ValueReadResult("Toggle", "On");
        var text = ValueAccessor.Format(result);
        text.Should().Contain("pattern: Toggle");
        text.Should().Contain("value: On");
        text.Should().NotContain("readonly");
        text.Should().NotContain("min");
    }
}
