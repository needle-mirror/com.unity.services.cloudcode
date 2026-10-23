using System;

namespace Unity.Services.CloudCode.Authoring.Editor.Debugger
{
    static class EnvironmentIds
    {
        // IEnvironmentsApi lifts a plain Guid, so an unset environment reads as Guid.Empty, never null.
        internal static bool IsSet(Guid? environmentId)
        {
            return environmentId.HasValue && environmentId.Value != Guid.Empty;
        }
    }
}
