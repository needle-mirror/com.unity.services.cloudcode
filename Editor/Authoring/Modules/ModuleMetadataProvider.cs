using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Compilation;

namespace Unity.Services.CloudCode.Authoring.Editor.Modules
{
    /// <summary>
    /// Describes a deployed module by reflecting over its compiled cloud assembly, which the Editor
    /// has necessarily loaded, rather than the source generator's manifest, which can be deleted
    /// without causing a recompile.
    /// </summary>
    class ModuleMetadataProvider : IModuleMetadataProvider
    {
        // Assembly-qualified so Type.GetType resolves them without enumerating assemblies.
        const string k_StateScopeAttribute =
            "Unity.Services.CloudCode.Core.StateScopeAttribute, Unity.Services.CloudCode.Core";
        const string k_CloudBehaviourType =
            "Unity.Services.CloudBehaviours.CloudBehaviour, Unity.Services.CloudBehaviours.Editor";
        const string k_StateScopeProperty = "StateScope";
        const string k_ScopePlayer = "Player";
        const string k_ScopeMultiplayerSession = "MultiplayerSession";

        /// <inheritdoc/>
        public ModuleMetadata GetMetadata(string cloudAssemblyName)
        {
            if (string.IsNullOrEmpty(cloudAssemblyName))
                return ModuleMetadata.Unknown;

            try
            {
                // An assembly Unity does not compile cannot be described; naming a type would be a guess.
                if (!IsProjectAssembly(cloudAssemblyName))
                    return ModuleMetadata.Unknown;

                var(player, session) = GetScopes(cloudAssemblyName);
                return new ModuleMetadata(GetModuleType(cloudAssemblyName), player, session);
            }
            catch (Exception)
            {
                // Reporting analytics must never fail a deploy.
                return ModuleMetadata.Unknown;
            }
        }

        static bool IsProjectAssembly(string assemblyName)
        {
            return CompilationPipeline.GetAssemblies(AssembliesType.Editor)
                .Any(assembly => assembly.name == assemblyName);
        }

        /// <summary>A cloud class deriving from CloudBehaviour makes the module a Cloud Behaviour.</summary>
        static ModuleType GetModuleType(string cloudAssemblyName)
        {
            var behaviourType = FindType(k_CloudBehaviourType);
            if (behaviourType == null)
                return ModuleType.CloudCodeModule;

            return TypeCache.GetTypesDerivedFrom(behaviourType).Any(type => InAssembly(type, cloudAssemblyName))
                ? ModuleType.CloudBehaviour
                : ModuleType.CloudCodeModule;
        }

        /// <summary>
        /// Independent flags: a module can declare both scopes, and a class with no [StateScope] sets
        /// neither. Matched on the enum member name so reordering Scope cannot invert the result.
        /// </summary>
        static(bool player, bool session) GetScopes(string cloudAssemblyName)
        {
            var attributeType = FindType(k_StateScopeAttribute);
            if (attributeType == null)
                return (false, false);

            var player = false;
            var session = false;
            foreach (var type in TypeCache.GetTypesWithAttribute(attributeType))
            {
                if (!InAssembly(type, cloudAssemblyName))
                    continue;

                switch (ReadScopeName(type, attributeType))
                {
                    case k_ScopePlayer:
                        player = true;
                        break;
                    case k_ScopeMultiplayerSession:
                        session = true;
                        break;
                }
            }

            return (player, session);
        }

        static bool InAssembly(Type type, string assemblyName)
        {
            return type.Assembly.GetName().Name == assemblyName;
        }

        // By name: referencing Core or CloudBehaviours would force their UNITY_6000_5_OR_NEWER
        // constraint onto this assembly, which must compile on the package minimum.
        static Type FindType(string assemblyQualifiedName)
        {
            return Type.GetType(assemblyQualifiedName, false);
        }

        static string ReadScopeName(Type type, Type attributeType)
        {
            var attribute = type.GetCustomAttributes(attributeType, false).FirstOrDefault();
            var value = attributeType
                .GetProperty(k_StateScopeProperty, BindingFlags.Public | BindingFlags.Instance)
                ?.GetValue(attribute);

            return value?.ToString();
        }
    }
}
