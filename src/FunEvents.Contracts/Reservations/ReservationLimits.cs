namespace FunEvents.Contracts.Reservations;

public static class ReservationLimits
{
    /// <summary>Per-request cap. Keeps one buyer (or bot) from emptying an event in a single call.</summary>
    public const int MaxTicketsPerReservation = 10;

    /// <summary>Maximum length of a public user code such as <c>USR-001</c>.</summary>
    public const int UserCodeMaxLength = 32;
}
