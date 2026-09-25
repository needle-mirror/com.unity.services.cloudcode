using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Authoring.Editor.Analytics;
using Unity.Services.CloudCode.Authoring.Editor.Core.Deployment.ModuleGeneration.Exceptions;
using Unity.Services.CloudCode.Authoring.Editor.Core.Logging;
using Unity.Services.CloudCode.Authoring.Editor.Core.Solution;
using Unity.Services.CloudCode.Authoring.Editor.Modules;
using Unity.Services.DeploymentApi.Editor;
using UnityEditor;

namespace Unity.Services.CloudCode.Authoring.Editor.Deployment
{
    class CloudCodeModuleReferenceGenerateSolutionCommand : Command<CloudCodeModuleReference>
    {
        public override string Name => L10n.Tr("Generate Solution");

        static CloudCodeModuleSolutionGenerator m_SolutionGenerator;
        static ILogger m_Logger;
        static Regex m_ValidNameRegex = new Regex("^[a-zA-Z][a-zA-Z_0-9]*$", RegexOptions.Compiled);

        readonly ISolutionGenerationAnalytics m_Analytics;

        public CloudCodeModuleReferenceGenerateSolutionCommand(
            CloudCodeModuleSolutionGenerator generator,
            ILogger logger,
            ISolutionGenerationAnalytics analytics)
        {
            m_SolutionGenerator = generator;
            m_Logger = logger;
            m_Analytics = analytics;
        }

        public override async Task ExecuteAsync(IEnumerable<CloudCodeModuleReference> items, CancellationToken cancellationToken = default)
        {
            var generation = Task.WhenAll(items.Select(ccmr => GenerateSolution(ccmr, cancellationToken)));
            try
            {
                await generation;
            }
            catch (Exception)
            {
                // Awaiting rethrows only the first fault; the task itself carries them all.
            }

            // A cancelled task carries no exception, so it would otherwise report a success. Only set
            // when nothing faulted, so a mixed batch still reports its fault below.
            if (generation.IsCanceled)
                throw new TaskCanceledException(generation);

            m_Analytics.SendSolutionGeneratedEvent(AnalyticsSource.DeploymentWindow, generation.Exception);

            if (generation.Exception != null)
            {
                throw generation.Exception;
            }
        }

        public static async Task GenerateSolution(CloudCodeModuleReference ccmr, CancellationToken cancellationToken = default)
        {
            var referenceFileDir = Path.GetDirectoryName(Path.GetFullPath(ccmr.Path));
            var targetPath = Path.Combine(referenceFileDir, ccmr.ModulePath);
            targetPath = Path.GetFullPath(targetPath);

            var solutionName = Path.GetFileNameWithoutExtension(targetPath);
            if (!m_ValidNameRegex.IsMatch(solutionName))
            {
                var msg =
                    "Cloud Code Module will not be generated, selected 'Path' contains invalid characters. The solution name should only contain alphanumerical characters and underscores.";

                m_Logger.LogError(msg);
                throw new InvalidSolutionNameException(msg);
            }
            var solutionPath = Path.Combine(
                Path.GetDirectoryName(targetPath),
                Path.GetFileNameWithoutExtension(targetPath) + CloudCodeModuleReferenceResources.SolutionExtension);

            if (File.Exists(solutionPath))
            {
                throw new SolutionAlreadyExistsException(
                    $"File {solutionPath} already exists. You cannot override an existing solution.");
            }

            try
            {
                await m_SolutionGenerator.CreateSolutionWithProject(
                    Path.GetDirectoryName(targetPath),
                    Path.GetFileNameWithoutExtension(targetPath), cancellationToken);

                m_Logger.LogInfo($"Solution '{solutionName}' generated successfully.");
            }
            catch (Exception e)
            {
                m_Logger.LogError($"Failed to generate solution: {e}");
                throw;
            }
        }
    }
}
