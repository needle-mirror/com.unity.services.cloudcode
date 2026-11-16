namespace Unity.Services.CloudCode.Cli
{
    /// <summary>
    /// The constants this package's CLI commands use: their groups, tags, and the other literals they depend on,
    /// so each has one source of truth. Keep one class per package: a const read from another assembly is
    /// compiled into the reader, so a shared class would go stale in every package not rebuilt with it.
    /// </summary>
    static class CloudCodeCliConstants
    {
        public const string Group = "services.cloudcode";
        public const string AssetsRoot = "Assets";
        /// <summary>
        /// Suffix of the synchronized-state type the Cloud Behaviour source generator emits per client. Must match
        /// the generator's <c>BehaviourModule.SynchronizationName</c>.
        /// </summary>
        public const string SyncTypeSuffix = "_Sync";
        /// <summary>
        /// The new-script template, relative to the package's Editor folder. Must match
        /// <c>CreateCloudCodeScript</c>'s template path.
        /// </summary>
        public const string ScriptTemplatePath = "Authoring/Scripts/Templates~/new_cloud_code_script.js.txt";
        public const int NoLocalServer = -1;
        public const string ModeLocal = "Local";
        public const string ModeRemote = "Remote";
        /// <summary>The default start of the <c>get_remote_logs</c> time range, relative to now.</summary>
        public const string DefaultLogsFrom = "now-1h";
    }
}
