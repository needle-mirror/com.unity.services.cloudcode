// WARNING: Auto generated code by Starbuck2. Modifications will be lost!
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Unity.Services.CloudCode.Authoring.Editor.Shared.Collections;
using UnityEditor;

namespace Unity.Services.CloudCode.Authoring.Editor.Shared.Assets
{
    sealed class ObservableAssets<T> : ObservableCollection<T>, IDisposable where T : UnityEngine.Object, IPath
    {
        readonly AssetPostprocessorProxy m_Postprocessor = new AssetPostprocessorProxy();
        readonly Dictionary<string, T> m_AssetPaths = new Dictionary<string, T>();

        public ObservableAssets()
        {
            m_Postprocessor.AllAssetsPostprocessed += AllAssetsPostprocessed;
            LoadAllAssets();
        }

        public void Dispose()
        {
            m_Postprocessor.AllAssetsPostprocessed -= AllAssetsPostprocessed;
        }

        void LoadAllAssets()
        {
            var assetPaths = AssetDatabase
                .FindAssets($"t:{typeof(T).Name}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !string.IsNullOrEmpty(path));
            foreach (var assetPath in assetPaths)
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);

                if (asset == null)
                {
                    continue;
                }

                AddForPath(assetPath, asset);
            }
        }

        void AllAssetsPostprocessed(object sender, PostProcessEventArgs args)
        {
            foreach (var imported in args.ImportedAssetPaths)
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(imported);
                if (asset != null && !Contains(asset))
                {
                    AddForPath(imported, asset);
                }
            }

            args.DeletedAssetPaths
                .Where(m_AssetPaths.ContainsKey)
                .ForEach(d => RemoveForPath(d, m_AssetPaths[d]));

            foreach (var(movedToPath, movedFromPath) in args.MovedAssetPaths.Select((a, i) => (a, args.MovedFromAssetPaths[i])))
            {
                if (m_AssetPaths.ContainsKey(movedFromPath))
                {
                    MovePath(movedToPath, movedFromPath);
                }
            }
        }

        void AddForPath(string path, T asset)
        {
            m_AssetPaths.Add(path, asset);
            m_AssetPaths[path].Path = path;
            Add(asset);
        }

        void RemoveForPath(string path, T asset)
        {
            m_AssetPaths.Remove(path);
            Remove(asset);
        }

        void MovePath(string toPath, string fromPath)
        {
            if (toPath != fromPath)
            {
                m_AssetPaths[toPath] = m_AssetPaths[fromPath];
                m_AssetPaths[toPath].Path = toPath;
                m_AssetPaths.Remove(fromPath);
            }
        }
    }
}
