namespace Hookivery.Domain.Events;

public enum EventStatus
{
    Pending = 0,
    Dispatched = 1,
    NoDestinations = 2,
}
