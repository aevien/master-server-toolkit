namespace MasterServerToolkit.MasterServer
{
    /// <summary>
    /// Result of atomically validating and consuming a persisted verification code.
    /// </summary>
    public enum VerificationCodeResult
    {
        Success,
        Invalid,
        Expired,
        AttemptsExceeded
    }
}
