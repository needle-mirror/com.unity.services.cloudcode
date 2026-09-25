// Pinned so the NuGet build (dotnet SDK, stamps AssemblyVersion from the csproj <Version>) and
// the Unity asmdef build (stamps 0.0.0.0 by default) produce the same assembly identity. Keep in
// sync with NuGet~/core/Unity.Services.CloudCode.Core.csproj's <Version> on every bump.
[assembly: System.Reflection.AssemblyVersion("0.0.7.0")]
