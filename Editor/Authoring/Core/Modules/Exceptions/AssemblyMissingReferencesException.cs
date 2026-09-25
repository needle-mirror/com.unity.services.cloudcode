using System;

namespace Unity.Services.CloudCode.Authoring.Editor.Core.Modules.Exceptions
{
    class AssemblyMissingReferencesException : Exception
    {
        public AssemblyMissingReferencesException(string message)
            : base(message)
        {
        }
    }
}
