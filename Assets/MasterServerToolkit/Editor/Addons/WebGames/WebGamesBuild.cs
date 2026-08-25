using MasterServerToolkit.MasterServer;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MasterServerToolkit.Editor.Addons
{
    public class WebGamesBuild
    {
        [MenuItem(MstConstants.ToolMenu + "Build Addons/WebGames/Master Server")]
        private static void BuildMaster()
        {
            Build(false);
        }

        [MenuItem(MstConstants.ToolMenu + "Build Addons/WebGames/Master Server [Dev]")]
        private static void BuildMasterDev()
        {
            Build(true);
        }

        private static void Build(bool dev)
        {
            string buildFolder = Path.Combine("Builds", "WebGames", "Master");

            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/MasterServerToolkit/Addons/WebGames/Scenes/Master/Master.unity" },
                locationPathName = Path.Combine(buildFolder, "Master.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.ShowBuiltPlayer,
                subtarget = (int)StandaloneBuildSubtarget.Server
            };

            if (dev)
            {
                buildPlayerOptions.options = BuildOptions.ShowBuiltPlayer | BuildOptions.Development;
            }

            BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                string appConfig = Mst.Args.AppConfigFile(buildFolder);

                MstProperties properties = new MstProperties();
                properties.Add(Mst.Args.Names.StartMaster, true);
                properties.Add(Mst.Args.Names.MasterIp, Mst.Args.MasterIp);
                properties.Add(Mst.Args.Names.MasterPort, Mst.Args.MasterPort);
                MasterServerToolkit.Utils.Editor.MstDemoAccessConfig.ConfigureMaster(properties, buildFolder);

                File.WriteAllText(appConfig, properties.ToReadableString("\n", "="));

                Debug.Log("Server build succeeded: " + (summary.totalSize / 1024) + " kb");
            }

            if (summary.result == BuildResult.Failed)
            {
                Debug.Log("Server build failed");
            }
        }
    }
}
