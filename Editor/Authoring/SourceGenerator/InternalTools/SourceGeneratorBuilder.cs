using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Authoring.Editor.Core.Dotnet;
using Unity.Services.CloudCode.Editor.Shared.DependencyInversion;
using Unity.Services.CloudCode.Editor.Shared.Infrastructure.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using ILogger = Unity.Services.CloudCode.Authoring.Editor.Core.Logging.ILogger;

namespace Unity.Services.CloudCode.Authoring.Editor.SourceGenerator
{
    /// <summary>
    /// Builds the Cloud Code Roslyn source generator assembly via
    /// <c>dotnet publish</c>, imports the output DLL into the
    /// package, and configures the asset as a Roslyn analyzer plugin.
    /// </summary>
    // ReSharper disable once ClassNeverInstantiated.Global
    sealed class SourceGeneratorBuilder : ISourceGeneratorBuilder
    {
        const string k_MenuPath = "Services/CloudCode/Source Generator/Build Source Generator";
        const string k_ProjectFilename = "SourceGenerator.csproj";

        static readonly string k_DllDirectoryRelativePath = PathUtils.Join(CloudCodePackage.RootPath, "Runtime", "SourceGenerator");
        static readonly string k_DllDirectoryAbsolutePath = Path.GetFullPath(k_DllDirectoryRelativePath);
        static readonly string k_DllAssetPath = PathUtils.Join(k_DllDirectoryRelativePath, "CloudCodeSourceGenerator.dll");

        static readonly string[] k_DllLabels =
        {
            "RoslynAnalyzer", "RunOnlyOnAssembliesWithReference", "SourceGenerator"
        };

        static readonly Lazy<List<BuildTarget>> k_SupportedBuildTargets = new(() => GetSupportedBuildTargets().ToList());

        readonly IDotnetRunner m_DotnetRunner;

        readonly ILogger m_Logger;

        public SourceGeneratorBuilder(ILogger logger, IDotnetRunner dotnetRunner)
        {
            m_Logger = logger;
            m_DotnetRunner = dotnetRunner;
        }

        static ISourceGeneratorBuilder Instance
        {
            get
            {
                try
                {
                    return CloudCodeAuthoringServices.Instance.GetService<ISourceGeneratorBuilder>();
                }
                catch (DependencyNotFoundException)
                {
                    Debug.LogError($"Failed to get the {nameof(ISourceGeneratorBuilder)} from the service provider. Make sure \"UNITY_SERVICES_CLOUDCODE_INTERNAL\" is defined in the project settings player's scripting defines symbols.");
                    return null;
                }
            }
        }

        /// <summary>
        /// Publishes <c>SourceGenerator.csproj</c> in Release
        /// mode without debug symbols or dependency file output,
        /// and applies analyzer-oriented plugin settings.
        /// </summary>
        public async Task Build()
        {
            m_Logger.LogInfo($"Publishing {k_ProjectFilename}");
            var progressId = Progress.Start("Building Source Generator", $"Publishing {k_ProjectFilename}...", Progress.Options.Sticky);

            string packageDirectoryPath;
            try
            {
                packageDirectoryPath = Path.GetFullPath(CloudCodePackage.RootPath);
                if (!Directory.Exists(packageDirectoryPath))
                {
                    m_Logger.LogError($"Directory {packageDirectoryPath} does not exist.");
                    Progress.Finish(progressId, Progress.Status.Failed);
                    return;
                }
            }
            catch (Exception e)
            {
                m_Logger.LogError(e);
                Progress.Finish(progressId, Progress.Status.Failed);
                return;
            }

            if (!TryGetFiles(new DirectoryInfo(packageDirectoryPath), k_ProjectFilename, out var matches, m_Logger))
            {
                Progress.Finish(progressId, Progress.Status.Failed);
                return;
            }

            if (matches.Length > 1)
            {
                m_Logger.LogError($"There should be only one {k_ProjectFilename} in {packageDirectoryPath}.");
                Progress.Finish(progressId, Progress.Status.Failed);
                return;
            }

            Progress.Report(progressId, 0.2f, "Verifying dotnet availability.");
            if (!await m_DotnetRunner.IsDotnetAvailable())
            {
                m_Logger.LogError("Dotnet is not available.");
                Progress.Finish(progressId, Progress.Status.Failed);
                return;
            }

            Progress.Report(progressId, 0.3f, "Invoking dotnet publish.");
            try
            {
                await m_DotnetRunner.ExecuteDotnetAsync(new List<string>
                {
                    "publish",
                    "-c",
                    "Release",
                    matches[0].FullName,
                    "--output",
                    k_DllDirectoryAbsolutePath,
                    "/p:DebugType=None",
                    "/p:DebugSymbols=false",
                    "/p:GenerateDependencyFile=false"
                }, Application.exitCancellationToken);
                m_Logger.LogInfo("Source Generator build completed successfully.");
            }
            catch (Exception e)
            {
                m_Logger.LogError($"Failed to publish {e.Message}.");
                Progress.Finish(progressId, Progress.Status.Failed);
                return;
            }

            try
            {
                Progress.Report(progressId, 0.8f, "Configuring DLL.");
                AssetDatabase.ImportAsset(k_DllAssetPath);
                ConfigureDLL(k_DllAssetPath, m_Logger);
                Progress.Finish(progressId);
            }
            catch (Exception e)
            {
                Progress.Finish(progressId, Progress.Status.Failed);
                m_Logger.LogError(e);
            }
        }

