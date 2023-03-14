using System.Collections.Generic;

namespace Unity.Services.CloudCode.Authoring.Editor.Core.Model
{
    struct ScriptInfo : IScript
    {
        public ScriptName Name { get; }
        public List<CloudCodeParameter> Parameters { get; }
        public Language? Language { get; set; }
        public string Path => null;
        public string Body => null;
        public string LastPublishedDate { get; set; }

        public ScriptInfo(string scriptName, string extension, string lastPublishedDate = "", Language language = Model.Language.JS)
        {
            Name = new ScriptName(scriptName + extension);
            Language = language;
            Parameters = new List<CloudCodeParameter>();
            LastPublishedDate = lastPublishedDate;
        }

        public ScriptInfo(ScriptName scriptName, string lastPublishedDate = "", Language language = Model.Language.JS)
        {
            Name = scriptName;
            Language = language;
            Parameters = new List<CloudCodeParameter>();
            LastPublishedDate = lastPublishedDate;
        }
    }
}
