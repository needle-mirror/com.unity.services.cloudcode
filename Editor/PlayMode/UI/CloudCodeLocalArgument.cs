using System.Collections.Generic;
using System.Text;

namespace Unity.Services.CloudCode.Editor.PlayMode
{
    // A Local Instance is a standalone player build, so it cannot pick up the scenario's deploy
    // target the way editor instances do. This argument, added by hand to the instance's
    // Arguments, is what points it at the local Cloud Code server instead.
    static class CloudCodeLocalArgument
    {
        internal const string k_Name = "--cloud-code-local-debugger";
        internal const int k_DefaultPort = CloudCodeInitializer.k_DefaultLocalCloudCodeServerPort;

        // LocalPlayerSettingsDrawer labels the foldout "Advanced Configuration".
        const string k_ArgumentsLocation = "(Local Instances > Advanced Configuration > Arguments)";

        internal const string k_RemoveMessage =
            "For local instances to switch connection from the local Cloud Code server to the remote Cloud Code server, please remove \""
            + k_Name + "\" from each local instance's argument field " + k_ArgumentsLocation + ".";

        internal static int ToEffectivePort(int configuredPort)
            => configuredPort > 0 && configuredPort <= ushort.MaxValue ? configuredPort : k_DefaultPort;

        internal static string GetArgument(int port)
        {
            var effectivePort = ToEffectivePort(port);
            return effectivePort == k_DefaultPort ? k_Name : $"{k_Name}={effectivePort}";
        }

        // "set" covers an instance that already carries the flag but points at another port.
        internal static string GetConnectMessage(int port, bool replaceExisting)
        {
            var action = replaceExisting ? "set" : "add";
            var preposition = replaceExisting ? "in" : "to";
            return $"For local instances to be connected to the local Cloud Code server, please {action} \"{GetArgument(port)}\" {preposition} each local instance's argument field {k_ArgumentsLocation}.";
        }

        // Runs the player's own parse, so the answer matches what the instance will actually do.
        internal static bool TryGetPort(string arguments, out int port)
        {
            var parsedPort = k_DefaultPort;
            var found = CloudCodeInitializer.TryGetLocalDebugPortFromCommandLine(Tokenize(arguments), ref parsedPort);
            port = parsedPort;
            return found;
        }

        // The Arguments string reaches the player as one command line, so it is split the way the
        // process launcher splits it: on spaces and tabs only, with double quotes grouping text and
        // then removed. Backslash escapes are not handled, since no valid Cloud Code argument needs them.
        internal static string[] Tokenize(string arguments)
        {
            var tokens = new List<string>();
            if (string.IsNullOrEmpty(arguments))
                return tokens.ToArray();

            var current = new StringBuilder();
            var inQuotes = false;
            var hasToken = false;

            foreach (var c in arguments)
            {
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    hasToken = true;
                }
                else if (!inQuotes && (c == ' ' || c == '\t'))
                {
                    if (hasToken)
                        tokens.Add(current.ToString());

                    current.Clear();
                    hasToken = false;
                }
                else
                {
                    current.Append(c);
                    hasToken = true;
                }
            }

            if (hasToken)
                tokens.Add(current.ToString());

            return tokens.ToArray();
        }

        // Null when every Local Instance already matches the target.
        internal static string GetWarning(
            CloudCodeDeployTarget target,
            int configuredPort,
            IReadOnlyList<string> localInstanceArguments)
        {
            configuredPort = ToEffectivePort(configuredPort);
            var anyMissing = false;
            var anyWrongPort = false;
            var anyPresent = false;

            foreach (var arguments in localInstanceArguments)
            {
                if (TryGetPort(arguments, out var port))
                {
                    anyPresent = true;
                    anyWrongPort |= port != configuredPort;
                }
                else
                {
                    anyMissing = true;
                }
            }

            if (target != CloudCodeDeployTarget.Local)
                return anyPresent ? k_RemoveMessage : null;

            return anyWrongPort || anyMissing ? GetConnectMessage(configuredPort, anyWrongPort) : null;
        }
    }
}
