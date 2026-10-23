using System;
using System.Linq;
using Unity.Services.CloudCode.Authoring.Editor.Core.Debugger.Exceptions;
using Unity.Services.CloudCode.Authoring.Editor.Core.Deployment.ModuleGeneration.Exceptions;
using Unity.Services.CloudCode.Authoring.Editor.Core.Modules.Exceptions;

namespace Unity.Services.CloudCode.Authoring.Editor.Core.Analytics
{
    /// <summary>Codes for the optional <c>error</c> field on Cloud Code analytics events.</summary>
    static class AnalyticsErrorCode
    {
        public const string k_Unknown = "unknown";
        public const string k_CompilationFailed = "compilation_failed";
        public const string k_UnsupportedApiCompatibilityLevel = "unsupported_api_compatibility_level";
        public const string k_InvalidSolutionName = "invalid_solution_name";
        public const string k_SolutionAlreadyExists = "solution_already_exists";
        public const string k_AdminApiError = "admin_api_error";
        public const string k_PortInUse = "port_in_use";
        public const string k_AssemblyNameConflict = "assembly_name_conflict";
        public const string k_ModuleAlreadyExists = "module_already_exists";
        public const string k_AssemblyMissingReferences = "assembly_missing_references";
        public const string k_PermissionDenied = "permission_denied";

        /// <summary>Null exception means no error. An unrecognised one falls back to unknown.</summary>
        public static string FromException(Exception exception)
        {
            switch (Unwrap(exception))
            {
                case null:
                    return null;
                case InvalidSolutionNameException _:
                    return k_InvalidSolutionName;
                case SolutionAlreadyExistsException _:
                    return k_SolutionAlreadyExists;
                case AssemblyNameConflictException _:
                    return k_AssemblyNameConflict;
                case ModuleAlreadyExistsException _:
                    return k_ModuleAlreadyExists;
                case AssemblyMissingReferencesException _:
                    return k_AssemblyMissingReferences;
                case PortInUseException _:
                    return k_PortInUse;
                // Not IOException: that also covers a missing template, which is a broken install.
                case UnauthorizedAccessException _:
                    return k_PermissionDenied;
                default:
                    return k_Unknown;
            }
        }

        // A batch reports its faults as an AggregateException, which GetBaseException only unwraps
        // when there is exactly one of them. Shared with AnalyticsErrorData so the code and the
        // data always describe the same exception.
        internal static Exception Unwrap(Exception exception)
        {
            if (!(exception is AggregateException aggregate))
                return exception?.GetBaseException();

            return aggregate.Flatten().InnerExceptions.FirstOrDefault()?.GetBaseException() ?? aggregate;
        }
    }
}
