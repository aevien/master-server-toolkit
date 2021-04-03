using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MasterServerToolkit.MasterServer.Examples.BasicAuthorization
{
    public class BasicAuthorizationPlayerBuild
    {
        [MenuItem("Master Server Toolkit/Build/Demos/Basic Authorization/All")]
        private static void BuildBoth()
        {
            BuildMaster();
            BuildClient();
        }

        [MenuItem("Master Server Toolkit/Build/Demos/Basic Authorization/Master Server")]
        private static void BuildMaster()
        {
            string buildFolder = Path.Combine("Builds", "BasicAuthorization", "MasterServer");

            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/MasterServerToolkit/Demos/BasicAuthorization/Scenes/MasterServer/MasterServer.unity" },
                locationPathName = Path.Combine(buildFolder, "MasterServer.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.EnableHeadlessMode | BuildOptions.ShowBuiltPlayer | BuildOptions.Development
            };

            BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                MstProperties properties = new MstProperties();
                properties.Add(Mst.Args.Names.StartMaster, true);
                properties.Add(Mst.Args.Names.MasterIp, Mst.Args.MasterIp);
                properties.Add(Mst.Args.Names.MasterPort, Mst.Args.MasterPort);

                File.WriteAllText(Path.Combine(buildFolder, "application.cfg"), properties.ToReadableString("\n", "="));

                Debug.Log("Server build succeeded: " + (summary.totalSize / 1024) + " kb");
            }

            if (summary.result == BuildResult.Failed)
            {
                Debug.Log("Server build failed");
            }
        }

        [MenuItem("Master Server Toolkit/Build/Demos/Basic Authorization/Client")]
        private static void BuildClient()
        {
            string buildFolder = Path.Combine("Builds", "BasicAuthorization", "Client");

            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/MasterServerToolkit/Demos/BasicAuthorization/Scenes/Client/Client.unity" },
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