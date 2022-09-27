namespace Unity.Services.CloudCode.Authoring.Editor.Core.Model
{
    struct ScriptInfo
    {
        public string ScriptName { get; }
        public string LastPublishedDate { get; }

        public ScriptInfo(string scriptName, string lastPublishedDate)
        {
            ScriptName = scriptName;
            LastPublishedDate = lastPublishedDate;
        }
    }
}
