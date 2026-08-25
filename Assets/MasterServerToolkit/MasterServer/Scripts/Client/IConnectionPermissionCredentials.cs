namespace MasterServerToolkit.MasterServer
{
    /// <summary>
    /// Exposes a connection-scoped permission credential configured by its connection owner.
    /// </summary>
    public interface IConnectionPermissionCredentials
    {
        /// <summary>
        /// Exact permission key associated with <see cref="PermissionCredential"/>.
        /// </summary>
        string PermissionKey { get; set; }

        /// <summary>
        /// Credential used when runtime MST configuration does not define <see cref="PermissionKey"/>.
        /// </summary>
        string PermissionCredential { get; set; }
    }
}
