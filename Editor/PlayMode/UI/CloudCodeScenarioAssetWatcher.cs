using System;
using System.Collections.Generic;
using UnityEditor;

namespace Unity.Services.CloudCode.Editor.PlayMode
{
    // A deleted asset can no longer be loaded to check its type, so listeners get the raw paths and
    // compare them with the assets they depend on.
    class CloudCodeScenarioAssetWatcher : AssetPostprocessor
    {
        internal static event Action<IReadOnlyCollection<string>> Changed;

        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] movedTo, string[] movedFrom)
        {
            if (Changed == null)
                return;

            var paths = new HashSet<string>(imported);
            paths.UnionWith(deleted);
            paths.UnionWith(movedTo);
            paths.UnionWith(movedFrom);
            Changed(paths);
        }
    }
}
