using FlaUI.Mcp.Core;
using FluentAssertions;
using Xunit;

namespace FlaUI.Mcp.Tests.Core;

/// <summary>
/// Tests ConditionEvaluator with a null AutomationElement — no real UIA connection needed.
/// </summary>
public class ConditionEvaluatorTests
{
    [Theory]
    [InlineData("exists",       false)]
    [InlineData("missing",      true)]
    [InlineData("visible",      false)]
    [InlineData("hidden",       true)]
    [InlineData("enabled",      false)]
    [InlineData("disabled",     false)]
    [InlineData("textEquals",   false)]
    [InlineData("textContains", false)]
    [InlineData("checked",      false)]
    [InlineData("unchecked",    false)]
    public void Evaluate_NullElement_ReturnsExpected(string condition, bool expected)
    {
        var (met, observed) = ConditionEvaluator.Evaluate(null, condition, null);

        met.Should().Be(expected, $"null element + condition '{condition}' should return {expected}");
        observed.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Evaluate_UnknownCondition_ReturnsFalseWithMessage()
    {
        var (met, observed) = ConditionEvaluator.Evaluate(null, "nonExistent", null);

        met.Should().BeFalse();
        observed.Should().Contain("unknown condition");
    }

    [Fact]
    public void ValidConditions_ContainsAllExpected()
    {
        var expected = new[]
        {
            "visible", "hidden", "enabled", "disabled",
            "exists", "missing",
            "textEquals", "textContains",
            "checked", "unchecked"
        };

        ConditionEvaluator.ValidConditions.Should().BeEquivalentTo(expected);
    }

    [Theory]
    [InlineData("button",      FlaUI.Core.Definitions.ControlType.Button)]
    [InlineData("textbox",     FlaUI.Core.Definitions.ControlType.Edit)]
    [InlineData("checkbox",    FlaUI.Core.Definitions.ControlType.CheckBox)]
    [InlineData("listitem",    FlaUI.Core.Definitions.ControlType.ListItem)]
    [InlineData("BUTTON",      FlaUI.Core.Definitions.ControlType.Button)]
    [InlineData("unknown_xyz", FlaUI.Core.Definitions.ControlType.Custom)]
    public void RoleToControlType_MapsCorrectly(string role, FlaUI.Core.Definitions.ControlType expected)
    {
        ConditionEvaluator.RoleToControlType(role).Should().Be(expected);
    }
}
