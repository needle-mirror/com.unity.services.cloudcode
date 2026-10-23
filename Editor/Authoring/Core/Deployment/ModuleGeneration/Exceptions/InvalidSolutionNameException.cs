using System;

namespace Unity.Services.CloudCode.Authoring.Editor.Core.Deployment.ModuleGeneration.Exceptions
{
    class InvalidSolutionNameException : Exception
    {
        public InvalidSolutionNameException(string message)
            : base(message)
        {
        }
    }
}
