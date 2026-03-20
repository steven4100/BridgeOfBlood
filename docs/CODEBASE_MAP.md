# Bridge of Blood -- Codebase Map

Short reference for where systems live and how data flows. Update when adding major systems.

---

## Script layout

| Folder / area | Purpose |
|---------------|--------|
| `Assets/Scripts/` | All C# (no sub-namespace by folder; some use `BridgeOfBlood.Data.Spells` etc.) |
| `Assets/Scripts/Spells/` | Spell authoring, cast flow, attack entities, modifications |
| `Assets/Scripts/Systems/` | Combat pipeline: collision, hit resolve, chain, damage, pierce, expiration, damage numbers, telemetry aggregation |
| `Assets/Scripts/Enemies/` | Enemy manager, movement, culling, spawner, grid, render |
| `Assets/Scripts/Data/` | Shared enums, runtime/authoring structs (Enemies, Idols, Shared) |
| `Assets/Scripts/Player/` | Player, PlayerRenderer, Stats |
| `Assets/Scripts/Editor/` | Unity editors/drawers (e.g. AttackEntityData) |
| `Assets/Shaders/` | DamageNumberUnlit, EnemyIndirectUnlit |
| `Assets/Rendering/` | Sprite rendering system (atlas-based GPU instanced) |
| `Assets/Rendering/Authoring/` | `SpriteEntityVisual` ScriptableObject (designer-facing, references Sprite) |
| `Assets/Rendering/Runtime/` | `SpriteFrame`, `SpriteRenderDatabase`, `EntityVisual`, `SpriteInstanceData` (no Sprite dependency) |
| `Assets/Rendering/Rendering/` | `SpriteInstancedRenderer`, `SpriteInstanceBuilder` (GPU instanced draw via RenderMeshIndirect) |
| `Assets/Rendering/Editor/` | `SpriteAtlasBuilder` (menu: Tools > BridgeOfBlood > Rebuild Sprite Rendering Data) |
| `Assets/Rendering/Shaders/` | `InstancedSprite.shader` (URP unlit, atlas UV remap) |
| `Assets/Rendering/Data/` | Baked `SpriteEntityVisual` assets (e.g. FireballVisual, NeedleVisual) |

---

## Sprite rendering (atlas-based)

Three-layer system: Authoring > Editor Bake > Runtime.

- **Authoring**: `SpriteEntityVisual` (ScriptableObject) holds a `Sprite` reference and `scale`. Only layer that touches `UnityEngine.Sprite`. Assigned to `EnemyAuthoringData.visual` and `AttackEntityData.visual`.
- **Bake**: `SpriteAtlasBuilder` (editor menu) finds all `SpriteEntityVisual` assets, packs sprites into a texture atlas via `Texture2D.PackTextures`, computes UV frames, writes `SpriteRenderDatabase` asset, and stamps `bakedFrameIndex` back onto each visual.
- **Runtime**: `Enemy` and `AttackEntity` structs carry `EntityVisual` (frameIndex + scale), populated from authoring data at spawn. `SpriteInstanceBuilder.Build(enemies, attacks)` builds a combined `SpriteInstanceData[]` by looking up `SpriteRenderDatabase.frames[frameIndex]`, then `SpriteInstancedRenderer.Render` issues a single `Graphics.RenderMeshIndirect` draw call. Shader `InstancedSprite.shader` remaps quad UVs via `lerp(uvRect.xy, uvRect.zw, meshUV)`.
- **Debug**: `AttackEntityDebugRenderer` still renders hitbox shapes (circles/rects) on top of sprite visuals.

Visual data flow: `SpriteEntityVisual.bakedFrameIndex` > `EnemyAuthoringData.CreateRuntimeEnemy` / `AttackEntityBuilder.Build` > `EntityVisual` on entity struct > `SpriteInstanceBuilder` > `SpriteInstancedRenderer`.

---

## Spell cast flow

1. **Entry**: `TestSceneManager` > `LoopedSpellCaster.AttemptToCastNextSpell(..., mods)` returns `SpellCastResult` (didCast, spellId, invocationCount, loopCompleted, loopCount). Optional `SpellModifications` from `castModifications`.
2. **Resolve**: `spellData.Modify(modifications)` returns a clone of `SpellAuthoringData` with modifications baked in via `SpellModificationsApplicator.CloneAndApply` (damage, crit, chains, pierce, area, flat additive). If no mods, raw spell is used.
3. **Invoke**: `SpellInvoker.StartCast(spellToCast, origin, time, spellId, spellInvocationId)` stores active cast with spell provenance; each frame `Update(simulationTime, forward)` fires keyframes.
4. **Emit**: On keyframe, `ISpellEmissionHandler.OnKeyframeFired` (implemented by `SpellEmissionHandler`) builds payload from keyframe's `AttackEntityData` via `AttackEntityBuilder.Build`, stamps `spellId`/`spellInvocationId` on payload, queues spawns with delays.
5. **Spawn**: `AttackEntityManager.Spawn(payload, position)` creates `AttackEntity` (with spell provenance) and policy lists (chain, pierce, expiration, rehit).

