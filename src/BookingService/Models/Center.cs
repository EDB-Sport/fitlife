namespace BookingService.Models;

/// <summary>One of FitLife's six centres (GLOSSARY: Center). Reference data with no rules: Booking only reads it (#254).</summary>
/// <param name="Id">The centre's id, as <see cref="Domain.ClassSessionDetails.CenterId"/> holds it.</param>
/// <param name="Name">The name shown in the class schedule.</param>
public sealed record Center(Guid Id, string Name);
