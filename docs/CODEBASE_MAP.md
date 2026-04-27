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
| `Assets/Scripts/Data/` | Shared enums, runtime/authoring structs (Enemies, Idols, Shared, Shop) |
| `Assets/Scripts/Player/` | Player, PlayerRenderer, Stats |
| `Assets/Scripts/Editor/` | Unity editors/drawers (e.g. AttackEntityData, `ScriptableObjectImplementsDrawer`, Probability Editor) |
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
4. **Emit**: On keyframe, `ISpellEmissionHandler.OnKeyframeFired` (implemented by `SpellEmissionHandler`) builds payload from keyframe's `AttackEntityData` via `AttackEntityBuilder.Build` (`AttackEntityBuildContext`: deterministic roll per keyframe from `FloatRange` damage/crit fields), stamps `spellId`/`spellInvocationId` on payload, queues spawns with delays.
5. **Spawn**: `AttackEntityManager.Spawn(payload, position)` creates `AttackEntity` (with spell provenance) and policy lists (chain, pierce, expiration, rehit).

**Key types**: `SpellAuthoringData`, `SpellModifications`, `SpellModificationsApplicator`, `SpellInvoker`, `SpellEmissionHandler`, `AttackEntityBuilder`, `AttackEntityBuildContext`, `FloatRange` (`BridgeOfBlood.Data.Shared`), `AttackEntitySpawnPayload`, `AttackEntityManager`, `AttackEntity`, `SpellCastResult`.

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

- **Data model**: `SpellModifications` (SpellModification.cs): `Dictionary<SpellModificationProperty, List<ParameterModifier>>` keyed by property. `ParameterModifier` (SpellModificationModifier.cs) carries `property`, `SpellAttributeMask filter`, and three `IValue<float>` fields (`flatAdditive`, `percentIncreased`, `moreMultiplier`). Structural lists for `FlatDamage`, `DamageConversion`, `ExtraDamageAs` remain separate. `SpellModificationProperty` enum covers core stats (CritChance..Projectiles), generic `DamageScaling`, per-type scaling (Physical/Cold/Fire/LightningDamageScaling), and per-type penetration.
- **Effect → pool**: `SpellModificationEffect` serializes a `ParameterModifier` with `IValue<float>` fields. On `Apply`, it eagerly resolves (bakes) each `IValue` into a `ConstantValue` wrapper, then adds the baked modifier to `SpellModifications.Add(...)`.
- **Resolution**: `SpellModificationsApplicator.Resolve(mods, property, mask)` returns a `ResolvedModifier` (flat, percentIncreased, moreCombined). The `Multiplier` property computes `(1 + pct/100) * moreCombined`. Used by `CloneAndApply` and behaviors (chain, pierce).
- **Application**: `SpellModificationsApplicator.CloneAndApply(AttackEntityData, spellAttributeMask, mods)` -- clones entity data via `Object.Instantiate` and applies all resolved mods (damage scaling, crit, area, behaviors for chain/pierce).
- **Test data**: `SpellModificationsTestData` (ScriptableObject) holds `List<ParameterModifier>` plus structural lists; `GetModifications()` builds runtime `SpellModifications`.

### Item conditions and values (Effects/)

Items use `ICondition` / `IEffect` / `IValue<float>` (all in `BridgeOfBlood.Effects`). Composable building blocks:

- **Generic condition**: `ValueCondition` (`IValue<float>` lhs + `Comparison` + `IValue<float>` rhs). Replaces all domain-specific condition classes.
- **Combat metric values**: `CombatMetricValue` (scope + property + coefficient). Reads `CombatMetrics` from `EffectContext` at frame/cast/loop/round/game scopes.
- **Spell invocation values**: `SpellInvocationValue` (property + coefficient) for scalar loop counters. `SpellSlotCountValue` (attributeFilter + coefficient) counts slots matching an attribute. `SpellCastCountByAttributeValue` (attributeFilter) counts casts of spells matching an attribute. `SlotAttributeCheckValue` (slot reference + attributeFilter) returns 1/0 for whether previous/current slot matches.
- **Spell invocation context**: `SpellInvocationContext` carries scalar counters plus `IReadOnlyList<RuntimeSpell> spells` (the full spell loop inventory). Populated each frame by `RoundController.EvaluateItems()` from `LoopedSpellCaster` state. Slot numbers are 1-based.
- **Shared**: `ConstantValue` (literal float), `ConditionalEffect` (AND conditions, then run effects), `ConditionEvaluator.Compare` for numeric comparisons.

