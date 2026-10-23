using TroyWebProperty.Core.Entities;
using Xunit;

namespace TroyWebProperty.Core.Tests;

public sealed class ApplicationRulesTests
{

    [Theory]
    [InlineData("Draft", true)]
    [InlineData("Returned", true)]
    [InlineData("Submitted", false)]
    [InlineData("Approved", false)]
    [InlineData("Denied", false)]
    [InlineData("Withdrawn", false)]
    public void EditingIsLimitedToDraftAndReturned(string status, bool expected)
    {
        Assert.Equal(expected, ApplicationRules.CanEdit(status));
    }

    [Theory]
    [InlineData("Draft", true)]
    [InlineData("Returned", true)]
    [InlineData("Submitted", true)]
    [InlineData("Approved", false)]
    [InlineData("Denied", false)]
    [InlineData("Withdrawn", false)]
    public void TerminalApplicationsCannotBeWithdrawn(string status, bool expected)
    {
        Assert.Equal(expected, ApplicationRules.CanWithdraw(status));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public void SubmitRequiresBothSavedSections(bool info, bool history)
    {
        Assert.Throws<RuleException>(() =>
            ApplicationRules.Submit(
                new RentalApplication
                {
                    Status = new ApplicationStatus { Code = "Draft" }, InformationSaved = info, HistorySaved = history
                },
                false));
    }

    [Fact]
    public void SubmitRejectsOccupiedUnit()
    {
        Assert.Throws<RuleException>(static () =>
            ApplicationRules.Submit(
                new RentalApplication
                {
                    Status = new ApplicationStatus { Code = "Returned" }, InformationSaved = true, HistorySaved = true
                },
                true));
    }

    [Theory]
    [InlineData("Approved")]
    [InlineData("Returned")]
    [InlineData("Denied")]
    public void ReviewRequiresSubmittedStatus(string outcome)
    {
        Assert.Throws<RuleException>(() => ApplicationRules.Review("Draft", outcome, "Comment", false));
    }

    [Theory]
    [InlineData("Returned")]
    [InlineData("Denied")]
    public void ReturnAndDenyRequireComments(string outcome)
    {
        Assert.Throws<RuleException>(() => ApplicationRules.Review("Submitted", outcome, "  ", false));
    }

    [Fact]
    public void ApprovalRejectsOccupiedUnit()
    {
        Assert.Throws<RuleException>(static () => ApplicationRules.Review("Submitted", "Approved", null, true));
    }

    [Fact]
    public void ReturnCanProceedWhenAnotherApplicationHasLeasedTheUnit()
    {
        ApplicationRules.Review("Submitted", "Returned", "Correct the address", true);
    }

    [Fact]
    public void InactiveTypeCannotBeAssignedToAnotherUnit()
    {
        Assert.Throws<RuleException>(static () => ApplicationRules.UnitType(false, 3, 1));
    }

    [Fact]
    public void ExistingInactiveTypeCanBeRetained()
    {
        ApplicationRules.UnitType(false, 3, 3);
    }

    [Fact]
    public void NewUnitCannotUseInactiveType()
    {
        Assert.Throws<RuleException>(static () => ApplicationRules.UnitType(false, 3, null));
    }

    [Fact]
    public void LeaseEndIsExclusive()
    {
        var start = new DateOnly(2026, 1, 1);
        var end = start.AddMonths(12);
        Assert.True(ApplicationRules.CoversToday(start, end, start));
        Assert.True(ApplicationRules.CoversToday(start, end, end.AddDays(-1)));
        Assert.False(ApplicationRules.CoversToday(start, end, end));
        Assert.False(ApplicationRules.CoversToday(start, end, start.AddDays(-1)));
    }

    [Fact]
    public void ResidenceDatesRejectFutureAndInvertedDates()
    {
        var today = new DateOnly(2026, 10, 20);
        Assert.Throws<RuleException>(() => ApplicationRules.ResidenceDates(today, today.AddDays(1), today));
        Assert.Throws<RuleException>(() => ApplicationRules.ResidenceDates(today, today.AddDays(-1), today));
    }

    [Fact]
    public void HistoryRequiresAtLeastOneResidence()
    {
        Assert.Throws<RuleException>(static () => ApplicationRules.History([], new DateOnly(2026, 10, 20)));
    }

    [Fact]
    public void ApplicantInformationReportsFieldErrors()
    {
        var errors = new ApplicantInformation { Email = "invalid" }.Validate();
        Assert.Contains(errors, static e => e.MemberNames.Contains(nameof(ApplicantInformation.Email)));
        Assert.Contains(errors, static e => e.MemberNames.Contains(nameof(ApplicantInformation.CurrentAddress)));
    }

}