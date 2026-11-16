using System.Linq;
using NUnit.Framework;
using Unity.Pipeline.Commands;

namespace Unity.Services.CloudCode.Cli.Tests
{
    class CloudCodeRemoteLogsCliRegistrationTests
    {
        const string k_CliAssembly = "Unity.Services.CloudCode.Cli";

        [Test]
        public void DiscoverCommands_RegistersGetRemoteLogs()
        {
            var registered = CommandRegistry.DiscoverCommands()
                .Where(command => command.Package == k_CliAssembly)
                .Select(command => (command.Group, command.Name));

            CollectionAssert.Contains(registered, (CloudCodeCliConstants.Group, "get_remote_logs"));
        }
    }
}
