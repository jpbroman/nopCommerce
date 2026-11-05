namespace Nop.Plugin.Misc.PunchOut.Tests;

/// <summary>
/// Shared test data for the PunchOut tests
/// </summary>
internal static class PunchOutTestData
{
    public const string Identity = "buyer-system";
    public const string SharedSecret = "s3cret";
    public const string BuyerCookie = "cookie-123";
    public const string CustomerEmail = "buyer@example.com";

    /// <summary>
    /// Builds a minimal cXML PunchOutSetupRequest
    /// </summary>
    /// <param name="operation">The value of the operation attribute; null leaves the attribute out</param>
    /// <param name="buyerCookie">The buyer cookie</param>
    /// <returns>The cXML document as a string</returns>
    public static string SetupRequestXml(string operation, string buyerCookie = BuyerCookie)
    {
        var operationAttribute = operation is null ? string.Empty : $" operation=\"{operation}\"";

        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <cXML payloadID="payload-1@example.com" timestamp="2026-11-05T10:00:00Z">
              <Header>
                <Sender>
                  <Credential domain="NetworkID">
                    <Identity>{Identity}</Identity>
                    <SharedSecret>{SharedSecret}</SharedSecret>
                  </Credential>
                </Sender>
              </Header>
              <Request>
                <PunchOutSetupRequest{operationAttribute}>
                  <BuyerCookie>{buyerCookie}</BuyerCookie>
                  <BrowserFormPost><URL>https://procurement.example.com/receive</URL></BrowserFormPost>
                  <Contact><Email>{CustomerEmail}</Email></Contact>
                </PunchOutSetupRequest>
              </Request>
            </cXML>
            """;
    }
}
