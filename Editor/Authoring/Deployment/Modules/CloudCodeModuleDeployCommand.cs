using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Authoring.Editor.Core.Dotnet;
using Unity.Services.CloudCode.Authoring.Editor.Core.IO;
using Unity.Services.CloudCode.Authoring.Editor.Core.Model;
using Unity.Services.CloudCode.Authoring.Editor.Modules;
using Unity.Services.CloudCode.Authoring.Editor.Scripts;
using Unity.Services.CloudCode.Authoring.Editor.Shared.Infrastructure.Collections;
using Unity.Services.DeploymentApi.Editor;
using UnityEditor;

namespace Unity.Services.CloudCode.Authoring.Editor.Deployment.Modules
{
    class CloudCodeModuleDeployCommand : Command<CloudCodeModuleReference>
    {
        public override string Name => L10n.Tr("Deploy");

        readonly IDotnetRunner m_DotnetRunner;
        readonly IFileSystem m_FileSystem;
        readonly IModuleZipper m_ModuleZipper;

        readonly EditorCloudCodeModuleDeploymentHandler m_EditorCloudCodeDeploymentHandler;
        readonly bool m_Reconcile;
        readonly bool m_DryRun;

        public CloudCodeModuleDeployCommand(
            IDotnetRunner dotnetRunner,
            IFileSystem fileSystem,
            IModuleZipper moduleZipper,
            EditorCloudCodeModuleDeploymentHandler editorCloudCodeDeploymentHandler)
        {
            m_DotnetRunner = dotnetRunner;
            m_FileSystem = fileSystem;
            m_ModuleZipper = moduleZipper;

            m_EditorCloudCodeDeploymentHandler = editorCloudCodeDeploymentHandler;
            m_Reconcile = false;
            m_DryRun = false;
        }

        public override async Task ExecuteAsync(IEnumerable<CloudCodeModuleReference> items, CancellationToken cancellationToken = new CancellationToken())
        {
            var cloudCodeModuleReferences = items.ToList();
            OnDeploy(cloudCodeModuleReferences);
            var compiled = await Compile(cloudCodeModuleReferences, cancellationToken);
            m_EditorCloudCodeDeploymentHandler.SetReferenceFiles(cloudCodeModuleReferences.ToList());
            await m_EditorCloudCodeDeploymentHandler.DeployAsync(compiled, m_Reconcile, m_DryRun);
        }

        static void OnDeploy(IEnumerable<CloudCodeModuleReference> items)
        {
            items.ForEach(i =>
            {
                i.Progress = 0f;
                i.Status = new DeploymentStatus();
                i.States.Clear();
            });
        }

        async Task<List<IScript>> Compile(IEnumerable<CloudCodeModuleReference> items, CancellationToken cancellationToken = default)
        {
            var generationList = new List<IScript>();
            foreach (var ccmr in items)
            {
                var ccmrDir = m_FileSystem.GetDirectoryName(m_FileSystem.GetFullPath(ccmr.Path));
                var targetPath = m_FileSystem.Combine(ccmrDir, ccmr.ModulePath);
                targetPath = m_FileSystem.GetFullPath(targetPath);

                try
                {
                    var outputPath = m_FileSystem.Combine(m_FileSystem.GetDirectoryName(targetPath), "module-compilation");
                    await m_DotnetRunner.ExecuteDotnetAsync(new[] { $"publish \"{targetPath}\" -c Release -r linux-x64 " +
                        $"-o \"{outputPath}\" -p:AssemblyName={ccmr.name}" }, cancellationToken);
                    UpdateStatus(ccmr, 33f, "Compiled Successfully");

                    var createdFilePath = await m_ModuleZipper.ZipCompilation(targetPath, ccmr.Name, cancellationToken);
                    UpdateStatus(ccmr, 66f, "Zipped Successfully");

                    var name = new ScriptName(m_FileSystem.GetFileNameWithoutExtension(ccmr.Name));
                    var script = new Script(createdFilePath)
                    {
                        Name = name,
                        Body = string.Empty,
                        Parameters = new List<CloudCodeParameter>(),
                        Language = Language.CS
                    };
                    generationList.Add(script);
                }
                catch (Exception e)
                {
                    ccmr.Status = new DeploymentStatus("Failed to compile", e.Message, SeverityLevel.Error);
                }
            }

            return generationList;
        }

        static void UpdateStatus(CloudCodeModuleReference item, float progress, string statusMessage)
        {
            item.Progress = progress;
            item.Status = new DeploymentStatus(statusMessage);
        }
    }
}
