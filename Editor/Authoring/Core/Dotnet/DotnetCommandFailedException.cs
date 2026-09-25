using System;

namespace Unity.Services.CloudCode.Authoring.Editor.Core.Dotnet
{
    class DotnetCommandFailedException : Exception
    {
        public bool DiagnosticsReported { get; }

        public DotnetCommandFailedException(string message, bool diagnosticsReported = false)
            : base(message)
        {
            DiagnosticsReported = diagnosticsReported;
        }
    }
}
