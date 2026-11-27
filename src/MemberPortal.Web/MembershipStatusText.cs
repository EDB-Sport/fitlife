namespace MemberPortal.Web;

/// <summary>Medlemskabets tilstand in the glossary's Danish words. Membership sends the status by its name in code.</summary>
public static class MembershipStatusText
{
    /// <summary>The Danish word for a status, or the status as it comes when the portal doesn't know it.</summary>
    /// <param name="status">The status's name in code, e.g. <c>PaymentMissing</c>.</param>
    /// <returns>Aktiv, Pause, Betaling mangler, Opsagt, Afsluttet, or <paramref name="status"/>.</returns>
    public static string InDanish(string status)
    {
        return status switch
        {
            "Active" => "Aktiv",
            "Paused" => "Pause",
            "PaymentMissing" => "Betaling mangler",
            "Cancelled" => "Opsagt",
            "Ended" => "Afsluttet",
            _ => status,
        };
    }
}
