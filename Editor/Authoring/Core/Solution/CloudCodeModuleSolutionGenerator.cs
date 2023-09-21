using System;
using System.Threading;
using System.Threading.Tasks;

namespace Unity.Services.CloudCode.Authoring.Editor.Core.Solution
{
    class CloudCodeModuleSolutionGenerator
    {
        IFileCopier m_FileCopier;

        public CloudCodeModuleSolutionGenerator(
            IFileCopier fileCopier)
        {
            m_FileCopier = fileCopier;
        }

        public async Task CreateSolutionWithProject(string dstDirectory, string moduleName, CancellationToken cancellationToken)
        {
            await CopyFilesFromTemplate(dstDirectory, moduleName, cancellationToken);
        }

        async Task CopyFilesFromTemplate(string dstDirectory, string moduleName, CancellationToken cancellationToken)
        {
            var slnTask = m_FileCopier.CopySolution(dstDirectory, moduleName, cancellationToken);
            var projectTask = m_FileCopier.CopyProjectWithExample(dstDirectory, moduleName, cancellationToken);
            var configTask = m_FileCopier.CopyPublishConfigs(dstDirectory, moduleName, cancellationToken);

            await Task.WhenAll(slnTask, projectTask, configTask);
        }
    }
}
