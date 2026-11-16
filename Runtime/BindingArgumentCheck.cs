using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Debug = UnityEngine.Debug;

namespace Unity.Services.CloudCode
{
    /// <summary>
    /// Called by generated Cloud Code module and Cloud Behaviour clients; not meant to be
    /// called directly.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class BindingArgumentCheck
    {
        static readonly HashSet<(string Endpoint, string Parameter, Type RuntimeType)> s_Reported = new();
        static readonly Dictionary<(Type Subtype, Type Declared), bool> s_AddsMembers = new();

        // Debug.isDebugBuild is main-thread only, and generated clients can be called from any thread.
#if UNITY_EDITOR
        static bool s_DebugBuild = true;
#else
        static bool s_DebugBuild;
#endif

        /// <summary>
        /// Warns once per endpoint, parameter and runtime type when
        /// <paramref name="argument"/> is a subclass of its declared type <typeparamref name="T"/>. Arguments
        /// are not serialized polymorphically, so the server reads a <typeparamref name="T"/> and the fields
        /// only the subclass declares are lost. Does nothing in release player builds.
        /// </summary>
        /// <param name="argument">The value passed for the parameter.</param>
        /// <param name="endpoint">The endpoint as shown to the user, such as <c>Module.Function</c>.</param>
        /// <param name="parameter">The parameter's name.</param>
        /// <typeparam name="T">The parameter's declared type.</typeparam>
        public static void WarnIfSubtype<T>(T argument, string endpoint, string parameter)
        {
            if (s_DebugBuild && Checked<T>.Applies && argument != null)
                Report(argument, typeof(T), endpoint, parameter);
        }

        internal static void Report(object argument, Type declared, string endpoint, string parameter)
        {
            var runtimeType = argument.GetType();
            if (runtimeType == declared || !AddsSerializedMembers(runtimeType, declared))
                return;

            lock (s_Reported)
            {
                if (!s_Reported.Add((endpoint, parameter, runtimeType)))
                    return;
            }

            Debug.LogWarning(
                $"{endpoint}: parameter \"{parameter}\" is declared as {declared.FullName} but was passed a " +
                $"{runtimeType.FullName} (assembly \"{runtimeType.Assembly.GetName().Name}\"). Values are not " +
                $"serialized polymorphically, so the server reads a {declared.Name} and the fields only " +
                $"{runtimeType.Name} declares are lost. Pass a {declared.Name}, or declare the parameter as the " +
                "type you send.");
        }

        internal static bool AddsSerializedMembers(Type subtype, Type declared)
        {
            lock (s_AddsMembers)
            {
                if (s_AddsMembers.TryGetValue((subtype, declared), out var known))
                    return known;
            }

            var declaredNames = new HashSet<string>(SerializedMemberNames(declared));
            var result = SerializedMemberNames(subtype).Any(name => !declaredNames.Contains(name));

            lock (s_AddsMembers)
            {
                s_AddsMembers[(subtype, declared)] = result;
            }

            return result;
        }

        static IEnumerable<string> SerializedMemberNames(Type type)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
            foreach (var field in type.GetFields(flags))
            {
                if (!field.IsNotSerialized && !IsJsonIgnored(field))
                    yield return field.Name;
            }

            foreach (var property in type.GetProperties(flags))
            {
                if (property.GetIndexParameters().Length == 0 && property.GetMethod is { IsPublic: true }
                    && !IsJsonIgnored(property))
                    yield return property.Name;
            }
        }

        static bool IsJsonIgnored(MemberInfo member) => Attribute.IsDefined(member, typeof(JsonIgnoreAttribute));

        internal static bool IsSubstitutable(Type type)
            => type.IsClass && !type.IsAbstract && !type.IsSealed && !type.IsArray && !IsFrameworkAssembly(type);

        static bool IsFrameworkAssembly(Type type)
        {
            var name = type.Assembly.GetName().Name ?? string.Empty;
            var root = name.Split('.')[0];
            return root == "System" || root == "mscorlib" || root == "netstandard" || root == "Microsoft"
                   || name.StartsWith("Unity.Services", StringComparison.Ordinal)
                   || name.StartsWith("UnityEngine", StringComparison.Ordinal)
                   || name.StartsWith("UnityEditor", StringComparison.Ordinal);
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ReadBuildType() => s_DebugBuild = Debug.isDebugBuild;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetReported()
        {
            lock (s_Reported)
            {
                s_Reported.Clear();
            }
        }

        static class Checked<T>
        {
            public static readonly bool Applies = IsSubstitutable(typeof(T));
        }
    }
}
