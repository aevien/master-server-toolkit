using MasterServerToolkit.Extensions;

namespace MasterServerToolkit.GameService
{
    public struct GameServiceOpCodes
    {
        public static ushort PlayWeb3GetUserWalletByKey = nameof(PlayWeb3GetUserWalletByKey).ToUint16Hash();
        public static ushort PlayWeb3GetArtifacts = nameof(PlayWeb3GetArtifacts).ToUint16Hash();
        public static ushort PlayWeb3GetArtifactPurchases = nameof(PlayWeb3GetArtifactPurchases).ToUint16Hash();
        public static ushort PlayWeb3RegisterArtifactPurchasedWebhook = nameof(PlayWeb3RegisterArtifactPurchasedWebhook).ToUint16Hash();
    }
}