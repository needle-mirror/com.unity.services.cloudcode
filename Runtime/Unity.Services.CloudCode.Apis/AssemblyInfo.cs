using System.Runtime.CompilerServices;

// Pinned so the NuGet build (dotnet SDK, stamps AssemblyVersion from the csproj <Version>) and
// the Unity asmdef build (stamps 0.0.0.0 by default) produce the same assembly identity. Keep in
// sync with NuGet~/apis/Unity.Services.CloudCode.Apis.csproj's <Version> on every bump.
[assembly: System.Reflection.AssemblyVersion("0.0.27.0")]

// Test assemblies
#if UNITY_INCLUDE_TESTS
[assembly: InternalsVisibleTo("Unity.Services.CloudCode.Apis.Tests.Editor")]

#endif
