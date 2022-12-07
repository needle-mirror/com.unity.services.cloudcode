using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Authoring.Editor.Core.Logging;
using Unity.Services.CloudCode.Authoring.Editor.Core.Model;

namespace Unity.Services.CloudCode.Authoring.Editor.Core.Deployment
{
    class PreDeployValidator : IPreDeployValidator
    {
        readonly ILogger m_Logger;
        protected internal string DuplicateNameConsoleError = "Cannot deploy cloud code scripts with the same name.";

        public PreDeployValidator(ILogger logger)
        {
            m_Logger = logger;
        }

        /// <summary>
        /// Returns scripts that pass predeploy validation
        /// </summary>
        /// <param name="scripts">Scripts to be deployed</param>
        /// <returns>The validation information</returns>
        public virtual Task<ValidationInfo> Validate(IReadOnlyList<IScript> scripts)
        {
            var validScripts = RemoveDuplicateScripts(scripts, out var duplicateScripts);
            var invalidScriptsDictionary = new ReadOnlyDictionary<IScript, Exception>(
                duplicateScripts
                    .ToDictionary(invalidScript =>
                    invalidScript,
                    invalidScript => new Exception($"Multiple scripts with the name {invalidScript.Name} were found. Only a single script for a given name may be deployed at the same time. Give all scripts unique names or deploy them separately to proceed.")));

            return Task.FromResult<ValidationInfo>(new ValidationInfo(validScripts, invalidScriptsDictionary));
        }

        List<IScript> RemoveDuplicateScripts(IReadOnlyList<IScript> scripts, out IReadOnlyList<IScript> duplicateScripts)
        {
            duplicateScripts = scripts.GroupBy(s => s.Name)
                .Where(grouping => grouping.Count() > 1)
                .Select(grouping => grouping.ToList())
                .SelectMany(s => s)
                .ToList();

            if (duplicateScripts.Count > 0)
            {
                NotifyDuplicateScriptError(scripts, duplicateScripts);
            }

            return scripts.Except(duplicateScripts).ToList();
        }

        protected virtual void NotifyDuplicateScriptError(
            IReadOnlyList<IScript> scripts,
            IReadOnlyList<IScript> duplicateScripts)
        {
            m_Logger.LogError(DuplicateNameConsoleError);
        }
    }
}
