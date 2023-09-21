using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Authoring.Editor.Core.Solution;
using Unity.Services.CloudCode.Authoring.Editor.Modules;
using Unity.Services.DeploymentApi.Editor;
using UnityEditor;
using UnityEngine;

namespace Unity.Services.CloudCode.Authoring.Editor.Deployment
{
    class GenerateSolutionCommand : Command<CloudCodeModuleReference>
    {
        public override string Name => L10n.Tr("Generate Solution");

        static CloudCodeModuleSolutionGenerator m_SolutionGenerator;
        static ILogger m_Logger;

        public GenerateSolutionCommand(CloudCodeModuleSolutionGenerator generator, ILogger logger)
        {
            m_SolutionGenerator = generator;
            m_Logger = logger;
        }

        public override Task ExecuteAsync(IEnumerable<CloudCodeModuleReference> items, CancellationToken cancellationToken = default)
        {
            List<Task> generationTasks = new List<Task>();
            foreach (var ccmr in items)
            {
                var task = GenerateSolution(ccmr, cancellationToken);
                generationTasks.Add(task);
            }

            List<Exception> exceptions = new List<Exception>();
            foreach (var task in generationTasks)
            {
                if (task.IsFaulted)
                {
                    exceptions.Add(task.Exception);
                }
            }
            if (exceptions.Count > 0)
            {
                throw new AggregateException(exceptions);
            }
            return Task.CompletedTask;
        }

        public static Task GenerateSolution(CloudCodeModuleReference ccmr, CancellationToken cancellationToken = default)
        {
            var ccmrDir = Path.GetDirectoryName(Path.GetFullPath(ccmr.Path));
            var targetPath = Path.Combine(ccmrDir, ccmr.ModulePath);
            targetPath = Path.GetFullPath(targetPath);

            var task = m_SolutionGenerator.CreateSolutionWithProject(
                Path.GetDirectoryName(targetPath),
                Path.GetFileNameWithoutExtension(targetPath), cancellationToken);

            var solutionName = Path.GetFileNameWithoutExtension(targetPath);
            if (task.IsCompleted && task.Exception == null)
            {
                m_Logger.Log($"Solution {solutionName} generated successfully.");
            }
            else
            {
                m_Logger.LogError($"Solution {solutionName} failed to generate", task.Exception?.Message);
            }

            return task;
        }
    }
}
