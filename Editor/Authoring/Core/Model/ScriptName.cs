using System.IO;

namespace Unity.Services.CloudCode.Authoring.Editor.Core.Model
{
    readonly struct ScriptName
    {
        readonly string m_Name;

        public ScriptName(string name)
        {
            m_Name = name;
        }

        public static ScriptName FromPath(string path)
        {
            if (path == null)
            {
                return new ScriptName(null);
            }

            return new ScriptName(Path.GetFileName(path));
        }

        public string GetNameWithoutExtension()
        {
            return GetFileNameWithoutSubExtension(m_Name);
        }

        bool Equals(ScriptName other)
        {
            return m_Name?.ToLowerInvariant() == other.m_Name?.ToLowerInvariant();
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (obj.GetType() != GetType()) return false;
            return Equals((ScriptName)obj);
        }

        public override int GetHashCode()
        {
            return m_Name != null ? m_Name.ToLowerInvariant().GetHashCode() : 0;
        }

        public override string ToString()
        {
            return m_Name;
        }

        static string GetFileNameWithoutSubExtension(string path)
        {
            var file = Path.GetFileName(path);
            var separator = file.IndexOf(".");
            if (separator != -1)
            {
                file = file.Substring(0, separator);
            }
            return file;
        }
    }
}
