namespace CraftorAI.Email.Contracts;

public enum EmailSendResult
{
    Success,
    InvalidEmail,
    NetworkError,
    AuthenticationError,
    RateLimited,
    ServiceDisabled,
    UnknownError
}
