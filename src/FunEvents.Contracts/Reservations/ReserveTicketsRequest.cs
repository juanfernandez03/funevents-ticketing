using System.ComponentModel.DataAnnotations;

namespace FunEvents.Contracts.Reservations;

/// <summary>Body of <c>POST /events/{eventCode}/reservations</c>.</summary>
/// <param name="UserCode">Public code of the user who receives the tickets, e.g. <c>USR-001</c>.</param>
/// <param name="Quantity">Number of tickets, between 1 and <see cref="ReservationLimits.MaxTicketsPerReservation"/>.</param>
public sealed record ReserveTicketsRequest(
    [Required, StringLength(ReservationLimits.UserCodeMaxLength)] string UserCode,
    [Range(1, ReservationLimits.MaxTicketsPerReservation)] int Quantity);
