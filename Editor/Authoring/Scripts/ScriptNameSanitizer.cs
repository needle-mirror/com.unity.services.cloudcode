namespace Unity.Services.CloudCode.Authoring.Editor.Scripts
{
    static class ScriptNameSanitizer
    {
        public static string Sanitize(string name)
        {
            return AssemblyNameSanitizer.Sanitize(name).Replace(".", string.Empty).Replace("-", string.Empty);
        }
    }
}
