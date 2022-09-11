using MasterServerToolkit.Extensions;

namespace MasterServerToolkit.Examples.BasicProfile
{
    public struct MessageOpCodes
    {
        public static ushort BuyDemoItem = nameof(BuyDemoItem).ToUint16Hash();
        public static ushort SellDemoItem = nameof(SellDemoItem).ToUint16Hash();
    }
}