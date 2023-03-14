using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Authoring.Editor.AdminApi;
using Unity.Services.CloudCode.Authoring.Editor.Core.Deployment;
using Unity.Services.CloudCode.Authoring.Editor.Core.Model;
using Unity.Services.CloudCode.Authoring.Editor.Projects;
using Unity.Services.CloudCode.Authoring.Editor.Scripts;
using Unity.Services.CloudCode.Authoring.Editor.UI;
using UnityEditor;
using UnityEditor.AssetImporters;

namespace Unity.Services.CloudCode.Authoring.Editor.Parameters.UI
{
    [CustomEditor(typeof(CloudCodeScriptImporter))]
    class CloudCodeParameterImporterEditor : ScriptedImporterEditor
    {
        static readonly string k_BreakingChangeWarning = L10n.Tr("The script parameters are incompatible with the parameters currently deployed. Deploying this version will break all existing clients.");

        ICloudCodeClient m_Client;
        InScriptParamsUIHandler m_UIHandler;

        Task m_LoadScriptTask;
        Script m_RemoteScript;

        // New needed in order for Unity to call the top level OnEnable
        public new void OnEnable()
        {
            base.OnEnable();
            CloudCodeAuthoringServices.Instance.InitializeInstance(this);
            m_UIHandler = CloudCodeAuthoringServices.Instance.GetService<InScriptParamsUIHandler>();
        }

        public void Initialize(ICloudCodeClient client)
        {
            m_Client = client;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.UpdateIfRequiredOrScript();

            var sourceProperty = serializedObject.FindProperty(nameof(CloudCodeScriptImporter.Source));
            var parameters = serializedObject.FindProperty(nameof(CloudCodeScriptImporter.Parameters));


            var source =  (ParameterSource)sourceProperty.enumValueIndex;
            var isProjectInitialized = CloudCodeProject.IsInitialized();

            m_UIHandler.Handle(parameters, source, isProjectInitialized);

            CheckForBreakingChanges();

            serializedObject.ApplyModifiedProperties();
            ApplyRevertGUI();
        }

        void CheckForBreakingChanges()
        {
            var path = AssetDatabase.GetAssetPath(serializedObject.targetObject);

            if (m_RemoteScript == null)
            {
                if (m_LoadScriptTask == null || (m_LoadScriptTask.IsCompleted && !m_LoadScriptTask.IsCompletedSuccessfully))
                {
                    m_LoadScriptTask = LoadRemoteScript(ScriptName.FromPath(path));
                }
            }
            else
            {
                var remoteParameters = m_RemoteScript.Parameters.ToDictionary(p => p.Name, p => p);
                var parameters = ((CloudCodeScriptImporter)serializedObject.targetObject).Parameters;

                var requiredParameters = parameters.Where(p => p.Required);

                var newRequiredParameters = requiredParameters
                    .Where(p => !remoteParameters.ContainsKey(p.Name) || !remoteParameters[p.Name].Required);
                var parameterTypeChanges = parameters
                    .Where(p => p.ParameterType != ParameterType.Any)
                    .Where(p => remoteParameters.ContainsKey(p.Name) && remoteParameters[p.Name].ParameterType != p.ParameterType);

                if (newRequiredParameters.Any() || parameterTypeChanges.Any())
                {
                    EditorGUILayout.HelpBox(k_BreakingChangeWarning, MessageType.Warning);
                }
            }
        }

        async Task LoadRemoteScript(ScriptName scriptName)
        {
            try
            {
                m_RemoteScript = (Script)await m_Client.Get(scriptName);
            }
            catch (UnexpectedRemoteStatusCodeException e) when (e.StatusCode == HttpStatusCode.NotFound)
            {
                m_RemoteScript = null;
            }
            Repaint();
        }
    }
}
