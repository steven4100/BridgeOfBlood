using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "AttackEntityData", menuName = "BridgeOfBlood/Spells/Attack Entity Data")]
public class AttackEntityData : ScriptableObject
{
    public float physicalDamage;
    public float coldDamage;
    public float fireDamage;
    public float lightningDamage;
    public float critChance;
    public float critDamageMultiplier;
    public Vector2 entityVelocity;
    public HitBoxData hitBoxData;
    public float rehitCooldownSeconds;

    [Tooltip("Optional behaviors (pierce, expiration, chain). Only present behaviors are serialized.")]
    [SerializeReference]
    [AttackEntityBehaviorsList]
    public List<AttackEntityBehavior> behaviors = new List<AttackEntityBehavior>();
}
