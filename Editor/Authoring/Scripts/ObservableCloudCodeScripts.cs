using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Unity.Services.CloudCode.Authoring.Editor.Shared.Assets;
using Unity.Services.DeploymentApi.Editor;
using UnityEngine;

namespace Unity.Services.CloudCode.Authoring.Editor.Scripts
{
    sealed class ObservableCloudCodeScripts : ObservableCollection<IDeploymentItem>, IDisposable
    {
        readonly ObservableAssets<CloudCodeScript> m_CloudCodeScripts;

        public ObservableCloudCodeScripts()
        {
            m_CloudCodeScripts = new ObservableAssets<CloudCodeScript>();
            foreach (var asset in m_CloudCodeScripts)
            {
                Add(asset.Model);
            }
            m_CloudCodeScripts.CollectionChanged += CloudCodeScriptsOnCollectionChanged;
        }

        public void Dispose()
        {
            m_CloudCodeScripts.CollectionChanged -= CloudCodeScriptsOnCollectionChanged;
        }

        void CloudCodeScriptsOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (var oldItem in e.OldItems.Cast<CloudCodeScript>())
                {
                    Remove(oldItem.Model);
                }
            }

            if (e.NewItems != null)
            {
                foreach (var newItem in e.NewItems.Cast<CloudCodeScript>())
                {
                    Add(newItem.Model);
                }
            }
        }

        CloudCodeScript RegenAsset(CloudCodeScript asset)
        {
            var newAsset = ScriptableObject.CreateInstance<CloudCodeScript>();
            newAsset.Model = asset.Model;
            asset = newAsset;
            return asset;
        }

        public CloudCodeScript GetOrCreateInstance(string ctxAssetPath)
        {
            foreach (var a in m_CloudCodeScripts)
            {
                if (ctxAssetPath == a.Path)
                {
                    return a == null ? RegenAsset(a) : a;
                }
            }

            var asset = ScriptableObject.CreateInstance<CloudCodeScript>();
            asset.Model = new Script(ctxAssetPath);
            return asset;
        }
    }
}
