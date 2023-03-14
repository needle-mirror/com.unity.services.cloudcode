using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Authoring.Editor.Core.Bundling;
using Unity.Services.CloudCode.Authoring.Editor.Core.Logging;
using Unity.Services.CloudCode.Authoring.Editor.Projects;

namespace Unity.Services.CloudCode.Authoring.Editor.Bundling
{
    class EditorScriptBundler : IScriptBundler
    {
        readonly INodeJsRunner m_ScriptRunner;
        readonly ILogger m_Logger;

        public EditorScriptBundler(INodeJsRunner scriptRunner, ILogger logger)
        {
            m_ScriptRunner = scriptRunner;
            m_Logger = logger;
        }

        public async Task<bool> ShouldBeBundled(string filePath, CancellationToken cancellationToken)
        {
            var fullScriptPath = Path.GetFullPath(ScriptPaths.ScriptShouldBundle);
            var output = await m_ScriptRunner.ExecNodeJs(
                new[] { fullScriptPath, filePath },
                null,
                cancellationToken);
            output = output.Trim();

            bool.TryParse(output, out var shouldBundle);
            return shouldBundle;
        }

        public async Task<string> Bundle(string filePath, CancellationToken cancellationToken)
        {
            var bundlerPath = Path.GetFullPath("Packages/com.unity.services.cloudcode/Editor/Authoring/Core/Bundling/Assets~/shim.cjs");
            var scriptPath = Path.GetFullPath(filePath);
            try
            {
                return await m_ScriptRunner.ExecNodeJs(
                    new List<string> { bundlerPath, scriptPath },
                    cancellationToken: cancellationToken);
            }
            catch (Exception e)
            {
                m_Logger.LogError(e.Message);
                throw;
            }
        }
    }
}
