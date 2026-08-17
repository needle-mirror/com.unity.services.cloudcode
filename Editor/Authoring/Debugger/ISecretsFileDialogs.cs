#if UNITY_6000_3_OR_NEWER
using UnityEngine;

namespace Unity.Services.CloudCode.Authoring.Editor.Debugger
{
    interface ISecretsFileDialogs
    {
        void ShowInvalidFileType();

        bool ShowInvalidJson();

        void OpenInIde(TextAsset asset);
    }
}
#endif