        [MenuItem(k_MenuPath, validate = true)]
        static bool MenuActionValidate()
        {
            return Instance != null;
        }

        [MenuItem(k_MenuPath)]
        static void MenuAction()
        {
            _ = Instance.Build();
        }

        /// <summary>
        /// Recursively searches <paramref name="directory"/>
        /// for files named <paramref name="filename"/> without
        /// throwing; logs and returns an empty array on failure.
        /// </summary>
        /// <param name="directory">Root directory to search.</param>
        /// <param name="filename">
        /// File name to match (e.g. project file name).
        /// </param>
        /// <param name="matches">
        /// Matching files, or empty if the
        /// search failed or no files were found.
        /// </param>
        /// <param name="logger">Logger used to report exceptions.</param>
        /// <returns>
        /// <c>true</c> if the search completed and at least one file was found;
        /// <c>false</c> if no files matched or an exception occurred.
        /// </returns>
        static bool TryGetFiles(DirectoryInfo directory, string filename, out FileInfo[] matches, ILogger logger)
        {
            try
            {
                matches = directory.GetFiles(filename, SearchOption.AllDirectories);
                if (matches.Length > 0)
                {
                    return true;
                }

                logger.LogError($"No {filename} files found in \"{directory.FullName}\".");
                return false;
            }
            catch (Exception e)
            {
                logger.LogError(e);
                matches = Array.Empty<FileInfo>();
                return false;
            }
        }

        /// <summary>
        /// Configures a DLL so it is recognized
        /// by the editor as a source generator.
        /// </summary>
        /// <param name="filepath">The filepath to the DLL to configure.</param>
        /// <param name="logger">Logger used to report exceptions.</param>
        static void ConfigureDLL(string filepath, ILogger logger)
        {
            var importer = AssetImporter.GetAtPath(filepath) as PluginImporter;
            if (importer == null)
            {
                logger.LogError($"Could not load the {nameof(PluginImporter)} at path {filepath}.");
                return;
            }

            // Ensure the resulting DLL is excluded
            // from every supported platforms.
            importer.SetCompatibleWithAnyPlatform(false);
            importer.SetCompatibleWithEditor(false);
            foreach (var supportedBuildTarget in k_SupportedBuildTargets.Value)
            {
                importer.SetCompatibleWithPlatform(supportedBuildTarget, false);
            }

            // The general properties:
            // - "Auto Reference"      (m_IsExplicitlyReference)
            // - "Validate References" (m_ValidateReferences)
            // are not exposed by the PluginImporter API, so
            // they are set via the serialized property API.
            var serializedImporter = new SerializedObject(importer);
            serializedImporter.FindProperty("m_IsExplicitlyReferenced").boolValue = true;
            serializedImporter.FindProperty("m_ValidateReferences").boolValue = false;
            serializedImporter.ApplyModifiedProperties();

            AssetDatabase.SetLabels(importer, k_DllLabels);

            importer.SaveAndReimport();
        }

        /// <summary>
        /// Returns the distinct set of <see cref="BuildTarget"/>
        /// that are currently available in the editor.
        /// </summary>
        static IEnumerable<BuildTarget> GetSupportedBuildTargets()
        {
            return Enum.GetValues(typeof(BuildTargetGroup))
                .Cast<BuildTargetGroup>()
                .SelectMany(g =>
                    Enum.GetValues(typeof(BuildTarget)).Cast<BuildTarget>()
                        .Where(b => BuildPipeline.IsBuildTargetSupported(g, b)))
                .Distinct();
        }
    }
}
