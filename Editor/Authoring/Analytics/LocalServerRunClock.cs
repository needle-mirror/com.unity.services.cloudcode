#if UNITY_6000_3_OR_NEWER
using System;
using System.Globalization;
using UnityEditor;

namespace Unity.Services.CloudCode.Authoring.Editor.Analytics
{
    interface ISessionStore
    {
        string GetString(string key);
        void SetString(string key, string value);
        void EraseKey(string key);
    }

    class SessionStateStore : ISessionStore
    {
        public string GetString(string key) => SessionState.GetString(key, string.Empty);
        public void SetString(string key, string value) => SessionState.SetString(key, value);
        public void EraseKey(string key) => SessionState.EraseString(key);
    }

    /// <summary>
    /// How long the local server has been running.
    /// </summary>
    class LocalServerRunClock
    {
        internal const string k_StartedAtKey = "LOCAL_CLOUD_CODE_STARTED_AT";

        readonly ISessionStore m_Store;
        readonly Func<DateTime> m_UtcNow;

        public LocalServerRunClock() : this(new SessionStateStore(), () => DateTime.UtcNow) {}

        internal LocalServerRunClock(ISessionStore store, Func<DateTime> utcNow)
        {
            m_Store = store;
            m_UtcNow = utcNow;
        }

        public void MarkStarted()
        {
            m_Store.SetString(k_StartedAtKey, m_UtcNow().Ticks.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>Elapsed milliseconds since MarkStarted. </summary>
        public long ConsumeUpTimeMs()
        {
            var startedAt = m_Store.GetString(k_StartedAtKey);
            m_Store.EraseKey(k_StartedAtKey);

            if (!long.TryParse(startedAt, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ticks))
                return 0;

            if (ticks < 0 || ticks > DateTime.MaxValue.Ticks)
                return 0;

            var elapsed = m_UtcNow() - new DateTime(ticks, DateTimeKind.Utc);
            return elapsed < TimeSpan.Zero ? 0 : (long)elapsed.TotalMilliseconds;
        }
    }
}
#endif
