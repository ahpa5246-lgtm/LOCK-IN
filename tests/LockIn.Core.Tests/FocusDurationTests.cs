using LockIn.Core.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LockIn.Core.Tests;

[TestClass]
public sealed class FocusDurationTests
{
    [DataTestMethod]
    [DataRow("3", 3)]
    [DataRow("25", 25)]
    [DataRow("720", 720)]
    public void ParseMinutes_AcceptsInclusiveSupportedRange(string input, int expected)
    {
        Assert.AreEqual(expected, FocusDuration.ParseMinutes(input));
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("two")]
    [DataRow("2")]
    [DataRow("721")]
    public void ParseMinutes_RejectsInvalidOrOutOfRangeInput(string input)
    {
        var exception = Assert.ThrowsException<InvalidOperationException>(
            () => FocusDuration.ParseMinutes(input));

        Assert.AreEqual(
            "Enter a duration between 3 and 720 minutes.",
            exception.Message);
    }
}
