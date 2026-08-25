namespace MasterServerToolkit.Networking
{
    public enum ResponseStatus : byte
    {
        // Success responses
        Success,  // Operation completed successfully

        // Timeout / Connectivity issues
        Timeout,  // Request timeout
        NotConnected,  // Client not connected / connection closed

        // General server errors
        Error,  // Internal server error
        DependencyError,  // Error from dependency service (Bad Gateway)
        ServiceUnavailable,  // Service temporarily unavailable
        Unhandled,  // Unknown / unhandled error

        // Authorization and access control
        Unauthorized,  // Unauthorized (no valid authentication)
        Forbidden,  // Forbidden (no permission to access resource)
        TokenExpired,  // Authentication token expired
        Banned,  // Banned or blocked by policy
        DuplicateLogin,  // Duplicate login detected

        // Client request errors
        BadRequest,  // Bad request (malformed syntax)
        Invalid,  // Unprocessable entity (invalid data)
        NotFound,  // Resource not found
        AlreadyExists,  // Resource already exists
        Conflict,  // Conflict with current state of resource

        // Cancelled operations
        Cancelled  // Request cancelled by client
    }
}
