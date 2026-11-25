namespace BookingService.Models;

/// <summary>A kind of class that recurs in the schedule, e.g. spinning or yoga (GLOSSARY: Holdtype). Reference data with no rules: Booking only reads it (#254).</summary>
/// <param name="Id">The class type's id, as <see cref="Domain.ClassSessionDetails.ClassTypeId"/> holds it.</param>
/// <param name="Name">The name shown in the class schedule.</param>
public sealed record ClassType(Guid Id, string Name);
