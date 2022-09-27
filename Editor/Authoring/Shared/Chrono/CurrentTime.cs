// WARNING: Auto generated code by Starbuck2. Modifications will be lost!
using System;

namespace Unity.Services.CloudCode.Authoring.Editor.Shared.Chrono
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
