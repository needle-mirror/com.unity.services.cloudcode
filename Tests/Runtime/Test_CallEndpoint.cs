using System;
using System.Collections;
using System.Threading.Tasks;
using NUnit.Framework;
using Unity.Services.Authentication;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.Services.CloudCode.Tests
{
    public class Test_CallEndpoint
    {
        [InitializeOnLoadMethod]
        static void SetUpCondition()
        {
            ConditionalIgnoreAttribute.AddConditionalIgnoreMapping("IgnoreBatchMode", Application.isBatchMode);
        }
        
        [Serializable]
        class Remote
        {
            public int hello;
        }

        [Serializable]
        class Request
        {
            public Remote loopback;
        }

        [Serializable]
        class RequestInt
        {
            public int val;
        }
        
        private SerializedObject _projectSettingsObject;
        private SerializedProperty _cloudProjectIdProperty;
        private SerializedProperty _cloudProjectNameProperty;
        private SerializedProperty _versionProperty;
        private string _previousProjectId;
        private string _previousProjectName;
        private string _previousVersion;
        
        private void FixYamatoProjectSettings()
        {
            // this needs to only exist in editor
            // yamato runs tests with an editor context, but doesn't pull in the project id/etc for some reason
            // this block fixes that, but uses Editor functionality which causes issues on device builds
            
            #if UNITY_EDITOR
            
            // Cloud Project ID needs to be linked or the SDK will fail to start.
            // Since this cannot be set in Yamato's transient test projects, we need to do a little hackery...
            const string ProjectSettingsAssetPath = "ProjectSettings/ProjectSettings.asset";
            _projectSettingsObject = new SerializedObject(UnityEditor.AssetDatabase.LoadAllAssetsAtPath(ProjectSettingsAssetPath)[0]);  
            _cloudProjectIdProperty = _projectSettingsObject.FindProperty("cloudProjectId");
            _cloudProjectNameProperty = _projectSettingsObject.FindProperty("projectName");
            _versionProperty = _projectSettingsObject.FindProperty("bundleVersion"); // NOTE: this is Project Settings -> Player -> Version

            _previousProjectId = _cloudProjectIdProperty.stringValue;
            _previousProjectName = _cloudProjectNameProperty.stringValue;
            _previousVersion = _versionProperty.stringValue;
            _cloudProjectIdProperty.stringValue = "3c901dde-39fc-46a5-b731-43153a7c14ca";  // TODO: re-project Catapult and update this
            _cloudProjectNameProperty.stringValue = "cloud code test";
            _versionProperty.stringValue = "1.3.3.7";
            _projectSettingsObject.ApplyModifiedProperties();
            
            #endif
        }

        [UnitySetUp]
        public IEnumerator SetupCR()
        {
            FixYamatoProjectSettings();
            
            bool done = false;
            var coreInit = Core.UnityServices.InitializeAsync().ContinueWith(x =>
            {
                done = true;
            });

            while (!done)
            {
                yield return null;
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                var a = AuthenticationService.Instance.SignInAnonymouslyAsync();

                while (AuthenticationService.Instance.AccessToken == null)
                {
                    yield return null;
                }
            }
        }
        
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _cloudProjectIdProperty.stringValue = _previousProjectId;
            _cloudProjectNameProperty.stringValue = _previousProjectName;
            _versionProperty.stringValue = _previousVersion;
            _projectSettingsObject.ApplyModifiedProperties();

            yield return null;
        }
        

        [UnityTest]
        [ConditionalIgnore("IgnoreBatchMode", "Fails on Yamato due to auth issue")]
        public IEnumerator TestCallEndpoint()
        {
            var foo = new Remote() { hello = 42 };
            var req = new Request() { loopback = foo };

            var res = CloudCode.CallEndpointAsync<Remote>("hi", req);

            while (!res.IsCompleted)
            {
                yield return null;
            }

            Assert.AreEqual(42, res.Result.hello);
            
            var stringRes = CloudCode.CallEndpointAsync("hi", req);
            while (!stringRes.IsCompleted)
            {
                yield return null;
            }

            var remote = JsonUtility.FromJson<Remote>(stringRes.Result);
            Assert.AreEqual(res.Result.hello, remote.hello);
        }

        [UnityTest]
        [ConditionalIgnore("IgnoreBatchMode", "Fails on Yamato due to auth issue")]
        public IEnumerator TestCallEndpoint_Int()
        {
            var req = new RequestInt() { val = 42 };

            var res = CloudCode.CallEndpointAsync<bool>("test_int", req);

            while (!res.IsCompleted)
            {
                yield return null;
            }

            Assert.AreEqual(true, res.Result);
            
            var stringRes = CloudCode.CallEndpointAsync("test_int", req);
            while (!stringRes.IsCompleted)
            {
                yield return null;
            }

            Assert.AreEqual(res.Result.ToString(), stringRes.Result);
        }
        
        [UnityTest]
        public IEnumerator NullTest()
        {
            yield return null;
            Assert.True(true);
        }
    }
}
