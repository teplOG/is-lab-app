using Xunit;

namespace IsLabApp.Tests;

public class NoteRulesTests
{
    [Fact]
    public void ValidTitleIsAccepted()
    {
        Assert.Null(NoteRules.Validate("First note"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void EmptyTitleIsRejected(string? title)
    {
        Assert.Equal("Title is required", NoteRules.Validate(title));
    }

    [Fact]
    public void TooLongTitleIsRejected()
    {
        var title = new string('x', NoteRules.MaxTitleLength + 1);

        Assert.Equal("Title must be 200 characters or less", NoteRules.Validate(title));
    }
}
