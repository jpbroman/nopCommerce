using AwesomeAssertions;
using Nop.Core;
using Nop.Plugin.Misc.PunchOut.Domain;
using Nop.Plugin.Misc.PunchOut.Services;
using NUnit.Framework;

namespace Nop.Plugin.Misc.PunchOut.Tests;

/// <summary>
/// Tests that the operation of a PunchOutSetupRequest is parsed correctly (issue #8435)
/// </summary>
[TestFixture]
public class PunchOutXmlBuilderTests
{
    [TestCase("create", PunchOutOperation.Create)]
    [TestCase("edit", PunchOutOperation.Edit)]
    [TestCase("inspect", PunchOutOperation.Inspect)]
    [TestCase("Edit", PunchOutOperation.Edit)]
    [TestCase(" INSPECT ", PunchOutOperation.Inspect)]
    public void ParseOperation_KnownValue_ReturnsOperation(string value, PunchOutOperation expected)
    {
        PunchOutXmlBuilder.ParseOperation(value).Should().Be(expected);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void ParseOperation_MissingValue_DefaultsToCreate(string value)
    {
        PunchOutXmlBuilder.ParseOperation(value).Should().Be(PunchOutOperation.Create);
    }

    [Test]
    public void ParseOperation_UnknownValue_Throws()
    {
        var act = () => PunchOutXmlBuilder.ParseOperation("delete");

        act.Should().Throw<NopException>().WithMessage("*delete*");
    }

    [TestCase("create", PunchOutOperation.Create)]
    [TestCase("edit", PunchOutOperation.Edit)]
    [TestCase("inspect", PunchOutOperation.Inspect)]
    public void ParseSetupRequest_ReadsOperationAttribute(string operation, PunchOutOperation expected)
    {
        var request = PunchOutXmlBuilder.ParseSetupRequest(PunchOutTestData.SetupRequestXml(operation));

        request.Operation.Should().Be(expected);
        request.BuyerCookie.Should().Be(PunchOutTestData.BuyerCookie);
        request.Contact.Should().Be(PunchOutTestData.CustomerEmail);
    }

    [Test]
    public void ParseSetupRequest_WithoutOperationAttribute_DefaultsToCreate()
    {
        var request = PunchOutXmlBuilder.ParseSetupRequest(PunchOutTestData.SetupRequestXml(operation: null));

        request.Operation.Should().Be(PunchOutOperation.Create);
    }
}
