using System.Collections.Generic;
using BridgeOfBlood.Data.Spells;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Handles spell invocation: starts casts at a given origin/time and advances them through
/// keyframes, spawning attack entities. Used by LoopedSpellCaster (loop-driven casts) and
/// SpellCastTester (e.g. click-to-cast) so each can have its own invoker and active-cast list.
/// </summary>
public class SpellInvoker
{
    private readonly AttackEntityManager _attackEntityManager;

    private struct ActiveCast
    {
        public float2 origin;
        public float startTime;
        public int nextKeyframeIndex;
        public SpellAuthoringData spellData;
    }

    private readonly List<ActiveCast> _activeCasts = new List<ActiveCast>();

    public SpellInvoker(AttackEntityManager attackEntityManager)
    {
        _attackEntityManager = attackEntityManager;
    }

    /// <summary>
    /// Start a new cast of the given spell at the given origin. Keyframes will fire relative to startTime.
    /// </summary>
    public void StartCast(SpellAuthoringData spellData, float2 origin, float startTime)
    {
        if (spellData == null || spellData.SpellAnimation?.keyFrames == null || spellData.SpellAnimation.keyFrames.Count == 0)
            return;

        _activeCasts.Add(new ActiveCast
        {
            origin = origin,
            startTime = startTime,
            nextKeyframeIndex = 0,
            spellData = spellData
        });
    }

    /// <summary>
    /// Advance all active casts and spawn keyframe entities that are due by simulationTime.
    /// </summary>
    public void Update(float simulationTime)
    {
        for (int c = _activeCasts.Count - 1; c >= 0; c--)
        {
            ActiveCast cast = _activeCasts[c];
            if (cast.spellData?.SpellAnimation?.keyFrames == null)
            {
                _activeCasts.RemoveAt(c);
                continue;
            }

            var keyFrames = cast.spellData.SpellAnimation.keyFrames;
            float elapsed = simulationTime - cast.startTime;

            while (cast.nextKeyframeIndex < keyFrames.Count
                   && elapsed >= keyFrames[cast.nextKeyframeIndex].time)
            {
                SpawnKeyframeEntities(keyFrames[cast.nextKeyframeIndex], cast.origin);
                cast.nextKeyframeIndex++;
            }

            if (cast.nextKeyframeIndex >= keyFrames.Count)
                _activeCasts.RemoveAt(c);
            else
                _activeCasts[c] = cast;
        }
    }

    void SpawnKeyframeEntities(SpellKeyFrame keyFrame, float2 origin)
    {
        if (keyFrame.entitiesToSpawn == null) return;
        foreach (var data in keyFrame.entitiesToSpawn)
        {
            float2 spawnPos = origin;
            if (data.spawnType == AttackEntitySpawnType.RelativeToPlayer)
            {
                spawnPos += new float2(
                    data.relativeToPlayerSpawnCriteria.offsetFromPlayer.x,
                    data.relativeToPlayerSpawnCriteria.offsetFromPlayer.y);
            }
            _attackEntityManager.Spawn(data, spawnPos);
        }
    }
}
