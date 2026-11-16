using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Pipeline.Commands;

namespace Unity.Services.CloudCode.Cli.Tests
{
    class CloudCodeCliRegistrationTests
    {
        const string k_CliAssembly = "Unity.Services.CloudCode.Cli";
        const string k_Group = "services.cloudcode";

        [Test]
        public void DiscoverCommands_ListsEveryCloudCodeCommand()
        {
            var expected = new List<(string Group, string Name)>
            {
                (k_Group, "get_mode"),
                (k_Group, "create_js_script"),
                (k_Group, "create_module_reference"),
#if UNITY_6000_3_OR_NEWER
                (k_Group, "get_local_server_status"),
                (k_Group, "get_local_server_logs"),
                (k_Group, "start_local_server"),
                (k_Group, "stop_local_server"),
                (k_Group, "set_local_server_port"),
                (k_Group, "delete_local_server_state"),
#endif
#if UNITY_6000_7_OR_NEWER
                (k_Group, "create_module"),
                (k_Group, "create_module_script"),
#endif
            };

            var actual = CommandRegistry.DiscoverCommands()
                .Where(command => command.Package == k_CliAssembly)
                .Select(command => (command.Group, command.Name))
                .ToList();

            var missing = expected.Except(actual).Select(command => $"{command.Group} {command.Name}").ToList();
            Assert.That(missing, Is.Empty, $"Cli commands not registered: {string.Join(", ", missing)}");
        }
    }
}
