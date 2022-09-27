using System.Collections.Generic;

namespace Unity.Services.CloudCode.Authoring.Editor.Core.Model
{
    interface IScript
    {
        ScriptName Name { get; }

        List<CloudCodeParameter> Parameters { get; }

        Language? Language { get; set; }

        string Path { get; }

        string Body { get; }

        string LastPublishedDate { get; set; }
    }

    enum Language
    {
        JS = 1
    }
}
