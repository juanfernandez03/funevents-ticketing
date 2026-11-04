using FunEvents.Api.Common;

namespace FunEvents.Api.UseCases.Events;

public static class EventErrors
{
    public static Error NotFound(string eventCode) =>
        Error.NotFound("event_not_found", $"Event '{eventCode}' does not exist.");
}
