using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Services.CloudCode.Authoring.Editor.Core.Crypto;
using Unity.Services.CloudCode.Authoring.Editor.Core.Model;

namespace Unity.Services.CloudCode.Authoring.Editor.Core.Deployment
{
    class JsScriptCache : IScriptCache
    {
        readonly IHashComputer m_HashComputer;
        readonly Dictionary<CacheKey, CacheValue> m_Cache = new Dictionary<CacheKey, CacheValue>();
        readonly IEnvironmentProvider m_EnvironmentProvider;

        public JsScriptCache(
            IHashComputer hashComputer,
            IEnvironmentProvider environmentProvider)
        {
            m_HashComputer = hashComputer;
            m_EnvironmentProvider = environmentProvider;
        }

        public bool HasItemChanged(IScript script)
        {
            var cacheKey = GetCacheKey(script);
            if (!m_Cache.TryGetValue(cacheKey, out var cacheValue))
            {
                return true;
            }

            var currentVal = GetCacheValue(script);
            var hasChanged = !cacheValue.Equals(currentVal);
            return hasChanged;
        }

        public void Cache(IScript script)
        {
            var cacheKey = GetCacheKey(script);
            if (script.LastPublishedDate == null)
            {
                m_Cache.Remove(cacheKey);
            }
            else
            {
                m_Cache[cacheKey] = GetCacheValue(script);
            }
        }

        CacheKey GetCacheKey(IScript script)
        {
            return new CacheKey(
                script,
                m_EnvironmentProvider.Current);
        }

        CacheValue GetCacheValue(IScript script)
        {
            return new CacheValue(
                script,
                m_HashComputer.ComputeFileHash(script));
        }

        readonly struct CacheKey
        {
            readonly string m_Name;
            readonly string m_Environment;

            public CacheKey(IScript script, string environment)
            {
                m_Name = script.Name.ToString();
                m_Environment = environment;
            }
        }

        readonly struct CacheValue
        {
            readonly string m_Date;
            readonly string m_Hash;
            readonly IReadOnlyList<CloudCodeParameter> m_Parameters;

            public CacheValue(IScript script, string hash)
            {
                m_Date = script.LastPublishedDate;
                m_Hash = hash;
                m_Parameters = new List<CloudCodeParameter>(script.Parameters);
            }

            bool Equals(CacheValue other)
            {
                return m_Date.Equals(other.m_Date)
                    && m_Hash.Equals(other.m_Hash)
                    && m_Parameters.SequenceEqual(other.m_Parameters);
            }

            public override bool Equals(object obj)
            {
                if (obj?.GetType() != GetType()) return false;
                return Equals((CacheValue)obj);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(
                    m_Date,
                    m_Hash,
                    m_Parameters);
            }
        }
    }
}
