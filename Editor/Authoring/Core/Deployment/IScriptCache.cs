using System.Collections.Generic;
using Unity.Services.CloudCode.Authoring.Editor.Core.Model;

namespace Unity.Services.CloudCode.Authoring.Editor.Core.Deployment
{
    interface IScriptCache
    {
        bool HasItemChanged(IScript script);
        void Cache(IReadOnlyList<IScript> scripts);
    }
}
