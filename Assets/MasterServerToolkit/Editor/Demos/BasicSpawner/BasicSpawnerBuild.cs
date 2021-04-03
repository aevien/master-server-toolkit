using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MasterServerToolkit.MasterServer.Examples.BasicSpawner
{
    public class BasicSpawnerBuild : MonoBehaviour
    {
        [MenuItem("Master Server Toolkit/Build/Demos/Basic Spawner/Room(Headless)")]
        private static void BuildRoomForWindowsHeadless()
        {
            BuildRoomForWindows(true);
        }

        [MenuItem("Master Server Toolkit/Build/Demos/Basic Spawner/Room(Normal)")]
        private static void BuildRoomForWindowsNormal()
        {
            BuildRoomForWindows(false);
        }

        private static void BuildRoomForWindows(bool isHeadless)
        {
            string buildFolder = Path.Combine("Builds", "BasicSpawner", "Room");

            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = new[] {
                    "Assets/MasterServerToolkit/Demos/BasicSpawner/Scenes/Room/Room.unity"
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

        [MenuItem("Master Server Toolkit/Build/Demos/Basic Spawner/Spawner")]
        private static void BuildSpawnerForWindows()
        {
            string buildFolder = Path.Combine("Builds", "BasicSpawner", "Room");
            string roomExePath = Path.Combine(Directory.GetCurrentDirectory(), "Builds", "BasicSpawner", "Room", "Room.exe");

            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = new[] {
                    "Assets/MasterServerToolkit/Demos/BasicSpawner/Scenes/Spawner/Spawner.unity"
                },
                locationPathName = Path.Combine(buildFolder, "Spawner.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.ShowBuiltPlayer | BuildOptions.EnableHeadlessMode
            };

            BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                MstProperties properties = new MstProperties();
                properties.Add(Mst.Args.Names.StartSpawner, true);
                properties.Add(Mst.Args.Names.StartClientConnection, true);
                properties.Add(Mst.Args.Names.MasterIp, Mst.Args.MasterIp);
                properties.Add(Mst.Args.Names.MasterPort, Mst.Args.MasterPort);
                properties.Add(Mst.Args.Names.RoomExecutablePath, roomExePath);

                File.WriteAllText(Path.Combine(buildFolder, "application.cfg"), properties.ToReadableString("\n", "="));

                Debug.Log("Spawner build succeeded: " + (summary.totalSize / 1024) + " kb");
            }

            if (summary.result == BuildResult.Failed)
            {
                Debug.Log("Spawner build failed");
            }
        }

        [MenuItem("Master Server Toolkit/Build/Demos/Basic Spawner/Client")]
        private static void BuildClientForWindows()
        {
            string buildFolder = Path.Combine("Builds", "BasicSpawner", "Client");

            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = new[] {
                    "Assets/MasterServerToolkit/Demos/BasicSpawner/Scenes/Client/Client.unity",
                    "Assets/MasterServerToolkit/Demos/BasicSpawner/Scenes/Room/Room.unity"
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