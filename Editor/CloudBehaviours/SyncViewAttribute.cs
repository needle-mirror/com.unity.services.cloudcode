using System;

namespace Unity.Services.CloudBehaviours
{
    /// <summary>
    /// Marks a method as a per-viewer synchronization projection: the method <em>is</em> the synced
    /// member. The generator emits one wire member whose key is the required <see cref="Name"/> and whose
    /// type is the method's return type, filled per viewer by calling the method with that player's id.
    /// </summary>
    /// <remarks>
    /// Signature: <c>TWire Method(string playerId)</c> — a non-void return (the wire type) and a single
    /// <c>string</c> playerId parameter. The method reads private module state and returns the value that
    /// player should see; it must be side-effect-free.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class SyncViewAttribute : Attribute
    {
        /// <summary>The wire key: this member's name in the sync envelope and on the client.</summary>
        public string Name { get; }

        /// <summary>Marks the method as a projection surfaced on the wire under <paramref name="name"/>.</summary>
        /// <param name="name">The wire key — the sync-envelope/client member name. Required and non-empty.</param>
        public SyncViewAttribute(string name)
        {
            Name = name;
        }
    }
}
