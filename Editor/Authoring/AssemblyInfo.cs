using System.Runtime.CompilerServices;

// Test assemblies
#if UNITY_INCLUDE_TESTS
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]
[assembly: InternalsVisibleTo("GameIteration")]
[assembly: InternalsVisibleTo("Unity.Services.CloudCode.Authoring.Tests.Editor")]
[assembly: InternalsVisibleTo("Unity.Services.CloudCode.Analytics.PlayerScope.Tests.Editor")]
[assembly: InternalsVisibleTo("Unity.Services.CloudCode.Analytics.SessionScope.Tests.Editor")]
[assembly: InternalsVisibleTo("Unity.Services.CloudCode.IntegrationTests.Editor")]
[assembly: InternalsVisibleTo("Unity.Services.CloudCode.Editor")]
[assembly: InternalsVisibleTo("CloudCode.Runtime.Tests")]
[assembly: InternalsVisibleTo("CloudCode.Editor.Tests")]
#endif

[assembly: InternalsVisibleTo("Unity.Services.CloudCode.Cli")]
[assembly: InternalsVisibleTo("Unity.Services.CloudCode.PlayMode.Editor")]
