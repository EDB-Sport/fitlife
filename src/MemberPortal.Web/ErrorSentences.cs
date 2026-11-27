namespace MemberPortal.Web;

/// <summary>
/// The Danish sentences the portal shows when a service says no. A page shows a sentence, never the service's own text,
/// status code or <c>code</c>. #207 to #210 add their rows.
/// </summary>
public static class ErrorSentences
{
    /// <summary>Shown for any answer the page has no sentence for, and for no answer at all.</summary>
    public const string Generic = "Det lykkedes ikke. Prøv igen om lidt.";

    /// <summary>Identity's <c>EmailAlreadyTaken</c>. The page adds a link to the log-in page.</summary>
    public const string EmailAlreadyTaken = "Der findes allerede en konto med den e-mail. Log ind i stedet.";

    /// <summary>Identity's 400 on a new account.</summary>
    public const string InvalidNewAccount = "Skriv en gyldig e-mail og en adgangskode på mindst 8 tegn.";

    /// <summary>The account is made and the person logged in, but joining failed.</summary>
    public const string AccountMadeButNotJoined = "Din konto er oprettet, men vi kunne ikke melde dig ind. Prøv igen.";

    /// <summary>Membership's 409 on joining: the account is already a member.</summary>
    public const string AlreadyAMember = "Du er allerede medlem.";

    /// <summary>Membership's 201 with <c>PaymentMissing</c>: the person is a member, but the first fee isn't paid (#546).</summary>
    public const string JoinedButPaymentMissing = "Du er meldt ind, men betalingen mangler. Du kan ikke booke, før den er betalt.";

    /// <summary>Membership's <c>ChargeDeclined</c>: the payment provider did not accept the charge (#535).</summary>
    public const string ChargeDeclined = "Betalingen blev afvist. Prøv igen senere.";

    /// <summary>Membership's <c>NothingIsOwed</c>: the membership owes no fee (#535).</summary>
    public const string NothingIsOwed = "Du skylder ikke noget.";

    /// <summary>Membership's <c>MembershipNotActive</c>: only an active membership can be paused (#211).</summary>
    public const string MembershipNotActive = "Dit medlemskab er ikke aktivt.";

    /// <summary>Membership's <c>PauseStartsInThePast</c> (#211).</summary>
    public const string PauseStartsInThePast = "Pausen kan ikke starte før i dag.";

    /// <summary>Membership's <c>PauseEndsBeforeItStarts</c> (#211).</summary>
    public const string PauseEndsBeforeItStarts = "Pausen skal slutte efter den dag, den starter.";

    /// <summary>Membership's <c>NoPauseToEnd</c> (#211).</summary>
    public const string NoPauseToEnd = "Du har ingen pause at afslutte.";

    /// <summary>The sentence for a service's problem-details <c>code</c>, never its text.</summary>
    /// <param name="code">The <c>code</c>, or <see langword="null"/> when the answer had none.</param>
    /// <returns>The code's sentence, or <see cref="Generic"/> for any code the portal has no sentence for.</returns>
    public static string ForCode(string? code)
    {
        return code switch
        {
            "ChargeDeclined" => ChargeDeclined,
            "NothingIsOwed" => NothingIsOwed,
            "MembershipNotActive" => MembershipNotActive,
            "PauseStartsInThePast" => PauseStartsInThePast,
            "PauseEndsBeforeItStarts" => PauseEndsBeforeItStarts,
            "NoPauseToEnd" => NoPauseToEnd,
            _ => Generic,
        };
    }
}