**Key types**: `ValueCondition`, `SpellInvocationContext`, `SpellInvocationProperty`, `SpellInvocationResolver`, `SpellInvocationValue`, `SpellSlotCountValue`, `SpellCastCountByAttributeValue`, `SlotAttributeCheckValue`, `CombatMetricValue` (see `Effect.cs`, `Value.cs`).

---

## Enemy spawning

- **Config**: `SimulationConfig.SpawnTable` (`EnemySpawnTable`) only. TestSceneManager assigns spawn table; no spawns if null. Spawn pattern is provided by the table (per entry or table fallback).
- **Spawner**: `EnemySpawner.GetSpawnEventOrigins(time)` returns event origins (left edge, random Y in local line space). Caller converts to world and applies pattern.
- **Per event**: For each origin, `EnemySpawnTable.PickEnemyByWeight(seed)` returns `EnemySpawnPick` (enemy + optional pattern). Pattern is chosen uniformly from `EnemySpawnEntry.spawnPatterns` per pick; if empty, table `fallbackSpawnPattern` is used; if both empty, one position at origin. `SpawnPattern.GetPositions(origin, list, seed)` fills positions; `EnemyManager.CreateEnemies(positions, authoring)`.
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

- **Config**: `GameConfig` (ScriptableObject): authoring asset holds round tuning + wallet/inventory **templates**. At session start (and Lose → Retry), `GameConfig.CreateRuntimeCopy` **`Instantiate`s** the whole config plus unique wallet/inventory clones; gameplay reads **`runtimeGameConfig.playerWallet`**, **`playerInventory`**, and scaling fields from that **one** session clone (`GameConfig.DestroyRuntimeCopy` on teardown / rebuild).
- **Per-round quota**: `RoundController` sets `BloodQuota` from `gameConfig.bloodQuotaScaling.BuildForRound(RoundNumber)` on construct, `StartNextRound`, and `Retry`.
- **Phase flow**: `Playing` → (loops exhausted) → `AwaitingDespawn` → (no active casts, no pending spawns, no attack entities) → `RoundEnd` → session state machine transitions to `Shop` or `Lose`.
- **Blood tracking**: `DamageEvent.bloodExtracted` (currently `damageDealt + overkillDamage`). Aggregated through `CombatMetrics.bloodExtracted` at all telemetry levels. Quota comparison uses `TelemetryAggregator.CurrentRound.aggregate.bloodExtracted`.
- **Round end**: `TelemetryAggregator.EndRound()` is called on RoundEnd, then `RoundController.EvaluateRoundEnd()` compares blood to quota.
- **Spell loop cap**: `LoopedSpellCaster` reports `LoopCount`; `RoundController` compares against `SpellLoopsPerRound` (from `GameConfig.maxSpellLoopsPerRound`).
- **Round reset**: `GameSimulation.ResetForNewRound()` clears enemies, attack entities, spawner, simulation time, event buffers. `LoopedSpellCaster.Reset()` + `ClearCastState()`. Player placed at right side of simulation zone.
- **Session start / retry**: `TestSceneManager` replaces `_runtimeGameConfig` via `GameConfig.CreateRuntimeCopy`; `RoundController` receives the clone and uses `gameConfig.playerInventory` for items. Lose → Retry calls `RoundController.SetGameConfig` with the new clone.
- **Placeholders**: Shop = press N to start next round. Lose = press R to retry from round 1.

**Key types**: `GameConfig`, `BloodQuotaScaling`, `RoundRuntimeData`, `GameLoopPhase` (enum).

---

## Shop system

Shop item definitions, weighted selection, and purchase flow.

