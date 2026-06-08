using QuartzDashboard.Internal;
using Xunit;

namespace QuartzDashboard.Tests;

public sealed class NameValidationTests
{
    [Theory]
    [InlineData("valid-name")]
    [InlineData("MyJob123")]
    [InlineData("job_with_underscores")]
    [InlineData("a")]
    [InlineData("A")]
    [InlineData("X.Y.Z")]
    [InlineData("job-name-v2")]
    [InlineData("test@email")]  // @ is allowed
    public void Validate_ValidNames_ReturnsNull(string name)
    {
        var result = NameValidation.Validate(name, "Job name");
        Assert.Null(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_NullOrWhitespace_ReturnsError(string? name)
    {
        var result = NameValidation.Validate(name, "Job name");
        Assert.NotNull(result);
        Assert.Contains("required", result);
    }

    [Theory]
    [InlineData("name<script>")]
    [InlineData("name'alert")]
    [InlineData("name\"injection")]
    [InlineData("name<evil>")]
    [InlineData("name>evil")]
    [InlineData("name\\path")]
    [InlineData("name`backtick")]
    public void Validate_NamesWithHtmlOrScriptMetacharacters_ReturnsError(string name)
    {
        var result = NameValidation.Validate(name, "Job name");
        Assert.NotNull(result);
        Assert.Contains("disallowed character", result);
    }

    [Theory]
    [InlineData((char)0x00)]
    [InlineData((char)0x01)]
    [InlineData((char)0x1F)]
    [InlineData((char)0x7F)]
    public void Validate_NamesWithControlCharacters_ReturnsError(char controlChar)
    {
        var name = $"job{controlChar}name";
        var result = NameValidation.Validate(name, "Job name");
        Assert.NotNull(result);
        Assert.Contains("disallowed control character", result);
    }

    [Fact]
    public void Validate_NameExceedsMaxLength_ReturnsError()
    {
        var longName = new string('x', 201);
        var result = NameValidation.Validate(longName, "Job name");
        Assert.NotNull(result);
        Assert.Contains("too long", result);
    }

    [Fact]
    public void Validate_NameAtMaxLength_ReturnsNull()
    {
        var maxName = new string('x', 200);
        var result = NameValidation.Validate(maxName, "Job name");
        Assert.Null(result);
    }

    [Fact]
    public void Validate_IncludesFieldLabelInError()
    {
        var result = NameValidation.Validate("name<evil>", "Trigger group");
        Assert.NotNull(result);
        Assert.Contains("Trigger group", result);
    }
}
