using System;
using System.IO;
using System.Text;
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
                scenes = new[] { "Assets/MasterServerToolkit/Demos/BasicSpawnerMirror/Scenes/MasterAndSpawner/MasterAndSpawner.unity" },
                locationPathName = Path.Combine(buildFolder, "MasterAndSpawner.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.EnableHeadlessMode | BuildOptions.ShowBuiltPlayer | BuildOptions.Development
            };

            BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                StringBuilder arguments = new StringBuilder();
                arguments.Append("@echo off\n");
                arguments.Append("start \"Basic Spawner Mirror - Master and Spawner\" ");
                arguments.Append("MasterAndSpawner.exe ");
                arguments.Append($"{Mst.Args.Names.StartMaster} ");
                arguments.Append($"{Mst.Args.Names.StartSpawner} ");
                arguments.Append($"{Mst.Args.Names.StartClientConnection} ");
                arguments.Append($"{Mst.Args.Names.MasterIp} {Mst.Args.MasterIp} ");
                arguments.Append($"{Mst.Args.Names.MasterPort} {Mst.Args.MasterPort} ");
                arguments.Append($"{Mst.Args.Names.RoomExecutablePath} {roomExePath} ");

                File.WriteAllText(Path.Combine(buildFolder, "Start Master Server and Spawner.bat"), arguments.ToString());

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
                    "Assets/MasterServerToolkit/Demos/BasicSpawnerMirror/Scenes/Room/RoomStart.unity",
                    "Assets/MasterServerToolkit/Demos/BasicSpawnerMirror/Scenes/Room/RoomOnline.unity"
                },
                locationPathName = Path.Combine(buildFolder, "Room.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = isHeadless ? BuildOptions.ShowBuiltPlayer | BuildOptions.EnableHeadlessMode : BuildOptions.ShowBuiltPlayer
            };

            BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                StringBuilder arguments = new StringBuilder();
                arguments.Append("@echo off\n");
                arguments.Append("start \"Basic Spawner Mirror - Room\" ");
                arguments.Append("Room.exe ");
                arguments.Append($"{Mst.Args.Names.StartClientConnection} ");
                arguments.Append($"{Mst.Args.Names.MasterIp} 127.0.0.1 ");
                arguments.Append($"{Mst.Args.Names.MasterPort} 5000 ");
                arguments.Append($"{Mst.Args.Names.RoomIp} 127.0.0.1 ");
                arguments.Append($"{Mst.Args.Names.RoomPort} 7777 ");

                File.WriteAllText(Path.Combine(buildFolder, "Start Room.bat"), arguments.ToString());

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
                    "Assets/MasterServerToolkit/Demos/BasicSpawnerMirror/Scenes/Client/Client.unity",
                    "Assets/MasterServerToolkit/Demos/BasicSpawnerMirror/Scenes/Room/RoomStart.unity",
                    "Assets/MasterServerToolkit/Demos/BasicSpawnerMirror/Scenes/Room/RoomOnline.unity"
                },
                locationPathName = Path.Combine(buildFolder, "Client.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.ShowBuiltPlayer | BuildOptions.Development
            };

            BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log("Client build succeeded: " + (summary.totalSize / 1024) + " kb");
            }

            if (summary.result == BuildResult.Failed)
            {
                Debug.Log("Client build failed");
            }
        }
    }
}