- **Data**: `Assets/Scripts/Data/Shop/` (namespace `BridgeOfBlood.Data.Shop`). `ShopItemDefinition` (ScriptableObject, implements `IRandomElement`): display name, description, price, currency type, resell value, rarity, shop item type, weight, and a reference to an `IPurchasable` payload. `ShopConfig` (ScriptableObject): per-`ShopItemType` spawn weights via `ShopItemTypeWeight` entries (also `IRandomElement`). `IPurchasable` interface: `OnPurchase(PurchaseContext)`.
- **Repository**: `ShopRepository` (plain class): loads all `ShopItemDefinition` assets from `Resources/ShopItems/`, groups by `ShopItemType`, exposes `GetAll()` and `PickItem(typeRoll, itemRoll)` for two-step weighted selection (type first, then item within type).
- **Assets**: `Assets/Resources/ShopItems/` holds `ShopItemDefinition` assets for `Resources.LoadAll` discovery.

**Key types**: `ShopItemDefinition`, `ShopConfig`, `ShopRepository`, `IPurchasable`, `PurchaseContext`, `ShopItemType`, `Rarity`, `CurrencyType`.

---

## Weighted selection (generic probability)

Reusable probability primitives in `BridgeOfBlood.Data.Shared`.

- **`IRandomElement`**: interface with `float Weight { get; set; }`. Implemented by `ShopItemDefinition`, `ShopConfig.ShopItemTypeWeight`, and any future weighted asset.
- **`WeightedSelection`**: static utility with `Pick`, `TotalWeight`, `Normalize` operating on any `IReadOnlyList<T> where T : IRandomElement`.
- **Editor**: `ProbabilityEditorWindow` (menu: Tools > Bridge of Blood > Probability Editor): folder picker, weight editing, percentage display, normalize button. `ScriptableObjectImplementsDrawer` + `ScriptableObjectImplementsPickerWindow`: filtered asset list for `[ScriptableObjectImplements(typeof(I))]` fields (Unity's object picker cannot filter by interface).

---

## Key data splits

- **Authoring (ScriptableObject)**: `GameConfig`, `PlayerWallet`, `PlayerInventory`, `SpellAuthoringData`, `AttackEntityData`, `EnemyAuthoringData`, `EnemySpawnTable`, `SpawnPattern`, `IdolAuthoringData`, `SpellModificationsTestData`, `SpriteEntityVisual`, `SpriteRenderDatabase`, `ShopItemDefinition`, `ShopConfig`. Lives in project; session uses **`Instantiate`** copies of wallet/inventory templates so templates stay read-only on disk.
- **Runtime (structs / NativeList)**: `AttackEntity`, `Enemy`, `HitEvent`, `DamageEvent`, `DamageNumber`, chain/pierce/expiration/rehit policy structs, `EntityVisual`, `SpriteFrame`, `SpriteInstanceData`, `CombatMetrics`, `SpellCastResult`. Simulation-only, no MonoBehaviours in hot path.
- **Enums / shared**: `DamageType`, `SpellAttributeMask`, `Rarity`, `CurrencyType`, `ShopItemType` in `Data/Shared/Enums.cs`. `IRandomElement`, `WeightedSelection` in `Data/Shared/`.

---

## Namespaces

- `BridgeOfBlood.Data.Spells`: spell/modification types, applicator, SpellAuthoringData, ResolvedKeyframe, etc.
- `BridgeOfBlood.Data.Shared`: enums, `GameConfig`, `BloodQuotaScaling`, `RoundRuntimeData`, GameContext, `CombatMetrics`, snapshot structs (`FrameSnapshot`, `SpellCastSnapshot`, etc.).
- `BridgeOfBlood.Data.Enemies` / `BridgeOfBlood.Data.Idols`: runtime/authoring for enemies and idols.
- `BridgeOfBlood.Data.Shop`: shop item definitions, config, repository, `IPurchasable`.
- Top-level (no namespace): many systems, CombatEvents, AttackEntity*, DamageNumber*, SpellInvoker, LoopedSpellCaster, SpriteInstancedRenderer, SpriteInstanceBuilder, etc.
