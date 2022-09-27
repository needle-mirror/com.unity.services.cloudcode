namespace Unity.Services.CloudCode.Authoring.Editor.Projects
{
    interface IProjectSettings
    {
        string NodeJsPath { get; }
        string NpmPath { get; }
        string ExternalEditorApplicationPath { get; }
        string ExternalEditorArgumentFormat { get; }
    }
}
