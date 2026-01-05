using Unity.Services.CloudCode.Authoring.Editor.Core.Model;
using Unity.Services.CloudCode.Editor.Shared.Assets;
using UnityEngine;

namespace Unity.Services.CloudCode.Authoring.Editor.Scripts
{
    [HelpURL("https://docs.unity3d.com/Packages/com.unity.services.cloudcode@2.10/manual/Authoring/cloud_code_scripts.html"),
     Icon("Packages/com.unity.services.cloudcode/Editor/Authoring/Scripts/UI/Assets/icon.png")]
    class CloudCodeScript : ScriptableObject, IPath
    {
        [SerializeField]
        Script m_Model;

        public Script Model { get => m_Model; internal set => m_Model = value; }
        public string Path { get => Model.Path; set => Model.Path = value; }
    }
}
