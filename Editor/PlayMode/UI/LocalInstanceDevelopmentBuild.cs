using System.Collections.Generic;
using Unity.Multiplayer.PlayMode.Editor;
using UnityEditor;
using UnityEditor.Build.Profile;
using LocalInstanceSettings = Unity.Multiplayer.PlayMode.Editor.LocalPlayerController.InstanceSettings;

namespace Unity.Services.CloudCode.Editor.PlayMode
{
    static class LocalInstanceDevelopmentBuild
    {
        // The editor keeps this flag internal, so it can only be read through serialization.
        const string k_DevelopmentPropertyPath = "m_PlatformBuildProfile.m_Development";

        internal const string k_Message =
            "For local instances to be connected to the local Cloud Code server, please enable \"Development Build\" in each local instance's build profile (Local Instances > Build Profile). Release builds ignore \""
            + CloudCodeLocalArgument.k_Name + "\".";

        // False when the flag cannot be read: Multiplayer Play Mode already rejects such a profile.
        internal static bool IsReleaseBuild(BuildProfile profile)
        {
            if (profile == null)
                return false;

            using var serializedProfile = new SerializedObject(profile);
            var development = serializedProfile.FindProperty(k_DevelopmentPropertyPath);
            return development != null && !development.boolValue;
        }

        internal static string GetWarning(CloudCodeDeployTarget target, IReadOnlyList<bool> releaseBuilds)
        {
            if (target != CloudCodeDeployTarget.Local)
                return null;

            foreach (var isReleaseBuild in releaseBuilds)
            {
                if (isReleaseBuild)
                    return k_Message;
            }

            return null;
        }

        internal static string GetWarning(CloudCodeDeployTarget target, IReadOnlyList<BuildProfile> profiles)
        {
            if (target != CloudCodeDeployTarget.Local)
                return null;

            foreach (var profile in profiles)
            {
                if (IsReleaseBuild(profile))
                    return k_Message;
            }

            return null;
        }

        internal static List<BuildProfile> GetLocalInstanceBuildProfiles(OrchestratedScenario scenario)
        {
            var profiles = new List<BuildProfile>();
            if (scenario == null)
                return profiles;

            foreach (var item in scenario.GetAllInstances())
            {
                if (item is not PlayModeControllerItem<LocalPlayerController, LocalInstanceSettings>)
                    continue;

                var profile = item.GetSettings<LocalInstanceSettings>().BuildProfile;
                if (profile != null)
                    profiles.Add(profile);
            }

            return profiles;
        }
    }
}
