// WARNING: Auto generated code. Modifications will be lost!

using System;
using UnityEditor;

namespace Unity.Services.CloudCode.Editor.Shared.EditorUtils
{
    class EditorValueTracker<T> : IDisposable
    {
        public EventHandler<T> ValueChanged;
        Func<T> m_Getter;
        T m_PreviousValue;
        readonly EditorApplication.CallbackFunction m_UpdateCallback;
        readonly double m_CheckIntervalSeconds;
        double m_LastCheckTime;
        bool m_Disposed;

        public T Value => m_Getter();

        public EditorValueTracker(Func<T> getter, float checkInterval = 500f)
        {
            m_Getter = getter;
            m_PreviousValue = m_Getter();
            m_CheckIntervalSeconds = checkInterval / 1000.0;
            m_LastCheckTime = EditorApplication.timeSinceStartup;
            m_UpdateCallback = OnEditorUpdate;
            EditorApplication.update += m_UpdateCallback;
        }

        void OnEditorUpdate()
        {
            if (m_Disposed)
            {
                return;
            }

            if (EditorApplication.timeSinceStartup - m_LastCheckTime < m_CheckIntervalSeconds)
            {
                return;
            }

            m_LastCheckTime = EditorApplication.timeSinceStartup;
            TrackValue();
        }

        void TrackValue()
        {
            var currentValue = m_Getter();

            if (!m_PreviousValue.Equals(currentValue))
            {
                ValueChanged?.Invoke(this, currentValue);
                m_PreviousValue = currentValue;
            }
        }

        public void Dispose()
        {
            m_Disposed = true;
            EditorApplication.update -= m_UpdateCallback;
            m_Getter = null;
            ValueChanged = null;
        }
    }
}
