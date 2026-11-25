namespace Hookivery.Domain.Deliveries;

public enum AttemptOutcome
{
    Succeeded = 0,
    HttpError = 1,
    Timeout = 2,
    NetworkError = 3,
}
