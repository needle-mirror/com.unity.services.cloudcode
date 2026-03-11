using System;
using System.Collections.Specialized;
using System.Linq;
using Unity.Services.CloudCode.Authoring.Editor.Deployment.Modules;
using Unity.Services.CloudCode.Authoring.Editor.Modules;
using Unity.Services.CloudCode.Editor.Shared.Infrastructure.Collections;
using Unity.Services.DeploymentApi.Editor;

namespace Unity.Services.CloudCode.Authoring.Editor.Deployment
{
    class NativeModuleDeploymentProvider : DeploymentProvider
    {
        public override string Service => "Cloud Code";
        public override Command DeployCommand { get; }

        public NativeModuleDeploymentProvider(
            NativeModuleDeployCommand deployCommand,
            NativeModuleGenerateBindingsCommand generateBindingsCommand,
            OpenModuleDashboardCommand openModuleDashboardCommand,
            NativeModuleReferenceCollection scripts)
        {
            DeployCommand = deployCommand;
            Commands.Add(generateBindingsCommand);
            Commands.Add(openModuleDashboardCommand);

            foreach (var script in scripts)
            {
                DeploymentItems.Add(script);
            }

            scripts.CollectionChanged += OnCollectionChanged;
        }

        void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs args)
        {
            var oldItems = args.OldItems?.Cast<NativeModuleReference>() ?? Array.Empty<NativeModuleReference>();
            var newItems = args.NewItems?.Cast<NativeModuleReference>() ?? Array.Empty<NativeModuleReference>();

            oldItems.ForEach(asset => DeploymentItems.Remove(asset));
            newItems.ForEach(asset => DeploymentItems.Add(asset));
        }
    }
}
