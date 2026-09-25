using System;

namespace Unity.Services.CloudCode.Authoring.Editor.Core.Modules.Exceptions
{
    class ModuleAlreadyExistsException : Exception
    {
        public ModuleAlreadyExistsException(string message)
            : base(message)
        {
        }
    }
}
