namespace Nop.Plugin.Misc.PunchOut.Domain;

/// <summary>
/// Represents the operation requested in a cXML PunchOutSetupRequest
/// (the value of the <c>operation</c> attribute)
/// </summary>
/// <remarks>
/// The default value is <see cref="Create"/>, so sessions that were saved before
/// this property existed are treated as new requisitions.
/// </remarks>
public enum PunchOutOperation
{
    /// <summary>
    /// The buyer starts a new requisition; the session starts with an empty shopping cart
    /// </summary>
    Create = 0,

    /// <summary>
    /// The buyer reopens an existing requisition that has not been submitted yet; the shopping cart is kept
    /// </summary>
    Edit = 1,

    /// <summary>
    /// The buyer views an existing requisition without changing it; the shopping cart is kept
    /// </summary>
    Inspect = 2
}
