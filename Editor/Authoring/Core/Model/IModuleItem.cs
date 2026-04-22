using System;
using System.Collections.Generic;
using System.Text;
using Unity.Services.DeploymentApi.Editor;

namespace Unity.Services.CloudCode.Authoring.Editor.Core.Model
{
    /// <summary>
    /// IModuleItem serves as the base interface for deployable Cloud Code modules and is referenced
    /// by deployment handlers. It extends IDeploymentItem to integrate with the deployment window while
    /// expanding on progression and status logs unique to all modules.
    /// </summary>
    interface IModuleItem : IDeploymentItem, ITypedItem
    {
        // Status Log as viewed in the Deployment Window.
        List<(DateTime, DeploymentStatus)> StatusLog { get; }

        // Progression as viewed in the Deployment Window.
        new float Progress { get; set; }
    }

    /// <summary>
    /// ISolutionModuleItem extends IModuleItem to include solution specific properties that are
    /// specifically required by Cloud Code Module References (referenced modules).
    /// </summary>
    interface ISolutionModuleItem : IModuleItem
    {
        // Name of the Module as set by the Module Builder, for example: "module.ccm"
        string ModuleName { get; set; }

        // Absolute path to the external C# Solution representing this module item.
        string SolutionPath { get; }

        // Absolute path to the zipped, compiled output as set by the Module Builder.
        string CcmPath { get; set; }
    }

    interface INativeModuleItem : IModuleItem
    {
        // Absolute path to the output assembly
        string AssemblyPath { get; }
    }

    static class IModuleItemExtensions
    {
        public static void UpdateLogStatus(this IModuleItem self, DeploymentStatus status)
        {
            self.StatusLog.Add((DateTime.Now, status));
            var builder = new StringBuilder();
            var statusLevel = SeverityLevel.Info;
            foreach (var(t, s) in self.StatusLog)
            {
                string detail = string.IsNullOrEmpty(s.MessageDetail) ? string.Empty : "- " + s.MessageDetail;
                builder.Append($"[{t:HH:mm:ss.fff}] {s.Message}{detail}{Environment.NewLine}");
                if (statusLevel < s.MessageSeverity)
                    statusLevel = s.MessageSeverity;
            }

            self.Status = new DeploymentStatus(status.Message, builder.ToString(), statusLevel);
        }

        public static void ClearLogStatus(this IModuleItem self)
        {
            self.StatusLog.Clear();
            self.Status = DeploymentStatus.Empty;
        }
    }
}
