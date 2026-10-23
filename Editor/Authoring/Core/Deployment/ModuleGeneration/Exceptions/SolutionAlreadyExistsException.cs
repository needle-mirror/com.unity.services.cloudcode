using System;

namespace Unity.Services.CloudCode.Authoring.Editor.Core.Deployment.ModuleGeneration.Exceptions
{
    class SolutionAlreadyExistsException : Exception
    {
        public SolutionAlreadyExistsException(string message)
            : base(message)
        {
        }
    }
}
