namespace MasterServerToolkit.Networking
{
    /// <summary>
    /// Hard limits for data received from an untrusted network peer.
    /// </summary>
    public static class MstNetworkLimits
    {
        public const int MaxMessagePayloadByteCount = 16 * 1024 * 1024;
        public const int MaxProtocolOverheadByteCount = 16;
        public const int MaxWireMessageByteCount =
            MaxMessagePayloadByteCount + MaxProtocolOverheadByteCount;
        public const int MaxFramePayloadByteCount = MaxWireMessageByteCount;
        public const int MaxQueuedIncomingMessageCount = 1024;
        public const int MaxQueuedIncomingMessageByteCount =
            2 * MaxWireMessageByteCount;
        public const int MaxTextPayloadByteCount = 1024 * 1024;
        public const int MaxDictionaryPayloadByteCount = 1024 * 1024;
        public const int MaxDictionaryEntryCount = 4096;
        public const int MaxCollectionEntryCount = 65536;
        public const int MaxProfilePropertyCount = 4096;
        public const int MaxAuthenticationPlaintextByteCount = 256 * 1024;
        public const int MaxAuthenticationCiphertextByteCount =
            MaxAuthenticationPlaintextByteCount + 4 * 1024;
        public const int MaxTokenCipherTextCharacterCount = 128 * 1024;
        public const int MaxAuthenticationTokenCharacterCount =
            MaxTokenCipherTextCharacterCount + 1 + 128;
        public const int MaxRsaParameterByteCount = 512;
        public const int MaxSecurityPublicKeyByteCount = 1024;
        public const int MaxSecurityWrappedKeyByteCount = 1024;
    }
}