**Key types**: `SpellAuthoringData`, `SpellModifications`, `SpellModificationsApplicator`, `SpellInvoker`, `SpellEmissionHandler`, `AttackEntityBuilder`, `AttackEntitySpawnPayload`, `AttackEntityManager`, `AttackEntity`, `SpellCastResult`.

---

## Combat pipeline (per frame)

Order of steps in `GameSimulation._steps` (after Move/BuildGrid/Cull/RemoveCulled):

1. **AttackTick** -- Move attack entities (`AttackEntityMovementSystem`), tick time (`AttackEntityTimeSystem`).
2. **Collision** -- `CollisionSystem`: overlap attack vs enemy > `CollisionEvent`s.
3. **Damage** (step name) -- `HitResolver.Resolve` (collisions > `HitEvent`s, pierce/rehit filtering) > `ChainSystem.ResolveChains` (redirect projectiles, or exhaust chains if no target) > `DamageSystem.ProcessHits` (apply damage, crit roll, health, emit `DamageEvent`/`EnemyKilledEvent`) > `RecordRehitHits`. Then `DeadEnemyRemovalSystem` + remove dead enemies.
4. **AttackExpire** -- `PierceSystem`, `ExpirationSystem`, `ChainSystem` each `CollectRemovals` into `_attackRemovalEvents`; `AttackEntityManager.ApplyRemovals`.

**Event types**: `HitEvent`, `DamageEvent` (enriched: per-type damage, spell provenance, kill/overkill), `EnemyHitEvent`, `EnemyKilledEvent`, `AttackEntityRemovalEvent` (see `CombatEvents.cs`).

---

## Combat telemetry

Hierarchical aggregation at five time scales: Frame > Spell Cast > Spell Loop > Round > Game.

- **Source**: `DamageEvent` is the single source of truth — enriched with damage-by-type, `spellId`, `spellInvocationId`, `wasKill`, `overkillDamage`.
- **Aggregator**: `TelemetryAggregator` (plain class) iterates `DamageEvent` list each frame, builds `FrameSnapshot`, accumulates into `CombatMetrics` at each level.
- **Boundaries**: `SpellCastResult` (from `LoopedSpellCaster`) drives spell cast resets and loop completion resets. Round boundary via `EndRound()` (not yet wired).
- **Consumers**: UI reads `CurrentFrame`, `CurrentSpellCast`, `CurrentSpellLoop`, `CurrentRound`, `Game` snapshot properties.
- **Per-spell**: Each level maintains per-spell `CombatMetrics` via `Dictionary<int, CombatMetrics>`, exposed as `SpellCombatMetrics[]` on snapshot structs.

**Key types**: `CombatMetrics`, `SpellCombatMetrics`, `FrameSnapshot`, `SpellCastSnapshot`, `SpellLoopSnapshot`, `RoundSnapshot`, `GameSnapshot`, `TelemetryAggregator` (see `CombatTelemetry.cs`, `TelemetryAggregator.cs`).

---

## Damage numbers

- **Source**: `DamageSystem.ProcessHits` fills `outDamageEvents` (`DamageEvent`: position, damageDealt, enemyIndex, isCrit, plus telemetry fields).
- **Spawn**: `DamageNumberController.SpawnFromDamageEvents` > `DamageNumberManager.Spawn(position, damage, velocityX, isCrit)`. Scale from value and crit exclamation are applied in the manager.
- **Render**: `DamageNumberRenderSystem.Render(numbers, rect, camera)` -- instance buffer with per-number scale and color (yellow for crit); shader `DamageNumberUnlit.shader` uses 11-cell atlas (digits + `!`).

---

## Modifications (spell stats)

- **Authoring**: `SpellModifications` (SpellModification.cs): ParamaterModifier for crit chance/mult, chains, pierce, area, damage type/attribute scaling, flat damage, etc. `ParamaterModifier`: flatAdditiveValue, percentIncreased, moreMultipliers (percent-based). `SpellModificationResolver` in same file: `ResolveToMultiplier`, `GetFlatAdditive` (used by applicator).
- **Application**: `SpellModificationsApplicator.CloneAndApply(AttackEntityData, spellAttributeMask, mods)` -- clones entity data via `Object.Instantiate` (copies all serialized fields including `visual`) and applies all mods (damage, crit chance/mult, area, behaviors for chain/pierce). Used only from `SpellAuthoringData.Modify(modifications)` when building the spell to cast.
- **Test data**: `SpellModificationsTestData` (ScriptableObject) holds list-based authoring and `GetModifications()` builds runtime `SpellModifications`.

---

## Enemy spawning

