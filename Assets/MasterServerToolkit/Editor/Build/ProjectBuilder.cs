#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MasterServerToolkit.Editor
{
    /// <summary>
    /// Simple, universal Unity project builder.
    /// One static Build method takes all required parameters and performs the build.
    /// No complex configurations — just call the method with parameters.
    /// </summary>
    public static class ProjectBuilder
    {
        public enum BuildCleanMode
        {
            Ask,
            Clean,
            Incremental
        }

        /// <summary>
        /// Universal project build method. Receives all parameters directly.
        /// </summary>
        /// <param name="buildName">Executable name (without extension)</param>
        /// <param name="scenes">Array of scene paths to include in the build</param>
        /// <param name="outputFolder">Folder where the build will be saved</param>
        /// <param name="isServer">true for server (headless) build, false for client</param>
        /// <param name="configProperties">Key-value pairs for application.cfg (can be null)</param>
        /// <param name="isDevelopment">Enable Development Build (default true)</param>
        /// <param name="buildTarget">Target platform (default Windows 64)</param>
        /// <returns>BuildReport with the build results (or null if validation failed)</returns>
        public static BuildReport Build(
            string buildName,
            string[] scenes,
            string outputFolder,
            bool isServer,
            Dictionary<string, object> configProperties = null,
            bool isDevelopment = true,
            BuildTarget buildTarget = BuildTarget.StandaloneWindows64,
            BuildCleanMode cleanMode = BuildCleanMode.Ask)
        {
            return Build(
                buildName,
                scenes,
                outputFolder,
                isServer,
                configProperties,
                isDevelopment,
                buildTarget,
                cleanMode,
                null
            );
        }

        /// <summary>
        /// Builds a player while allowing the output directory to differ from the executable name.
        /// </summary>
        /// <param name="buildName">Executable name (without extension)</param>
        /// <param name="scenes">Array of scene paths to include in the build</param>
        /// <param name="outputFolder">Root folder where the build will be saved</param>
        /// <param name="isServer">true for server (headless) build, false for client</param>
        /// <param name="configProperties">Key-value pairs for application.cfg (can be null)</param>
        /// <param name="isDevelopment">Enable Development Build</param>
        /// <param name="buildTarget">Target platform</param>
        /// <param name="cleanMode">Controls whether existing build output is removed</param>
        /// <param name="outputDirectoryName">Output directory name under outputFolder</param>
        /// <returns>BuildReport with the build results (or null if validation failed)</returns>
        public static BuildReport Build(
            string buildName,
            string[] scenes,
            string outputFolder,
            bool isServer,
            Dictionary<string, object> configProperties,
            bool isDevelopment,
            BuildTarget buildTarget,
            BuildCleanMode cleanMode,
            string outputDirectoryName)
        {
            // Validate inputs before doing any work
            if (!ValidateInputParameters(buildName, scenes, outputFolder))
                return null;

            // Compute the final path to the built executable (or .app on macOS)
            string fullBuildPath = GetFullBuildPath(buildName, outputFolder, outputDirectoryName, buildTarget);

            if (!TryResolveCleanMode(fullBuildPath, cleanMode, out bool cleanBeforeBuild))
            {
                Debug.Log($"Build canceled");
                return null;
            }

            // Ensure a clean build directory (prevents stale files from previous builds)
            if (cleanBeforeBuild)
            {
                Debug.Log($"All files in the {fullBuildPath} folder have been deleted.");
                EnsureCleanDirectory(fullBuildPath, buildTarget);
            }

            Debug.Log($"Starting build: {buildName} ({(isServer ? "Server" : "Client")})");

            // Create the directory if it still doesn't exist
            string buildDirectory = Path.GetDirectoryName(fullBuildPath);
            if (!Directory.Exists(buildDirectory))
            {
                Directory.CreateDirectory(buildDirectory);
                Debug.Log($"Created directory: {buildDirectory}");
            }

            // Prepare build options
            BuildPlayerOptions buildOptions = CreateBuildPlayerOptions(
                scenes, fullBuildPath, buildTarget, isServer, isDevelopment);

            // Run the actual Unity build
            BuildReport report = BuildPipeline.BuildPlayer(buildOptions);

            // Safety: if report is null, something blocked the build entirely
            if (report == null)
            {
                Debug.LogError("BuildPipeline returned null. Build did not start.");
                return null;
            }

            // Handle result and create config files
            ProcessBuildResults(buildName, report, configProperties, fullBuildPath, buildTarget);

            return report;
        }

        /// <summary>
        /// Overload when no configuration file is needed.
        /// A convenience wrapper for calling the main method without extra parameters.
        /// </summary>
        public static BuildReport Build(
            string buildName,
            string[] scenes,
            string outputFolder,
            bool isServer)
        {
            return Build(buildName, scenes, outputFolder, isServer, null);
        }

        private static bool TryResolveCleanMode(string fullBuildPath, BuildCleanMode cleanMode, out bool cleanBeforeBuild)
        {
            cleanBeforeBuild = false;

            if (cleanMode == BuildCleanMode.Clean)
            {
                cleanBeforeBuild = true;
                return true;
            }

            if (cleanMode == BuildCleanMode.Incremental)
                return true;

            if (Application.isBatchMode)
            {
                cleanBeforeBuild = true;
                return true;
            }

            int pressed = EditorUtility.DisplayDialogComplex(
                "Build Alert!",
                $"Output path: {fullBuildPath}\n\nDelete existing build output before building?",
                "Delete and build",
                "Build without delete",
                "Cancel"
            );

            if (pressed == 2)
                return false;

            cleanBeforeBuild = pressed == 0;
            return true;
        }

        /// <summary>
        /// Validate input parameters early to fail fast with clear messages.
        /// Uses AssetDatabase to confirm scenes exist in the project.
        /// </summary>
        private static bool ValidateInputParameters(string buildName, string[] scenes, string outputFolder)
        {
            if (string.IsNullOrEmpty(buildName))
            {
                Debug.LogError("Build name cannot be empty.");
                return false;
            }

            if (scenes == null || scenes.Length == 0)
            {
                Debug.LogError("At least one scene must be provided for the build.");
                return false;
            }

            if (string.IsNullOrEmpty(outputFolder))
            {
                Debug.LogError("Output folder cannot be empty.");
                return false;
            }

            // Validate that each scene path points to a valid SceneAsset
            foreach (string scenePath in scenes)
            {
                if (string.IsNullOrEmpty(scenePath))
                {
                    Debug.LogError("Encountered an empty scene path.");
                    return false;
                }

                var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
                if (sceneAsset == null)
                {
                    Debug.LogError($"Scene not found as asset: {scenePath}");
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Creates the final path to the built executable including appropriate extension.
        /// Windows -> .exe, Linux -> usually no extension, macOS -> .app bundle.
        /// The output directory can differ from the executable name so multiple build variants can coexist.
        /// </summary>
        private static string GetFullBuildPath(
            string buildName,
            string outputFolder,
            string outputDirectoryName,
            BuildTarget buildTarget)
        {
            string directoryName = string.IsNullOrWhiteSpace(outputDirectoryName)
                ? buildName
                : outputDirectoryName;
            string dir = Path.Combine(outputFolder, directoryName);
            string fileName = buildName;

            switch (buildTarget)
            {
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                    return Path.Combine(dir, fileName + ".exe");
                case BuildTarget.StandaloneOSX:
                    // Unity expects a .app bundle path as the location
                    return Path.Combine(dir, fileName + ".app");
                case BuildTarget.StandaloneLinux64:
                    // Modern Unity usually uses no extension here.
                    // If you want .x86_64, uncomment the next line.
                    // fileName += ".x86_64";
                    return Path.Combine(dir, fileName);
                case BuildTarget.WebGL:
                    return dir;
                default:
                    return Path.Combine(dir, fileName);
            }
        }

        /// <summary>
        /// Creates BuildPlayerOptions based on provided parameters.
        /// Configures flags for server/client and dev/prod.
        /// </summary>
        private static BuildPlayerOptions CreateBuildPlayerOptions(
            string[] scenes,
            string buildPath,
            BuildTarget buildTarget,
            bool isServer,
            bool isDevelopment)
        {
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = buildPath,
                target = buildTarget,
                // Do NOT use ShowBuiltPlayer (it launches the app). Keep it clean for editor/CI.
                options = BuildOptions.None
            };

            // Development settings
            if (isDevelopment)
                options.options |= BuildOptions.Development;

#if UNITY_2021_1_OR_NEWER
            // Server subtarget is supported only for Standalone targets
            bool standalone =
                buildTarget == BuildTarget.StandaloneWindows ||
                buildTarget == BuildTarget.StandaloneWindows64 ||
                buildTarget == BuildTarget.StandaloneOSX ||
                buildTarget == BuildTarget.StandaloneLinux64;

            if (isServer && !standalone)
            {
                Debug.LogWarning("Server subtarget is supported only for Standalone platforms. Falling back to Player.");
                isServer = false;
            }

            options.subtarget = (int)(isServer ? StandaloneBuildSubtarget.Server : StandaloneBuildSubtarget.Player);
#else
            if (isServer)
                options.options |= BuildOptions.EnableHeadlessMode;
#endif
            return options;
        }

        /// <summary>
        /// Handle build results: log summary and create configuration files.
        /// Also reveals the output in Finder/Explorer for convenience.
        /// </summary>
        private static void ProcessBuildResults(
            string buildName,
            BuildReport report,
            Dictionary<string, object> configProperties,
            string fullBuildPath,
            BuildTarget target)
        {
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"Build {buildName} completed successfully!");
                Debug.Log($"-Size: {summary.totalSize / (1024f * 1024f):F2} MB");
                Debug.Log($"-Duration: {summary.totalTime}");
                Debug.Log($"-Output: {summary.outputPath}");

                // Create application.cfg next to the executable (or inside .app/Contents/MacOS on macOS)
                if (configProperties != null && configProperties.Count > 0)
                {
                    string execDir = GetExecutableDirectory(fullBuildPath, target);
                    CreateConfigurationFile(configProperties, execDir);
                }

                // Show the built file (or its folder) in system file explorer
                EditorUtility.RevealInFinder(summary.outputPath);
                // To reveal the folder instead, use:
                // EditorUtility.RevealInFinder(Path.GetDirectoryName(summary.outputPath));
            }
            else if (summary.result == BuildResult.Failed)
            {
                Debug.LogError($"Build {buildName} failed!");
                foreach (var step in report.steps)
                {
                    foreach (var message in step.messages)
                    {
                        if (message.type == LogType.Error || message.type == LogType.Exception)
                        {
                            Debug.LogError($"Build error: {message.content}");
                        }
                    }
                }
            }
            else
            {
                Debug.LogWarning($"Build {buildName} completed with warnings.");
            }
        }

        /// <summary>
        /// Creates application.cfg next to the executable.
        /// Uses UTF-8 (no BOM) and Environment.NewLine.
        /// </summary>
        private static void CreateConfigurationFile(Dictionary<string, object> configProperties, string targetDir)
        {
            try
            {
                string configPath = Path.Combine(targetDir, "application.cfg");

                var configLines = new List<string>(configProperties.Count);
                foreach (var property in configProperties)
                {
                    // Convert values to string safely
                    string valueString = property.Value?.ToString() ?? "null";
                    configLines.Add($"{property.Key}={valueString}");
                }

                File.WriteAllText(configPath, string.Join(Environment.NewLine, configLines), new UTF8Encoding(false));
                Debug.Log($"Created configuration file: {configPath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to create configuration file: {e.Message}");
            }
        }

        /// <summary>
        /// Gets the directory where the final executable lives.
        /// On macOS this is inside MyApp.app/Contents/MacOS.
        /// On Windows/Linux it's the directory of the executable file.
        /// </summary>
        private static string GetExecutableDirectory(string fullBuildPath, BuildTarget target)
        {
            switch (target)
            {
                case BuildTarget.StandaloneOSX:
                    return Path.Combine(fullBuildPath, "Contents", "MacOS");
                default:
                    return Path.GetDirectoryName(fullBuildPath);
            }
        }

        /// <summary>
        /// Removes previous build output to guarantee clean, repeatable results.
        /// For macOS .app, deletes the .app bundle directory.
        /// For Windows/Linux, deletes the target directory of the executable.
        /// </summary>
        private static void EnsureCleanDirectory(string pathToExecutableOrApp, BuildTarget target)
        {
            string dirToDelete;

            if (target == BuildTarget.StandaloneOSX)
            {
                // pathToExecutableOrApp is .../Name.app
                dirToDelete = pathToExecutableOrApp;
            }
            else if (target == BuildTarget.WebGL)
            {
                dirToDelete = pathToExecutableOrApp;
            }
            else
            {
                // For Windows/Linux, delete the containing directory of the executable
                dirToDelete = Path.GetDirectoryName(pathToExecutableOrApp);
            }

            if (string.IsNullOrEmpty(dirToDelete))
                return;

            if (Directory.Exists(dirToDelete))
            {
                try
                {
                    Directory.Delete(dirToDelete, true);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Failed to clean build directory: {dirToDelete}. Reason: {e.Message}");
                }
            }
        }

        /// <summary>
        /// Helper to create a configuration dictionary from key-value pairs.
        /// Usage: CreateConfig("key1", "value1", "key2", 42, "key3", true)
        /// </summary>
        public static Dictionary<string, object> CreateConfig(params object[] keyValuePairs)
        {
            if (keyValuePairs.Length % 2 != 0)
                throw new ArgumentException("Number of parameters must be even (key-value pairs).");

            var config = new Dictionary<string, object>();

            for (int i = 0; i < keyValuePairs.Length; i += 2)
            {
                string key = keyValuePairs[i]?.ToString();
                object value = keyValuePairs[i + 1];

                if (key != null)
                    config[key] = value;
            }

            return config;
        }
    }
}
#endif
