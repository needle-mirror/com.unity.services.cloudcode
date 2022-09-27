// WARNING: Auto generated code by Starbuck2. Modifications will be lost!
using System;
using System.Timers;
using Unity.Services.CloudCode.Authoring.Editor.Shared.Threading;

namespace Unity.Services.CloudCode.Authoring.Editor.Shared.Tracking
{
    class EditorValueTracker<T> : IDisposable
    {
        public EventHandler<T> ValueChanged;
        Func<T> m_Getter;
        T m_PreviousValue;
        Timer m_Timer;

        public EditorValueTracker(Func<T> getter, float checkInterval = 500f)
        {
            m_Getter = getter;
            m_PreviousValue = m_Getter();
            m_Timer = new Timer()
            {
                Interval = checkInterval
            };

            m_Timer.Elapsed += (_, _) => Sync.RunNextUpdateOnMain(TrackValue);
            m_Timer.Start();
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
            m_Timer.Dispose();
        }
    }
}
