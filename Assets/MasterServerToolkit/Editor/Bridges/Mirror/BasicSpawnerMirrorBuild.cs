#if MIRROR
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MasterServerToolkit.MasterServer.Examples.BasicSpawnerMirror
{
    public class BasicSpawnerMirrorBuild
    {
        [MenuItem("Master Server Toolkit/Build/Demos/Basic Spawner Mirror/All")]
        private static void BuildBoth()
        {
            BuildMasterAndSpawnerForWindows();
            BuildRoomForWindowsHeadless();
            BuildClientForWindows();
        }

        [MenuItem("Master Server Toolkit/Build/Demos/Basic Spawner Mirror/Master Server and Spawner")]
        private static void BuildMasterAndSpawnerForWindows()
        {
            string buildFolder = Path.Combine("Builds", "BasicSpawnerMirror", "MasterAndSpawner");
            string roomExePath = Path.Combine(Directory.GetCurrentDirectory(), "Builds", "BasicSpawnerMirror", "Room", "Room.exe");

            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/MasterServerToolkit/Bridges/Mirror/BasicSpawnerMirror/Scenes/MasterAndSpawner/MasterAndSpawner.unity" },
                locationPathName = Path.Combine(buildFolder, "MasterAndSpawner.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.EnableHeadlessMode | BuildOptions.ShowBuiltPlayer | BuildOptions.Development
            };

            BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                MstProperties properties = new MstProperties();
                properties.Add(Mst.Args.Names.StartMaster, true);
                properties.Add(Mst.Args.Names.StartSpawner, true);
                properties.Add(Mst.Args.Names.StartClientConnection, true);
                properties.Add(Mst.Args.Names.MasterIp, Mst.Args.MasterIp);
                properties.Add(Mst.Args.Names.MasterPort, Mst.Args.MasterPort);
                properties.Add(Mst.Args.Names.RoomExecutablePath, roomExePath);

                File.WriteAllText(Path.Combine(buildFolder, "application.cfg"), properties.ToReadableString("\n", "="));

                Debug.Log("Master Server build succeeded: " + (summary.totalSize / 1024) + " kb");
            }

            if (summary.result == BuildResult.Failed)
            {
                Debug.Log("Master Server build failed");
            }
        }

        [MenuItem("Master Server Toolkit/Build/Demos/Basic Spawner Mirror/Room(Headless)")]
        private static void BuildRoomForWindowsHeadless()
        {
            BuildRoomForWindows(true);
        }

        [MenuItem("Master Server Toolkit/Build/Demos/Basic Spawner Mirror/Room(Normal)")]
        private static void BuildRoomForWindowsNormal()
        {
            BuildRoomForWindows(false);
        }

        private static void BuildRoomForWindows(bool isHeadless)
        {
            string buildFolder = Path.Combine("Builds", "BasicSpawnerMirror", "Room");

            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = new[] {
                    "Assets/MasterServerToolkit/Bridges/Mirror/BasicSpawnerMirror/Scenes/Room/RoomStart.unity",
                    "Assets/MasterServerToolkit/Bridges/Mirror/BasicSpawnerMirror/Scenes/Room/RoomOnline.unity"
                },
                locationPathName = Path.Combine(buildFolder, "Room.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = isHeadless ? BuildOptions.ShowBuiltPlayer | BuildOptions.EnableHeadlessMode : BuildOptions.ShowBuiltPlayer
            };

            BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                MstProperties properties = new MstProperties();
                properties.Add(Mst.Args.Names.StartClientConnection, true);
                properties.Add(Mst.Args.Names.MasterIp, Mst.Args.MasterIp);
                properties.Add(Mst.Args.Names.MasterPort, Mst.Args.MasterPort);
                properties.Add(Mst.Args.Names.RoomIp, Mst.Args.RoomIp);
                properties.Add(Mst.Args.Names.RoomPort, Mst.Args.RoomPort);

                File.WriteAllText(Path.Combine(buildFolder, "application.cfg"), properties.ToReadableString("\n", "="));

                Debug.Log("Room build succeeded: " + (summary.totalSize / 1024) + " kb");
            }

            if (summary.result == BuildResult.Failed)
            {
                Debug.Log("Room build failed");
            }
        }

        [MenuItem("Master Server Toolkit/Build/Demos/Basic Spawner Mirror/Client")]
        private static void BuildClientForWindows()
        {
            string buildFolder = Path.Combine("Builds", "BasicSpawnerMirror", "Client");

            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = new[] {
                    "Assets/MasterServerToolkit/Bridges/Mirror/BasicSpawnerMirror/Scenes/Client/Client.unity",
                    "Assets/MasterServerToolkit/Bridges/Mirror/BasicSpawnerMirror/Scenes/Room/RoomStart.unity",
                    "Assets/MasterServerToolkit/Bridges/Mirror/BasicSpawnerMirror/Scenes/Room/RoomOnline.unity"
                },
                locationPathName = Path.Combine(buildFolder, "Client.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.ShowBuiltPlayer | BuildOptions.Development
            };

            BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                MstProperties properties = new MstProperties();
                properties.Add(Mst.Args.Names.StartClientConnection, true);
                properties.Add(Mst.Args.Names.MasterIp, Mst.Args.MasterIp);
                properties.Add(Mst.Args.Names.MasterPort, Mst.Args.MasterPort);

                File.WriteAllText(Path.Combine(buildFolder, "application.cfg"), properties.ToReadableString("\n", "="));

                Debug.Log("Client build succeeded: " + (summary.totalSize / 1024) + " kb");
            }

            if (summary.result == BuildResult.Failed)
            {
                Debug.Log("Client build failed");
            }
        }
    }
}
#endif