using System;

namespace Unity.Services.CloudCode.Authoring.Editor.Core.Modules.Exceptions
{
    class AssemblyNameConflictException : Exception
    {
        public AssemblyNameConflictException(string message)
            : base(message)
        {
        }
    }
}
