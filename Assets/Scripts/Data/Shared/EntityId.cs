using System;

namespace BridgeOfBlood.Data.Shared
{
    /// <summary>
    /// Stable handle into an entity table. Index identifies the slot; Generation rejects stale handles after slot reuse.
    /// </summary>
    [Serializable]
    public struct EntityId : IEquatable<EntityId>
    {
        public int Index;
        public uint Generation;

        public static readonly EntityId Invalid = new EntityId { Index = -1, Generation = 0u };

        public bool IsValid => Index >= 0 && Generation != 0u;

        public bool Equals(EntityId other) => Index == other.Index && Generation == other.Generation;

        public override bool Equals(object obj) => obj is EntityId other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (Index * 397) ^ (int)Generation;
            }
        }

        public override string ToString() => IsValid ? $"{Index}:{Generation}" : "Invalid";

        public static bool operator ==(EntityId left, EntityId right) => left.Equals(right);

        public static bool operator !=(EntityId left, EntityId right) => !left.Equals(right);
    }
}
