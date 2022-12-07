// WARNING: Auto generated code. Modifications will be lost!
using System;

namespace Unity.Services.CloudCode.Authoring.Editor.Shared.Clients
{
    interface ICurrentTime
    {
        DateTime Now { get; }
    }

    class CurrentTime : ICurrentTime
    {
        public DateTime Now => DateTime.Now;
    }
}
