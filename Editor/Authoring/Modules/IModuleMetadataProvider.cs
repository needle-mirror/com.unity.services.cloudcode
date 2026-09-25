namespace Unity.Services.CloudCode.Authoring.Editor.Modules
{
    /// <summary>Describes a Cloud Code Module for the analytics sent when it is deployed.</summary>
    interface IModuleMetadataProvider
    {
        /// <summary>Never throws; an unreadable source yields that field's unknown value.</summary>
        ModuleMetadata GetMetadata(string cloudAssemblyName);
    }

    /// <summary>Value domain of the module_type analytics field.</summary>
    enum ModuleType
    {
        Unknown,
        CloudCodeModule,
        CloudBehaviour
    }

    readonly struct ModuleMetadata
    {
        public static readonly ModuleMetadata Unknown =
            new ModuleMetadata(ModuleType.Unknown, false, false);

        public ModuleType Type { get; }
        public bool PlayerScopeUsed { get; }
        public bool MultiplayerSessionScopeUsed { get; }

        public ModuleMetadata(ModuleType type, bool playerScopeUsed, bool multiplayerSessionScopeUsed)
        {
            Type = type;
            PlayerScopeUsed = playerScopeUsed;
            MultiplayerSessionScopeUsed = multiplayerSessionScopeUsed;
        }
    }

    static class ModuleTypeExtensions
    {
        public static string ToAnalyticsValue(this ModuleType type)
        {
            switch (type)
            {
                case ModuleType.CloudCodeModule:
                    return "Cloud Code Module";
                case ModuleType.CloudBehaviour:
                    return "Cloud Behaviour";
                default:
                    return "unknown";
            }
        }
    }
}
