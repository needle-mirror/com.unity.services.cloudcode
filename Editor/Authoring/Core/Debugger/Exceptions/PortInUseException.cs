using System;

namespace Unity.Services.CloudCode.Authoring.Editor.Core.Debugger.Exceptions
{
    class PortInUseException : Exception
    {
        public PortInUseException(string message)
            : base(message)
        {
        }
    }
}
