using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MasterServerToolkit.MasterServer.Examples.BasicConnection
{
    public class BasicConnectionPlayerBuild
    {
        [MenuItem("Master Server Toolkit/Build/Demos/Basic Connection/All")]
        private static void BuildBoth()
        {
            BuildMasterForWindows();
            BuildClientForWindows();
        }

        [MenuItem("Master Server Toolkit/Build/Demos/Basic Connection/Master Server")]
        private static void BuildMasterForWindows()
        {
            string buildFolder = Path.Combine("Builds", "BasicConnection", "MasterServer");

            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/MasterServerToolkit/Demos/BasicConnection/Scenes/MasterServer/MasterServer.unity" },
                locationPathName = Path.Combine(buildFolder, "MasterServer.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.EnableHeadlessMode | BuildOptions.ShowBuiltPlayer | BuildOptions.Development
            };

            BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                File.WriteAllText(Path.Combine(buildFolder, "Start Server.bat"), "start MasterServer.exe -msfStartMaster true");

                Debug.Log("Server build succeeded: " + (summary.totalSize / 1024) + " kb");
            }

            if (summary.result == BuildResult.Failed)
            {
                Debug.Log("Server build failed");
            }
        }

        [MenuItem("Master Server Toolkit/Build/Demos/Basic Connection/Client")]
        private static void BuildClientForWindows()
        {
            string buildFolder = Path.Combine("Builds", "BasicConnection", "Client");

            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/MasterServerToolkit/Demos/BasicConnection/Scenes/Client/Client.unity" },
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