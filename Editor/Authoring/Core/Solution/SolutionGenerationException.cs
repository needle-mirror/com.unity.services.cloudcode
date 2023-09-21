using System;

namespace Unity.Services.CloudCode.Authoring.Editor.Core.Solution
{
    public class SolutionGenerationException : Exception
    {
        public SolutionGenerationException(string message)
            : base(message)
        {
        }
    }
}