- **Config**: `SimulationConfig.SpawnTable` (`EnemySpawnTable`) only. TestSceneManager assigns spawn table; no spawns if null. Spawn pattern is provided by the table (per entry or table fallback).
- **Spawner**: `EnemySpawner.GetSpawnEventOrigins(time)` returns event origins (left edge, random Y in local line space). Caller converts to world and applies pattern.
- **Per event**: For each origin, `EnemySpawnTable.PickEnemyByWeight(seed)` returns `EnemySpawnPick` (enemy + optional pattern). Pattern is tethered per table entry (`EnemySpawnEntry.spawnPattern`); if null, table `fallbackSpawnPattern` is used; if both null, one position at origin. `SpawnPattern.GetPositions(origin, list, seed)` fills positions; `EnemyManager.CreateEnemies(positions, authoring)`.
- **Pattern**: `SpawnPattern` (ScriptableObject): fill shape (circle/rectangle/triangle), spawn density (points per unit area), distribution (Random | Grid), optional omission zones. `SpawnShape` struct: type, center, size, rotation; helpers `GetArea()`, `Contains(point)`.
- **Editor**: `SpawnPatternEditor` (CustomEditor): edit shape/density/omissions, preview point count and points; Scene view draws fill and omission shapes plus preview points when asset is selected.

---

## Session state machine

High-level session flow managed by `SessionStateMachine` (plain class); `TestSceneManager` reads the current state each frame and dispatches accordingly.

- **States**: `Pregame` → `Round` → `Shop` or `Lose`. Pregame waits for Space/Return to start. Shop waits for N to begin next round. Lose waits for R to retry from round 1.
- **Transitions**: `RequestStart()`, `OnRoundEnded(quotaMet)`, `RequestNextRound()`, `RequestRetry()`. Each returns `bool` for whether the transition occurred.
- **Round controller**: `RoundController` (plain class) encapsulates one frame of round gameplay (player move, cast gating, items, casting, simulation steps, telemetry, damage/effect spawn, rendering, phase evaluation). Returns `RoundTickResult { roundEnded, quotaMet }`. `TestSceneManager` delegates to it when session state == Round.
- **GameState**: `GameState.sessionState` carries the current `SessionState` so UI can display Pregame/Round/Shop/Lose.

**Key types**: `SessionState` (enum), `SessionStateMachine`, `RoundController`, `RoundControllerConfig`, `RoundTickResult`.

---

## Game loop / rounds

Round-internal phase management by `RoundController`; session-level transitions handled by `SessionStateMachine` above.

- **Config**: `RoundConfig` (serializable class, embedded in `TestSceneManager`): `bloodQuota` (float), `spellLoopsPerRound` (int).
- **Phase flow**: `Playing` → (loops exhausted) → `AwaitingDespawn` → (no active casts, no pending spawns, no attack entities) → `RoundEnd` → session state machine transitions to `Shop` or `Lose`.
- **Blood tracking**: `DamageEvent.bloodExtracted` (currently `damageDealt + overkillDamage`). Aggregated through `CombatMetrics.bloodExtracted` at all telemetry levels. Quota comparison uses `TelemetryAggregator.CurrentRound.aggregate.bloodExtracted`.
- **Round end**: `TelemetryAggregator.EndRound()` is called on RoundEnd, then `RoundController.EvaluateRoundEnd()` compares blood to quota.
- **Spell loop cap**: `LoopedSpellCaster` reports `LoopCount`; `RoundController` compares against `RoundConfig.spellLoopsPerRound` externally.
- **Round reset**: `GameSimulation.ResetForNewRound()` clears enemies, attack entities, spawner, simulation time, event buffers. `LoopedSpellCaster.Reset()` + `ClearCastState()`. Player placed at right side of simulation zone.
- **Placeholders**: Shop = press N to start next round. Lose = press R to retry from round 1.

**Key types**: `RoundConfig`, `GameLoopPhase` (enum).

---

## Key data splits

- **Authoring (ScriptableObject)**: `SpellAuthoringData`, `AttackEntityData`, `EnemyAuthoringData`, `EnemySpawnTable`, `SpawnPattern`, `IdolAuthoringData`, `SpellModificationsTestData`, `SpriteEntityVisual`, `SpriteRenderDatabase`. Lives in project; not mutated at runtime for "baked" values.
- **Runtime (structs / NativeList)**: `AttackEntity`, `Enemy`, `HitEvent`, `DamageEvent`, `DamageNumber`, chain/pierce/expiration/rehit policy structs, `EntityVisual`, `SpriteFrame`, `SpriteInstanceData`, `CombatMetrics`, `SpellCastResult`. Simulation-only, no MonoBehaviours in hot path.
- **Enums / shared**: `DamageType`, `SpellAttributeMask` in `Data/Shared/Enums.cs`.

---

## Namespaces

- `BridgeOfBlood.Data.Spells`: spell/modification types, resolver, applicator, SpellAuthoringData, ResolvedKeyframe, etc.
- `BridgeOfBlood.Data.Shared`: enums, GameContext, `CombatMetrics`, snapshot structs (`FrameSnapshot`, `SpellCastSnapshot`, etc.).
- `BridgeOfBlood.Data.Enemies` / `BridgeOfBlood.Data.Idols`: runtime/authoring for enemies and idols.
- Top-level (no namespace): many systems, CombatEvents, AttackEntity*, DamageNumber*, SpellInvoker, LoopedSpellCaster, SpriteInstancedRenderer, SpriteInstanceBuilder, etc.
