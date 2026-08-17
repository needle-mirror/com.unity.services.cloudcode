using System.Threading.Tasks;

namespace Unity.Services.CloudCode.Authoring.Editor.SourceGenerator
{
    interface ISourceGeneratorBuilder
    {
        Task Build();
    }
}
