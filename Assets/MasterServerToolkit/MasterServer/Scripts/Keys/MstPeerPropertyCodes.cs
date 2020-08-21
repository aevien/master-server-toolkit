namespace MasterServerToolkit.MasterServer
{
    public enum MstPeerPropertyCodes : short
    {
        Start = 26000,

        // Rooms
        RegisteredRooms,

        // Spawners
        RegisteredSpawners,
        ClientSpawnRequest
    }
}