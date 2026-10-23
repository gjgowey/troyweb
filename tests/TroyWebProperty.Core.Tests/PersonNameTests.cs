using Xunit;

namespace TroyWebProperty.Core.Tests;

public class PersonNameTests
{

    [Theory]
    [InlineData("", "Smith", null, null, false)]
    [InlineData("Ada", "", null, null, false)]
    [InlineData("Ada", "Smith", null, null, true)]
    [InlineData("Ada", "Smith", "B", "Dr.", true)]
    [InlineData("Ada", "Smith", "BB", null, false)]
    [InlineData("Ada", "Smith", "1", null, false)]
    [InlineData("Ada", "Smith", null, "Invalid", false)]
    public void InformationValidatesNameComponents(string first, string last, string? middle, string? title, bool valid)
    {
        var info = new ApplicantInformation
        {
            FirstName = first, LastName = last, MiddleInitial = middle,
            Title = title, Phone = "202-555-0102", Email = "ada@example.test", CurrentAddress = "1 Main Street"
        };
        Assert.Equal(valid, info.Validate().Count == 0);
    }

}