using MasterServerToolkit.MasterServer;
using UnityEditor;

namespace MasterServerToolkit.Editor
{
    public class MirrorRoomsAndLobbiesBuilder
    {
        private const string ROOM_SCENE = "Assets/MasterServerToolkit/Bridges/Mirror/RoomsAndLobbies/Scenes/Room.unity";
        private const string MASTER_SCENE = "Assets/MasterServerToolkit/Bridges/Mirror/RoomsAndLobbies/Scenes/Master.unity";
        private const string SPAWNER_SCENE = "Assets/MasterServerToolkit/Bridges/Mirror/RoomsAndLobbies/Scenes/Spawner.unity";
        private const string CLIENT_SCENE = "Assets/MasterServerToolkit/Bridges/Mirror/RoomsAndLobbies/Scenes/Client.unity";

        private const string BUILD_FOLDER = "Builds/Mirror/RoomsAndLobbies";

        [MenuItem(MstConstants.ToolMenu + "Build Bridges/Mirror/RoomsAndLobbies/Room(Headless)")]
        private static void BuildRoomForWindowsHeadless()
        {
            BuildRoom(true);
        }

        [MenuItem(MstConstants.ToolMenu + "Build Bridges/Mirror/RoomsAndLobbies/Room(Normal)")]
        private static void BuildRoomForWindowsNormal()
        {
            BuildRoom(false);
        }

        private static void BuildRoom(bool headless) {
            // Создаем конфигурацию для Room сборки
            var roomConfig = ProjectBuilder.CreateConfig(
                Mst.Args.Names.StartClientConnection, true,
                Mst.Args.Names.MasterIp, Mst.Args.MasterIp,
                Mst.Args.Names.MasterPort, Mst.Args.MasterPort,
                Mst.Args.Names.RoomIp, Mst.Args.RoomIp,
                Mst.Args.Names.RoomPort, Mst.Args.RoomPort
            );

            // Вызываем универсальный билдер - вся сложная логика спрятана внутри
            ProjectBuilder.Build(
                "Room",                    // Имя сборки
                new[] { ROOM_SCENE },      // Массив сцен
                BUILD_FOLDER,              // Папка для сборки
                headless,                      // isServer = true (headless)
                roomConfig                 // Конфигурационные свойства
            );
        }
    } 
}
