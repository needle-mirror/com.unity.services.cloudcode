#if UNITY_6000_3_OR_NEWER

using UnityEditor.Toolbars;
using UnityEngine;

namespace Unity.Services.CloudCode.Authoring.Editor.Modules.UI
{
    internal static class CloudCodeLocalToolbarBootstrap
    {
        static CloudCodeLocalToolbarController m_ToolbarController;
        const string k_ToolbarPath = "Services/Cloud Code";

        [MainToolbarElement(k_ToolbarPath, defaultDockPosition = MainToolbarDockPosition.Right)]
        public static MainToolbarElement CreateToolbar()
        {
            if (m_ToolbarController == null)
            {
                m_ToolbarController = new CloudCodeLocalToolbarController();
                m_ToolbarController.OnToolbarInvalidated += RebuildToolbar;
            }

            return new MainToolbarDropdown(m_ToolbarController.GetMainToolbarContent(), OpenToolbarPopup);
        }

        static void RebuildToolbar()
        {
            MainToolbar.Refresh(k_ToolbarPath);
        }

        static void OpenToolbarPopup(Rect rect)
        {
            m_ToolbarController?.OnOpenToolbarPopup(rect);
        }
    }
}

#endif
