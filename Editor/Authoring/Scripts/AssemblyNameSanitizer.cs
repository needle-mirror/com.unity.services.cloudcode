using System.IO;
using System.Linq;

namespace Unity.Services.CloudCode.Authoring.Editor.Scripts
{
    static class AssemblyNameSanitizer
    {
        // Path.GetInvalidFileNameChars() only returns '/' and '\0' on macOS and Linux.
        static readonly char[] k_InvalidChars = Path.GetInvalidFileNameChars().Union("/?<>\\:*|\"").ToArray();

        public static string Sanitize(string name)
        {
            return new string(name.Where(c => !k_InvalidChars.Contains(c)).ToArray());
        }
    }
}
