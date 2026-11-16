using System.Linq;

namespace Unity.Services.CloudCode.Authoring.Editor.Scripts
{
    static class ModuleNameSanitizer
    {
        // Cloud Code service rule (^[a-zA-Z0-9_]*$, at most 50 characters), starting with a letter since the name becomes a C# identifier and a solution name.
        public const int MaxLength = 50;

        public static string Sanitize(string name)
        {
            var sanitized = new string(name.Where(c => c < 128 && (char.IsLetterOrDigit(c) || c == '_')).ToArray()).TrimStart('0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '_');
            return sanitized.Length > MaxLength ? sanitized.Substring(0, MaxLength) : sanitized;
        }
    }
}
