using BridgeOfBlood.Data.Shared;

namespace BridgeOfBlood.Data.Spells
{
    public struct DamageConversion
    {
        public DamageType from;
        public DamageType to;
        public float percent; // 50 = 50%
    }

    public struct ExtraDamageAs
    {
        public DamageType from;
        public DamageType to;
        public float percent; // 20 = 20%
    }
    /// <summary>
    /// Hit-time scratch-stat dispatch retained by AttackEntityModifier.
    /// Spawn/item modification lookup uses typed IModificationData channels instead.
    /// </summary>
    public enum SpellModificationProperty : byte
    {
        CritChance = 0,
        CritMult = 1,
        Chains = 2,
        Pierce = 3,
        AreaOfEffect = 4,
        Duration = 5,
        CastSpeed = 6,
        Projectiles = 7,

        DamageScaling = 8,

        PhysicalDamageScaling = 10,
        ColdDamageScaling = 11,
        FireDamageScaling = 12,
        LightningDamageScaling = 13,

        PhysicalPenetration = 20,
        ColdPenetration = 21,
        FirePenetration = 22,
        LightningPenetration = 23,

        KnockbackStrength = 24,

        ManaCost = 25,
    }

}
