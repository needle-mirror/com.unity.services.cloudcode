// UNITY_PIPELINE_AVAILABLE only means the package is installed; match Unity.Pipeline's own constraint.
#if UNITY_PIPELINE_AVAILABLE && (UNITY_EDITOR || (ENABLE_RUNTIME_PIPELINE_DEV && (UNITY_INCLUDE_INSTRUMENTATION || ENABLE_PROFILER || ENABLE_RUNTIME_PIPELINE)) || (ENABLE_RUNTIME_PIPELINE && !UNITY_INCLUDE_INSTRUMENTATION && !ENABLE_PROFILER))
#if UNITY_6000_7_OR_NEWER
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Pipeline.Commands;
using Unity.Services.CloudBehaviours;
using Unity.Services.CloudCode;
using UnityEngine;

namespace Unity.Services.CloudCode.Cli
{
    /// <summary>
    /// Pipeline CLI command that snapshots the live Cloud Behaviour client bindings — the same
    /// connection status and synchronized values the generated inspector shows, which is the
    /// documented first step when a behaviour misbehaves but is otherwise only visible to a human
    /// looking at the Inspector.
    /// </summary>
    /// <remarks>
    /// Unlike its neighbours in this assembly this carries no <c>UNITY_EDITOR</c> guard, so it survives
    /// into a Player build and the on-device runtime server can serve it.
    /// </remarks>
    static class CloudBehaviourRuntimeCliCommands
    {
        [CliGroupCommand(CloudCodeCliConstants.Group,
            "Snapshot every live Cloud Behaviour client binding: its scope, connection status and current synchronized state. Requires Play Mode, or a running Player",
            Name = "get_cloudbehaviour_state")]
        public static List<BehaviourStateDto> GetCloudBehaviourState(
            [CliArg("behaviour", "Only bindings of this client type (e.g. MatchStateClient)")] string behaviour = null,
            [CliArg("game-object", "Only bindings on the GameObject with this name")] string gameObject = null)
        {
            List<BehaviourStateDto> found = UnityEngine.Object
                .FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include)
                .Where(IsBinding)
                .Where(mb => behaviour == null || mb.GetType().Name == behaviour)
                .Where(mb => gameObject == null || mb.gameObject.name == gameObject)
                .Select(BehaviourStateDto.From)
                .ToList();

            if (found.Count == 0 && (behaviour != null || gameObject != null))
                throw new InvalidOperationException(
                    "No Cloud Behaviour binding matched. Run the command with no filters to list every live binding.");

            return found;
        }

        static bool IsBinding(MonoBehaviour behaviour) =>
            behaviour != null && behaviour.GetType().GetCustomAttribute<CloudBehaviourAttribute>() != null;

        /// <summary>
        /// The synchronized members and <c>[SyncView]</c> projections of a binding, read off the
        /// <c>&lt;CloudType&gt;_Sync</c> the generator emits for it — the same contract it synchronizes
        /// against, so it stays right as members come and go. Null when that type cannot be resolved,
        /// which the caller should report rather than guessing at the member list.
        /// </summary>
        /// <remarks>
        /// The cloud assembly is Editor-only, so the generator emits the type twice over and the two
        /// layouts are mutually exclusive: beside the cloud class for the Editor, and re-emitted into the
        /// binding's own namespace for a Player, where the binding has no reference to the cloud assembly.
        /// Generated code reaches it by simple name and never spells out a namespace; the cloud type is the
        /// stable anchor in both, since its full name is preserved even where it is only a stub.
        /// CloudBehaviourAttribute takes the cloud type but does not keep it, so the constructor argument
        /// is read off the attribute data rather than the attribute.
        /// </remarks>
        static HashSet<string> SynchronizedMembers(Type binding)
        {
            Type cloud = CustomAttributeData.GetCustomAttributes(binding)
                .FirstOrDefault(data => data.AttributeType == typeof(CloudBehaviourAttribute))
                ?.ConstructorArguments.FirstOrDefault().Value as Type;
            if (cloud == null)
                return null;

            Type sync = cloud.Assembly.GetType(cloud.FullName + CloudCodeCliConstants.SyncTypeSuffix)
                ?? binding.Assembly.GetType(Qualify(binding.Namespace, cloud.Name + CloudCodeCliConstants.SyncTypeSuffix));

            return sync == null
                ? null
                : sync.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Select(property => property.Name)
                    .ToHashSet();
        }

        static string Qualify(string containingNamespace, string name) =>
            string.IsNullOrEmpty(containingNamespace) ? name : $"{containingNamespace}.{name}";

        /// <summary>Serializable snapshot of one client binding for CLI JSON output.</summary>
        public class BehaviourStateDto
        {
            public string Behaviour { get; set; }
            public string GameObject { get; set; }
            public bool IsClientEnabled { get; set; }
            public string ScopeType { get; set; }                           // null until a scope resolves
            public string ScopeId { get; set; }
            public string Target { get; set; }                              // TargetSessionType / TargetPlayerId
            public Dictionary<string, object> State { get; set; } = new();
            public List<string> Unreadable { get; set; } = new();           // whatever stopped state being read

            public static BehaviourStateDto From(MonoBehaviour binding)
            {
                Type type = binding.GetType();
                var dto = new BehaviourStateDto
                {
                    Behaviour = type.Name,
                    GameObject = binding.gameObject.name,
                    IsClientEnabled = Read(binding, type, "IsClientEnabled") as bool? ?? false,
                    Target = ReadTarget(binding, type),
                };

                if (Read(binding, type, "Scope") is CloudCodeScope scope)
                {
                    dto.ScopeType = scope.Type.ToString();
                    dto.ScopeId = scope.Id;
                }

                // Extract list of members that make up the synchronized state, so we can differentiate
                // between the state members and other members on the binding.
                HashSet<string> synchronized = SynchronizedMembers(type);
                if (synchronized == null)
                {
                    // Every generated binding has one, so this is a broken build or a generator change
                    // rather than a state worth reporting around.
                    dto.Unreadable.Add(
                        $"{type.Name}: no generated <CloudType>{CloudCodeCliConstants.SyncTypeSuffix} type, so the synchronized "
                        + "members are unknown and no state is reported for this binding.");
                    return dto;
                }

                foreach (PropertyInfo property in type
                         .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                         .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
                         .Where(p => synchronized.Contains(p.Name)))
                {
                    try
                    {
                        dto.State[property.Name] = property.GetValue(binding);
                    }
                    catch (Exception e)
                    {
                        // A user getter can throw; one bad member must not cost the whole snapshot.
                        dto.Unreadable.Add($"{property.Name}: {e.GetBaseException().Message}");
                    }
                }

                return dto;
            }

            // The authored target is internal, so it is not reachable through the public surface.
            static string ReadTarget(MonoBehaviour binding, Type type) =>
                Read(binding, type, "TargetSessionType") as string ?? Read(binding, type, "TargetPlayerId") as string;

            static object Read(MonoBehaviour binding, Type type, string name)
            {
                PropertyInfo property = type.GetProperty(name,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (property == null || !property.CanRead)
                    return null;
                try
                {
                    return property.GetValue(binding);
                }
                catch
                {
                    return null;
                }
            }
        }
    }
}
#endif
#endif
