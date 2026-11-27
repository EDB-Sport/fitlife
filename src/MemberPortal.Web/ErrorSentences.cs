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
}
