using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Utils.Editor;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MasterServerToolkit.Bridges.Dashboard
{
    public class DashboardBuild
    {
        [MenuItem(MstConstants.ToolMenu + "Build/Dashboard Service")]
        private static void BuildMasterAndSpawnerForWindows()
        {
            string buildFolder = Path.Combine("Builds", "Dashboard");

            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/MasterServerToolkit/Bridges/Dashboard/Scenes/Dashboard.unity" },
                locationPathName = Path.Combine(buildFolder, "DashboardService.exe"),
                target = BuildTarget.StandaloneWindows64,
#if UNITY_2021_1_OR_NEWER
                options = BuildOptions.ShowBuiltPlayer | BuildOptions.Development,
                subtarget = (int)StandaloneBuildSubtarget.Server
#else
                options = BuildOptions.EnableHeadlessMode | BuildOptions.ShowBuiltPlayer | BuildOptions.Development
#endif
            };

            BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                MstProperties properties = new MstProperties();
                properties.Add(Mst.Args.Names.StartMaster, true);
                properties.Add(Mst.Args.Names.StartSpawner, true);
                properties.Add(Mst.Args.Names.StartClientConnection, true);
                properties.Add(Mst.Args.Names.DashboardIp, "127.0.0.1");
                properties.Add(Mst.Args.Names.DashboardPort, 5090);

                File.WriteAllText(Path.Combine(buildFolder, "application.cfg"), properties.ToReadableString("\n", "="));

                Debug.Log("Master Server build succeeded: " + (summary.totalSize / 1024) + " kb");
            }

            if (summary.result == BuildResult.Failed)
            {
                Debug.Log("Master Server build failed");
            }
        }
    }
}