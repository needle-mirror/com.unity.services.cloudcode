using System;
using System.Collections.ObjectModel;
using Unity.Services.CloudCode.Authoring.Client;
using Unity.Services.CloudCode.Authoring.Client.Apis.Default;
using Unity.Services.CloudCode.Authoring.Client.ErrorMitigation;
using Unity.Services.CloudCode.Authoring.Client.Http;
using Unity.Services.CloudCode.Authoring.Editor.AdminApi;
using Unity.Services.CloudCode.Authoring.Editor.AdminApi.Readers;
using Unity.Services.CloudCode.Authoring.Editor.Analytics;
using Unity.Services.CloudCode.Authoring.Editor.Analytics.Deployment;
using Unity.Services.CloudCode.Authoring.Editor.Bundling;
using Unity.Services.CloudCode.Authoring.Editor.Core.Analytics;
using Unity.Services.CloudCode.Authoring.Editor.Core.Bundling;
using Unity.Services.CloudCode.Authoring.Editor.Core.Deployment;
using Unity.Services.CloudCode.Authoring.Editor.Deployment;
using Unity.Services.CloudCode.Authoring.Editor.IO;
using Unity.Services.CloudCode.Authoring.Editor.Package;
using Unity.Services.CloudCode.Authoring.Editor.Parameters;
using Unity.Services.CloudCode.Authoring.Editor.Projects;
using Unity.Services.CloudCode.Authoring.Editor.Projects.UI;
using Unity.Services.CloudCode.Authoring.Editor.Shared.DependencyInversion;
using Unity.Services.CloudCode.Authoring.Editor.Scripts;
using Unity.Services.CloudCode.Authoring.Editor.Scripts.Validation;
using Unity.Services.CloudCode.Authoring.Editor.Shared.Analytics;
using Unity.Services.CloudCode.Authoring.Editor.Shared.Assets;
using Unity.Services.CloudCode.Authoring.Editor.Shared.UI;
using Unity.Services.CloudCode.Authoring.Editor.UI;
using Unity.Services.Core.Editor;
using Unity.Services.DeploymentApi.Editor;
using UnityEditor;
using UnityEngine;
using static Unity.Services.CloudCode.Authoring.Editor.Shared.DependencyInversion.Factories;
using IDeploymentEnvironmentProvider = Unity.Services.DeploymentApi.Editor.IEnvironmentProvider;
using ICoreLogger = Unity.Services.CloudCode.Authoring.Editor.Core.Logging.ILogger;
using IEnvironmentProvider = Unity.Services.CloudCode.Authoring.Editor.Core.Deployment.IEnvironmentProvider;
using Logger = Unity.Services.CloudCode.Authoring.Editor.Logging.Logger;

namespace Unity.Services.CloudCode.Authoring.Editor
{
    class CloudCodeAuthoringServices : AbstractRuntimeServices<CloudCodeAuthoringServices>
    {
        [InitializeOnLoadMethod]
        static void Initialize()
        {
            Instance.Initialize(new ServiceCollection());
            var deploymentItemProvider = Instance.GetService<DeploymentProvider>();
            ((CloudCodeDeploymentProvider)deploymentItemProvider).ValidateDeploymentStatus();
            Deployments.Instance.DeploymentProviders.Add(deploymentItemProvider);
        }

        internal override void Register(ServiceCollection collection)
        {
            collection.Register(_ => CloudCodePreferences.LoadProjectSettings());
            collection.Register(_ => new Func<IProjectSettings>(CloudCodePreferences.LoadProjectSettings));

            collection.Register(_ => Debug.unityLogger);

            collection.Register(Default<IProcessRunner, ProcessRunner>);
            collection.Register(Default<INodeJsRunner, NodePackageManager>);
            collection.Register(Default<INpmScriptRunner, NodePackageManager>);
            collection.Register(Default<INodePackageManager, NodePackageManager>);
            collection.Register(Default<IPackageVersionProvider, PackageVersionProvider>);
            collection.Register(Default<NodePackageManager>);

            collection.Register(Default<IInScriptParameters, InScriptParameters>);
            collection.Register(Default<ObservableCollection<IDeploymentItem>, ObservableCloudCodeScripts>);
            collection.RegisterStartupSingleton(Default<DuplicateNameValidator>);

            collection.Register(Default<IAccessTokens, AccessTokens>);

            collection.Register(Default<IScriptReader, ScriptReader>);

            collection.Register(Default<INotifications, Notifications>);

            collection.RegisterSingleton(Default<IDeploymentAnalytics, DeploymentAnalytics>);
            collection.Register(Default<ICommonAnalytics, CommonAnalytics>);
            collection.Register(Default<CloudScriptCreationAnalytics>);

            collection.Register(Default<EditorCloudCodeDeploymentHandler>);
            collection.Register(Default<DeployCommand>);
            collection.Register(Default<OpenCommand>);

            collection.Register(Default<JsAssetHandler>);
            collection.Register(Default<IExternalCodeEditor, ExternalCodeEditor>);
            collection.Register(Default<ICoreLogger, Logger>);

            collection.RegisterStartupSingleton(Default<DeploymentProvider, CloudCodeDeploymentProvider>);

            collection.Register(_ => new Configuration(null, null, null, null));
            collection.Register(Default<IRetryPolicyProvider, RetryPolicyProvider>);
            collection.Register(Default<IHttpClient, HttpClient>);
            collection.Register(Default<IDefaultApiClient, DefaultApiClient>);
            collection.Register(Default<IPreDeployValidator, EditorPreDeployValidator>);
            collection.Register(Default<ICloudCodeClient, CloudCodeClient>);
            collection.Register(Default<IFileReader, FileReader>);

            collection.Register(Default<IEnvironmentProvider, EnvironmentProvider>);
            collection.Register(Default<IProjectIdProvider, ProjectIdProvider>);
            collection.Register(_ => new Lazy<IDeploymentEnvironmentProvider>(() => Deployments.Instance.EnvironmentProvider));

            collection.Register(Default<IScriptBundler, EditorScriptBundler>);
            collection.RegisterSingleton(Default<AssetPostprocessorProxy>);
            collection.RegisterSingleton(Default<IScriptModifiedTracker, ScriptModifiedTracker>);

            collection.Register(Default<IEditorGUIUtils, EditorGUIUtils>);
            collection.Register(Default<InScriptParamsUIHandler>);
        }
    }
}
