# Bridge of Blood -- Codebase Map

Short reference for where systems live and how data flows. Update when adding major systems.

---

## Script layout

| Folder / area | Purpose |
|---------------|--------|
| `Assets/Scripts/` | All C# (no sub-namespace by folder; some use `BridgeOfBlood.Data.Spells` etc.) |
| `Assets/Scripts/Services/` | Session services registered on `ServiceLocator` (e.g. `ISimulationZoneService`, `ISpellModificationService`) |
| `Assets/Scripts/Spells/` | Spell authoring, cast flow, attack entities, modifications. `LoopedSpellCasterController` gathers aim (`SpellInputContext`) as an `InputReciever`. |
| `Assets/Scripts/Input/` | Pointer focus and click routing: `InputManager`, `InputActionMap`, `InputContext`, `InputReciever`. Callers process input and pass a `SpellCastRequest` into the combat tick. |
| `Assets/Scripts/Systems/` | Combat pipeline: collision, hit resolve, chain, damage, pierce, expiration, damage numbers, telemetry aggregation |
| `Assets/Scripts/Enemies/` | Enemy manager, movement, culling, spawner, grid, render |
| `Assets/Scripts/Data/` | Shared enums, runtime/authoring structs (Enemies, Idols, Shared, Shop) |
| `Assets/Scripts/Presentation/` | Combat presentation: `CombatPresentationDriver` (materials/atlas on the component), damage numbers, VFX, spell renderers (`SpellRenderer` / `SpellRenderManager`) |
| `Assets/Scripts/Player/` | Player, PlayerRenderer |
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
- **Debug**: `AttackEntityDebugRenderer` (under Presentation) still renders hitbox shapes (circles/rects) on top of sprite visuals. Rects are drawn rotated by `AttackEntity.direction`; sprite instances stay axis-aligned.

Visual data flow: `SpriteEntityVisual.bakedFrameIndex` > `EnemyAuthoringData.CreateRuntimeEnemy` / `AttackEntityModificationApplicator.BuildRolledEntity` > `EntityVisual` on entity struct > `SpriteInstanceBuilder` > `SpriteInstancedRenderer`.

**Bootstrap**: Materials and `SpriteRenderDatabase` serialize on scene/prefab `CombatPresentationDriver` (not `GameConfig`). The driver owns `CombatPresentationLayer` and draws on `SimulationCompleteEvent`; simulation never constructs or calls presentation. After `ServiceLocator` registration, bootstrap raises `ServicesRegisteredEvent` (always via `ServicesRegisteredEvent.Raise()`, never `Bus.Raise(new ...)`); presentation, brush, shop, and inventory UI bind services in response to that event. The event is **sticky**: it records its last raise in `ServicesRegisteredEvent.LastRaised`, and subscribers use `ServicesRegisteredEvent.SubscribeAndCatchUp(handler)` / `Unsubscribe(handler)` so objects spawned or enabled after bootstrap still bind immediately.

---

## Spell cast flow

1. **Entry**: `GameManager` / lab bootstrap (`LabbingScene`) create `CombatSimulationController` and register session services (`ISimulationZoneService`, inventory/shop/wallet, `CombatSimulationController`, `ISpellModificationService`) on `ServiceLocator`, then raise `ServicesRegisteredEvent`. `RoundController` and `LabbingScene` own `InputManager` and `LoopedSpellCasterController`. While casting is allowed they turn pointer clicks into one `SpellCastRequest` (requested, simulation-space origin, direction) and pass it into `CombatSimulationController.Tick`. The combat controller does not reference the input manager. Casting proceeds via `LoopedSpellCaster.AttemptToCastNextSpell(...)` → `SpellCastResult` (didCast, spellId, invocationCount, loopCompleted, loopCount, isRecast). After timing, the caster evaluates prefix mana costs (`SpellModificationsApplicator.EvaluateLoopManaCosts`) against `GameConfig.totalMana`; if the next slot would exceed the budget, the loop completes and this attempt casts the first spell of the new loop (`loopCompleted = true`, `didCast = true` unless slot 0 is also over budget). `MulticastModificationData` resolves how many invocations that one paid slot produces. Extra invocations are queued automatically at the spell's authored `recastCooldown`, do not consume mana or loop slots, and each records a unique invocation. At most one invocation runs per frame, even when `recastCooldown` is 0. The next slot remains blocked until the queue is empty and the previous slot's resolved `castCompletionDuration` has elapsed after the final recast. The frame runner raises `SpellCastEvent` for the frame's invocation so presentation/audio see recasts. Telemetry treats the paid slot plus its recasts as one spell-cast window. The caster owns cast timing, mana gating, multicast queues, and on-deck flags; it carries no `ModificationSet` of its own.
2. **Mods injection**: each frame, after item evaluation, `ISpellEmissionHandler.SetFrameModifications(mods)` injects the frame's `ModificationCatalog` (built fresh by `RoundController` / lab cast mods, treated as immutable for the rest of the frame). There is **no** `SpellAuthoringData.Modify` clone — mods are applied at spawn, not on the authoring asset. The same catalog is also published on `SimulationCompleteEvent.frameModifications` for spell preview renderers. UI (spell-tile hover) reads a resolved `ModificationSet` via `ISpellModificationService.GetSpellModCollectionForSpell` (`CombatSimulationController` implements it as `ResolveFor(spell.spellId)`), fills `SpellDescriptionUI`, then `HoverTooltip` reparents that element onto an overlay canvas and clamps it to the screen.
3. **Invoke**: `SpellInvoker.StartCast(runtime, origin, time, spellId, spellInvocationId, forward)` stores an active cast playing `runtime.Definition` directly; each frame `Update(simulationTime, forward)` fires keyframes. A non-zero forward stored on the cast is the facing; otherwise `Update`'s forward is used. `LoopedSpellCaster`, not the invoker, owns multicast scheduling and calls `StartCast` once per due invocation at the paid press origin and forward. Recasts reuse that press origin and forward.
4. **Emit**: On keyframe, `SpellEmissionHandler.OnKeyframeFired` builds an `AttackEntityBuildContext` (authoring `AttackEntityData` + spell provenance + frame mods snapshot + `attributeMask`), resolves the projectile-count mod for emit count, and queues pending contexts; transform (position/velocity/direction) is filled at flush via `ctx.WithTransform`. Facing is the emit ray; a zero direction falls back to velocity, then +X. `AttackEntity.direction` is independent of velocity so a stationary pulse can still be oriented. Local +X of a rect hitbox lies along that vector.
5. **Spawn**: `AttackEntityManager.Spawn(in AttackEntityBuildContext)` rolls stats + applies parameter mods via `AttackEntityModificationApplicator.BuildRolledEntity`, appends default policies, then each authoring `AttackEntityBehavior.ApplyTo(manager, index, mods, mask)` writes its contribution (chain/pierce/expiration/appliers/effect scalars) directly into the parallel lists by index. Attack entities do not own hit modifiers.

**Key types**: `SpellAuthoringData`, `ModificationCatalog`, `ModificationSet`, `ParameterModification`, `ParameterModificationMerger`, `SpellModificationsApplicator` (`ResolveEmitCount`, `ResolveMulticastCount`, `ResolveManaCost`), `MulticastModificationData`, `AttackEntityModificationApplicator`, `SpellInvoker`, `SpellEmissionHandler`, `AttackEntityBuildContext`, `FloatRange`/`AttackEntityBuildRngSeed` (`BridgeOfBlood.Data.Shared`), `AttackEntityManager`, `AttackEntity`, `AttackEntityBehavior`, `SpellCastResult`. There is **no** `AttackEntitySpawnPayload` — attack entities exist only as authoring data and as runtime `AttackEntity` + policy lists.

---

## Spell renderers

- **Authoring**: `SpellAuthoringData.rendererPrefab` (a `SpellRenderer`) — optional; spells with no world-space representation leave it empty.
- **Base**: `SpellRenderer` (abstract MonoBehaviour, `Presentation/`). `Bind(RuntimeSpell)` subscribes to cast/on-deck events; `Unbind()` detaches. Each frame, `SpellRenderManager` calls `SyncFrame(mods)` then `SyncOrigin(playerPosition)`. Subclasses implement `IsPreviewDirty()` / `OnPreviewRefresh()` to resolve only the preview fields they need from authoring + `ModificationSet`, rebuilding visuals when resolved values change. Cast animations read `PrimaryKeyFrame.time` from authoring.
- **Origin**: all casts emit from the player, so the base class sets `localPosition` to player position + the emitter's `RelativeToPlayerSpawnCriteria.offsetFromPlayer` and subclasses draw in local space around that. Instances are parented to the simulation zone rect, where local space is simulation space (same convention as `PlayerRenderer` / `AttackEntityDebugRenderer`).
- **Lifecycle**: `SpellRenderManager` (MonoBehaviour) binds `ISpellInventoryService` on `ServicesRegisteredEvent` and reacts to `SpellsUpdated` — instantiates/destroys instances keyed by `spellId` and syncs sibling order to loop order. On `SimulationCompleteEvent`, pushes `frameModifications.ResolveFor(spellId)` and `playerPosition` into every bound renderer.
- **Shared math**: previews and spawns use the same helpers — `SpellModificationsApplicator.ResolveEmitCount` and `AttackEntityModificationApplicator.ResolveHitBox`.
- **Reference impls**:
  - `DebugSpellRenderer` — line mesh (emit rays + hitbox outline), tinted on deck / flash on cast.
  - `HoveringSphereSpellRenderer` — sphere at half resolved AoE radius, hovering on local Z (clearance above ground contact); on cast crashes to ground contact over keyframe `time`, then rises back over `riseDuration`. Prefab: `Assets/Prefabs/SpellRenderers/HoveringSphereSpellRenderer.prefab`.

---

## Combat pipeline (per frame)

Order of steps in `GameSimulation._steps` (after Move/BuildGrid/Cull/RemoveCulled):

1. **AttackTick** -- Move attack entities (`AttackEntityMovementSystem`), tick time (`AttackEntityTimeSystem`).
2. **Collision** -- `CollisionSystem`: overlap attack vs enemy circle > `CollisionEvent`s. Every enemy has an `EnemyHurtbox.radius` (authored on `EnemyAuthoringData`, or the placement radius for map objects). Slot identity lives on `EnemyId` (`IsAlive`, `IsMapObject`, `Generation`). Broadphase query radius adds the largest live hurtbox radius, computed by scanning live slots. Spheres overlap when distance &lt;= `sphereRadius * scale + radius`. Rects overlap when the closest point on the oriented rect (local +X = `AttackEntity.direction`) is within `radius`. The rect broad-phase radius stays the half-diagonal.
3. **Damage** (step name) -- `HitResolver.Resolve` (collisions > `HitEvent`s with attack base damage/crit/provenance, pierce/rehit filtering) > `ChainSystem.ResolveChains` (redirect projectiles, or exhaust chains if no target) > `HitModifierSystem.Apply` (each catalog `HitModifier` walks the hit list and edits bases in place) > `MapObjectRegistry.ProcessHits` (map-object behaviors read post-modifier hit damage and may start spell casts) > map-object `HitModifier`s (pass-through bonus matched against a set of attack ids) > `DamageSystem.ProcessHits` (weakness, crit roll, shock, health, emit `DamageEvent`/`EnemyKilledEvent`; map-object hits increment `enemiesHit` then skip health, damage events, kills, blood, knockback, and ailments) > `KnockbackSystem.Apply` (from damage events; radial from the attack center unless `KnockbackRuntime.useEntityDirection`, which pushes along `AttackEntity.direction`) > `RecordRehitHits`. Then `MapObjectRegistry.Tick` advances map-object behaviors and flushes spell casts through `SpellInvoker` / `SpellEmissionHandler`. `DeadEnemyRemovalSystem` skips map objects.
4. **AttackExpire** -- `PierceSystem`, `ExpirationSystem`, `ChainSystem` each `CollectRemovals` into `_attackRemovalEvents`; `AttackEntityManager.ApplyRemovals`.

**Event types**: `HitEvent`, `DamageEvent` (enriched: per-type damage, spell provenance, kill/overkill), `EnemyHitEvent`, `EnemyKilledEvent`, `AttackEntityRemovalEvent` (see `CombatEvents.cs`).

After the step loop, `CombatSimulationController` raises `SimulationCompleteEvent` on `SharedGameEventBus.Bus` before `GameSimulation.ClearFrameCombatEvents()`. The payload exposes `GameSimulation.SimulationState`, frame delta, simulation time, whether time advanced, the frame's `SpellCastResult`, and `playerPosition`. `TelemetryAggregator`, `CombatPresentationDriver` / `CombatPresentationLayer`, `GameAudioManager`, `EnemyPositionVfxController` subclasses, and future completed-frame consumers subscribe instead of being called directly by the simulation runners.

---

## Combat telemetry

Hierarchical aggregation at five time scales: Frame > Spell Cast > Spell Loop > Round > Game.

- **Source**: Frame combat events (`DamageEvent`, `TickDamageEvent`, `StatusAilmentAppliedEvent`, `AttackEntityRemovalEvent`). Hits snapshot `enemyStatusAilments` at event time so intersection queries (e.g. crits on Frozen) are possible. Removals stay until `ClearFrameCombatEvents` (after `SimulationCompleteEvent`).
- **Queries**: `TelemetryStatArgs` define a query; `GetKey()` is its identity (no special keys per permutation). `TelemetryStatBase<float, TArgs>` evaluators live in `Assets/Telemetry/TelemetryStatBase.cs`.
- **Aggregator**: `TelemetryAggregator` subscribes to `SimulationCompleteEvent`, runs **subscribed** additive queries once per frame (plus per-spell when `FoldPerSpell`), and stores sparse `key → float` maps at each time scale. Spell-cast scope is one live window that opens on a paid slot press and keeps accumulating through that slot's multicast recasts (`isRecast` skips the reset). Always-on core args (hits, kills, damage-by-type, blood, ailments, duration, …) keep round flow and the debug viewer working. Extra keys come from equipped jokers/gems (`TelemetryStatValue`) rebuilt on `ItemsUpdated` / `SpellsUpdated`.
- **Derived**: DPS is not accumulated; it is `total_damage / duration` at read time.
- **Item values**: `TelemetryStatValue` serializes polymorphic `TelemetryStatArgs` and looks up `GetKey()` via `EffectContext.GetTelemetry`. For item eval, `MetricsScope.SpellCast` is remapped to the previous loop slot's per-spell totals in the Spell Loop store (forecast-safe; slot 1 reads 0). The viewer still shows the live current-cast aggregate.
- **Boundaries**: `SpellCastResult` drives loop reset. Round boundary via `EndRound()`.
- **Consumers**: Items and `RoundController` (round blood / kills) read sparse lookup. Snapshots carry window metadata (`FrameSnapshot`, …); values are keyed lookups. Telemetry Viewer dumps all subscribed keys.

**Key types**: `TelemetryStatArgs`, `TelemetryStatValue`, `TelemetryAggregator`, snapshot structs in `CombatTelemetry.cs`.

---

## Damage numbers

- **Source**: `DamageSystem.ProcessHits` fills `outDamageEvents` (`DamageEvent`: position, damageDealt, enemyIndex, isCrit, plus telemetry fields).
- **Spawn**: `CombatPresentationDriver` → `CombatPresentationLayer.HandleFrameComplete` on `SimulationCompleteEvent`, then `DamageNumberController.SpawnFromDamageEvents` > `DamageNumberManager.Spawn(position, damage, velocityX, isCrit)`. Scale from value and crit exclamation are applied in the manager.
- **Render**: Same frame-complete handler draws sprites, hitbox debug, and `DamageNumberRenderSystem.Render(numbers, rect, camera)` -- instance buffer with per-number scale and color (yellow for crit); shader `DamageNumberUnlit.shader` uses 11-cell atlas (digits + `!`).
- **Generic presentation hook**: `GameAudioManager`, `EnemyPositionVfxController` subclasses, and `CombatPresentationDriver` subscribe to `SimulationCompleteEvent` and read completed-frame buffers from the event's `SimulationState`; future presentation systems can use the same event without adding dependencies to `GameSimulation` or frame runners.

---

## Enemy-position VFX

- **Shared GPU bridge**: `EnemyPositionVfxController` owns a structured `GraphicsBuffer` of `Vector3` positions and uploads the active prefix to VFX Graph exposed properties `positions` and `count`.
- **Kill bursts**: `KillBurstVfxController` replaces the old blood-specific texture uploader. It converts frame `KillEvents` into buffer positions and calls `VisualEffect.Play()` for burst effects.
- **Ailment tracking**: `IgnitedEnemyVfxController` scans live `EnemyBuffers` slots after each advanced simulation frame, filters `StatusAilmentFlag.Ignited`, and uploads all matching enemy positions for continuous VFX tracking.

---

## Modifications (spell stats)

- **Data model**: `ModificationCatalog` is frame-scoped and owns a global `ModificationSet` plus per-runtime-spell sets. `ModificationSet` indexes `IModificationData` lists by exact runtime type; consumers request their own channel via `Get<T>()`. Scalar channels such as `ChainModificationData`, `ProjectileModificationData`, and `DamageScalingModificationData` each **contain** a `ParameterModification`; they do not inherit from the numeric payload.
- **Parameter stacking**: `ParameterModification` wraps the three authoring values (`flatAdditive`, `percentIncreased`, `moreMultiplier`). `ParameterModificationMerger.Merge` filters typed channel entries by `SpellAttributeMask` and returns `ResolvedModifier` (`flat`, `percentIncreased`, `moreCombined`). It only aggregates; each consumer owns application, units, clamping, and integer conversion.
- **Effect → catalog**: `SpellModificationEffect` serializes an `IModificationData` channel. On `Apply`, the channel bakes its `IValue<float>` fields into constants and is added to the current spell's `ModificationSet`. `ModificationCatalog.FinalizeResolution` combines global + per-spell data after item evaluation.
- **Application (spawn-time)**: attack-entity behaviors pull only their channel types and write their own runtime columns. Damage behavior combines generic + per-type scaling; chain, pierce, crit, AoE, knockback, and duration apply their own formulas. Emission consumes projectiles; the caster consumes mana cost and cast speed. `AttackEntityModificationApplicator.BuildRolledEntity` still initializes common entity data without cloning authoring assets.
- **Application (hit-time)**: `HitModifier` is an `IEffect`. Item eval adds the same instance to the current spell's `ModificationSet.hitModifiers`. `HitModifierSystem` applies each catalog modifier once: global mods touch every hit, per-spell mods only hits whose `spellId` matches. `ConditionalHitModifier` bakes its predicate on `Apply`, then walks the hit list and writes matching bases through `AttackEntityModificationApplicator.Apply` (never written back to the stored entity). `DamageSystem` then commits the modified hit.
- **Hit-time exception**: `SpellModificationProperty` remains only as the hit-scratch dispatch key for `ConditionalHitModifier`; it is no longer the spawn/item catalog key.
- **Structural/test data**: damage conversions and extra-damage-as values are typed `IModificationData` wrappers. `SpellModificationsTestData` holds SerializeReference typed data plus its existing structural authoring lists and builds a `ModificationSet`.

### Item conditions and values (Effects/)

Items use `ICondition` / `IEffect` / `IValue<float>` (all in `BridgeOfBlood.Effects`). Composable building blocks:

- **Generic condition**: `ValueCondition` (`IValue<float>` lhs + `Comparison` + `IValue<float>` rhs). Replaces all domain-specific condition classes.
- **Combat telemetry values**: `TelemetryStatValue` (scope + polymorphic `TelemetryStatArgs` + coefficient). Reads sparse aggregator keys at frame/cast/loop/round/game scopes.
- **Spell invocation values**: `SpellInvocationValue` (property + coefficient) for scalar loop counters. `SpellSlotCountValue` (attributeFilter + coefficient) counts slots matching an attribute. `SpellCastCountByAttributeValue` (attributeFilter) counts casts of spells matching an attribute. `SlotAttributeCheckValue` (slot reference + attributeFilter) returns 1/0 for whether previous/current slot matches.
- **Spell invocation context**: `SpellInvocationContext` carries scalar counters plus `IReadOnlyList<RuntimeSpell> spells` (the full spell loop inventory). Populated each frame by `RoundController.EvaluateItems()` from `LoopedSpellCaster` state. Slot numbers are 1-based.
- **Shared**: `ConstantValue` (literal float), `ConditionalEffect` (AND conditions, then run effects), `ConditionEvaluator.Compare` for numeric comparisons. Composite nodes implement `GetDescription()` for player-facing sentences (`ConditionalEffect`: "If {conditions}, then {effects}").

**Key types**: `ValueCondition`, `SpellInvocationContext`, `SpellInvocationProperty`, `SpellInvocationResolver`, `SpellInvocationValue`, `SpellSlotCountValue`, `SpellCastCountByAttributeValue`, `SlotAttributeCheckValue`, `TelemetryStatValue` (see `Value.cs`, `Assets/Telemetry/TelemetryStatBase.cs`).

---

## Enemy spawning

- **Contract**: `IEnemySpawner.CollectSpawnRequests(time, playfield)` returns `EnemySpawnRequest` batches (`enemy` + playfield-local `positions`). `GameSimulation` only calls `EnemyManager.CreateEnemies` per request.
- **Runtime identity/storage**: `EnemyManager` stores enemies in stable SoA slots. Removing an enemy marks its slot dead, adds the index to a free list, and does not swap later rows. Reusing a slot increments its generation; combat and ailment code carry `EntityId { Index, Generation }` and validate generation + alive state before direct slot lookup. `EnemyCount` is live count; `SpawnedThisRound` is cumulative creates since `Clear`; `EnemyBuffers.Length`/`SlotCount` includes tombstones. Slot metadata is `EnemyId` (`IsAlive`, `IsMapObject`, `Generation`). Each live enemy is a circle via `EnemyHurtbox.radius`, authored on `EnemyAuthoringData.collisionRadius`. `CreateMapObject` writes a map-object slot (placement radius, stationary, positive health) and does **not** increment `SpawnedThisRound`. Map objects skip movement, right-edge culling, death removal, health loss, and kill/blood telemetry. Their hits still spend pierce, chain, and rehit.
- **Ownership**: Each spawner implementation owns an `EnemySpawnTable` (serialized on the spawner). The active spawner lives on `RoundConfig.spawner` (`GameConfig.roundConfigs`), applied via `GameSimulation.ApplyRoundConfig` before each round reset.
- **EnemySpawner** (rate / left edge): per spawn event, table pick + `SpawnPattern` expansion + `positionScale` (private resolve on the spawner).
- **BrushStrokeEnemySpawner** (lab): brush circle fill is final placement; one table pick per drained batch; no `SpawnPattern` pass.
- **NoiseSheetEnemySpawner** (one-shot field): samples its layered noise grid and emits all matching positions on the first `CollectSpawnRequests` after `Reset`; subsequent collects return no requests. `GameSimulation.FlushSpawnerNow` drains this first batch immediately and rebuilds the grid for pre-simulation map mutations.
- **Round scaling**: `EnemyAuthoringData.WriteRuntimeColumnsAt` multiplies rolled move speed and health by the active round's `enemyMoveSpeedMultiplier` / `enemyHealthMultiplier`.
- **Pattern**: `SpawnPattern` (ScriptableObject): fill shape (circle/rectangle/triangle), spawn density (points per unit area), distribution (Random | Grid), optional omission zones. `SpawnShape` struct: type, center, size, rotation; helpers `GetArea()`, `Contains(point)`.
- **Editor**: `SpawnPatternEditor` (CustomEditor): edit shape/density/omissions, preview point count and points; Scene view draws fill and omission shapes plus preview points when asset is selected.

---

## Session state machine

High-level session flow is owned by `SessionFlowController`. `GameManager` ticks it each frame and copies `CurrentState` into `GameState`.

- **States**: `SessionState` is `Pregame`, `Round`, `Shop`, `MapMod`, `Lose`. The live machine registers `RoundController`, `ShopSessionPhase`, and `MapModSessionPhase`, and starts in shop. Pregame and lose phases exist but are not registered.
- **Context**: each phase defines a nested `Context`. `RoundController.Context` carries the runtime `GameConfig`. `MapModSessionPhase.Context` carries a positive placement count. Shop, lose, and pregame contexts are empty.
- **Transitions**: `EnterRound`, `EnterShop`, and `EnterMapMod` accept only that phase's context. A request is applied at the start of the next `Tick` (exit the active phase, then enter the destination). Requesting the phase that is already active does not re-enter. There is no shared session context and no cast.
- **Shop**: the continue button or N enters round with the live runtime config. A map-mod purchase enters map mod with that placement count.
- **Round**: `RoundController` gates casts, evaluates the kill quota, then enters shop.
- **Map mod**: returns to shop when the placement budget is exhausted.
- **GameState**: `GameState.sessionState` carries the current `SessionState` so UI can display the phase.

**Key types**: `SessionState`, `SessionFlowController`, `SessionPhaseBase<TContext>`, `RoundController`, `ShopSessionPhase`, `MapModSessionPhase`.

Session phase enter/exit events and post-simulation frame notifications use the shared generic event bus:

- **Bus**: `SharedGameEventBus` wraps `GenericEventBus<IEvent>`.
- **Events**: `RoundEnterEvent`, `RoundExitEvent`, `ShopEnterEvent`, `ShopExitEvent` in `Assets/Scripts/Data/Shared/SessionPhaseEvents.cs`; `SimulationCompleteEvent` and `SpellCastEvent` in `Assets/Scripts/Systems/`.
- **Unity bridge**: `SharedGameEventReceiver` lets prefabs select a concrete `IEvent` listener via `[SerializeReference]` and wire a `UnityEvent<TEvent>`.

---

## Game loop / rounds

Round-internal phase management by `RoundController`; session-level transitions handled by `SessionFlowController` above.

- **Config**: `GameConfig` (ScriptableObject): authoring asset holds `roundConfigs` + wallet/inventory **templates** (no materials/presentation). Combat visuals serialize on scene/prefab `CombatPresentationDriver`. At session start (and Lose → Retry), `GameConfig.CreateRuntimeCopy` **`Instantiate`s** the whole config plus unique wallet/inventory clones; gameplay reads **`runtimeGameConfig.playerWallet`**, **`playerInventory`**, and round fields from that **one** session clone (`GameConfig.DestroyRuntimeCopy` on teardown / rebuild). `SimulationConfig` on the asset holds gizmos/spawn-rate knobs only — playfield size lives on each `RoundConfig`.
- **RoundConfig**: each entry (`class`) has `spawner`, `killQuotaPercent`, `enemyMoveSpeedMultiplier`, `enemyHealthMultiplier`, and `simulationSize` (`Vector2` width/height). `ResolvePlayfield()` builds sim space as x = 0..width, y = ±height/2. `GameConfig.GetRoundConfig(roundNumber)` is 1-based and clamps to the last list entry. `RoundController` / lab apply it through `CombatSimulationController.ApplyRoundConfig` before `ResetForNewRound`, which sets `GameSimulation` playfield and rebuilds the enemy spatial grid. `GameConfigEditor` duplicates rounds by cloning the spawner object (Unity aliases `[SerializeReference]` when this type is a struct).
- **Per-round quota**: `RoundController` resolves `MinEnemiesKilled` as `ceil(spawnedThisRound × killQuotaPercent / 100)` from `EnemyManager.SpawnedThisRound`. Round success is round `event_count:Kills` >= `MinEnemiesKilled` (`QuotaBasedRoundEndStrategy`).
- **Phase flow**: `Playing` → (loops exhausted) → `AwaitingDespawn` → (no active casts, no pending spawns, no attack entities) → `RoundEnd` → session state machine transitions to `Shop` or `Lose`.
- **Blood tracking**: `DamageEvent.bloodExtracted` (currently `damageDealt + overkillDamage`). Aggregated through the `blood_extracted` telemetry key at all scopes (display/items; not the round-win check).
- **Round end**: `TelemetryAggregator.EndRound()` is called on RoundEnd, then `IRoundEndStrategy.Evaluate` compares round kills to the resolved kill quota.
- **Spell loop cap**: `LoopedSpellCaster` reports `LoopCount`; `RoundController` compares against `SpellLoopsPerRound` (from `GameConfig.maxSpellLoopsPerRound`). Loop mana budget is `GameConfig.totalMana` on the session clone.
- **Round reset**: `GameSimulation.ResetForNewRound()` clears enemies, attack entities, spawner, simulation time, event buffers. `LoopedSpellCaster.Reset()` + `ClearCastState()`. Player placed at right side of simulation zone.
- **Lab**: `LabbingScene` IMGUI prev/next at the top of the screen iterates `roundConfigs`, applies the selected config, resets combat, and re-raises `ServicesRegisteredEvent` so brush spawners rebind.
- **Session start / retry**: `GameManager` replaces `_runtimeGameConfig` via `GameConfig.CreateRuntimeCopy`; `RoundController` receives the clone and uses `gameConfig.playerInventory` for items. Lose → Retry calls `RoundController.SetGameConfig` with the new clone.
- **Placeholders**: Shop = press N to start next round. Lose = press R to retry from round 1.

**Key types**: `GameConfig`, `RoundConfig`, `GameLoopPhase` (enum).

---

## Map modifiers (standalone authoring scene)

- **Scene**: `Assets/Scenes/MapModScene.unity` is a LabScene-derived placement surface driven by `MapModScene`. It owns `GameSimulation` directly: no session state, round events, shop/wallet services, spell inventory, item evaluation, or spell modifications.
- **Rebuild/apply sequence**: `ResetForNewRound` → `FlushSpawnerNow` → `MapModifierApplicator.ApplyMapModifiers` (stable `index` order, yield-then-fast-forward) → normal simulation steps. The active map configuration must use `NoiseSheetEnemySpawner`, so the flush creates the full field and consumes its one-shot spawn before modifiers query enemies.
- **Settle modes**: Replay (round start, rebuild) fast-forwards waits via `CombatSimWaitRunner.Run` so prior placements look like permanent map state. A newly committed placement plays out in `MapModifierPlacementHost.PlayOutAsync`: each unsatisfied `CombatSimWait` awaits an `AwaitableCompletionSource` that `Tick` completes after `CombatSimWaitRunner.StepOnce` and `SimulationCompleteEvent`. Placement and catalog input stay locked until that `Awaitable` finishes. Session budget-exhaustion waits for that play-out before leaving MapMod. Factories: `OneTick`, `AttackEntitiesDespawned`, `KnockbackSettled`.
- **Runtime contract**: `MapModifierContext` exposes required `AttackEntityManager`, `EnemyManager`, `MapObjectRegistry`, and playfield data. `MapModifier.Apply` yields `CombatSimWait`s. Replay runs each wait to completion before the next modifier; placement play-out awaits one simulation tick per unsatisfied check.
- **Attack-entity modifier**: `AttackEntitySpawnMapModifier` uses a point or directed `IAttackEntityEmitter` to build `AttackEntityBuildContext` with environmental provenance (`spellId = 0`, no `ModificationSet`) and spawn directly through `AttackEntityManager`. Directed emit copies the placement facing onto `AttackEntity.direction` even when authored velocity is zero. Authored `MapModifierSettleStep[]` is yielded after emit (`FreezeMapModifier`: despawn; `KnockBackMapModifier`: despawn then knockback settle).
- **Combine modifier**: `EnemyCombineMapModifier` broadphase-queries a circle, exact-distance filters live slots, sums current/max health into one survivor, moves it to the centroid, tombstones the absorbed enemies, then rebuilds the grid (no wait). Map objects are left in place.
- **Map object modifier**: `MapObjectMapModifier` spawns a permanent circular enemy via `EnemyManager.CreateMapObject` and registers cloned `MapObjectBehavior`s on `MapObjectRegistry`. Placement is point-mode with an authored radius. The click commits only when the circle does not overlap a live enemy circle (`distance < objectRadius + hurtbox.radius`); a blocked preview draws red. Behaviors are `[SerializeReference]` subclasses with `OnHit` and `Tick`. Every X hits casts an authored spell Y times. The first hit from an attack records its id; a `PassThroughDamageHitModifier` then adds flat damage to hits whose attack id is in that set. Every X observed damage casts an authored spell Y times. Casts play through `SpellInvoker` and `SpellEmissionHandler` from the object position. `Tick` runs every simulation frame so behaviors can act over time. Replay respawns the object after each round reset.
- **Authoring/placement**: `MapModifierDefinition` assets under `Resources/MapModifiers/` are loaded at placement start. Point, two-click directed, and resizable-region controllers map LMB through `SimulationZonePointer`. Directed placement locks the origin on the first click and commits facing (origin toward the second click) on the second click. Committed modifiers are appended with collection-order indices. `MapModifierController` uses one shared `MapModifierUI` prefab for every runtime type.
- **Session visit**: `MapModifierPlacementTicket.OnPurchase` raises `MapModPlacementRequested` with its placement count. `RepositoryShopService` leaves the ticket on the shelf. `ShopSessionPhase` enters `MapMod` with `MapModSessionPhase.Context` built from that count. `MapModSessionPhase` reads the count on enter. `IMapModifierSessionService` only stores placed modifiers so later rounds and visits can replay them.
- **Sample**: `MapFreezeAttackEntity` has a circular hitbox, 0.01 cold damage (required by the current `DamageEvent`-driven ailment pipeline), guaranteed `ApplyFrozenBehavior`, and short expiration. `FreezeMapModifier` places it as a stationary environmental pulse. `MapDirectionalKnockbackEntity` is a stationary 100×20 rect (`HitBoxData.rectDimension.x` along facing) with `KnockbackBehavior.useEntityDirection`, 0.1 physical damage, and a one-frame expiration. `DirectionalKnockbackMapModifier` places it with two-click facing and waits for despawn, then knockback settle. The older circular `KnockBackMapModifier` stays radial.
- **Presentation**: `CombatPresentationDriver.InitializeStandalone` binds the map simulation directly without publishing `ServicesRegisteredEvent`. `SimulationRectRenderer.SyncFrom` receives the playfield directly instead of a `RoundEnterEvent`.

**Key types**: `MapModScene`, `MapModifier`, `MapModifierApplicator`, `CombatSimWait`, `CombatSimWaitRunner`, `MapModifierDefinition`, `MapModifierPlacementController`, `AttackEntitySpawnMapModifier`, `EnemyCombineMapModifier`, `MapObjectMapModifier`, `MapObjectBehavior`, `MapObjectRegistry`.

---

## Shop system

Shop item definitions, weighted selection, and purchase flow.

- **Data**: `Assets/Scripts/Data/Shop/` (namespace `BridgeOfBlood.Data.Shop`). `ShopItemDefinition` (ScriptableObject, implements `IRandomElement`): display name, description, price, currency type, resell value, rarity, shop item type, weight, and a reference to an `IPurchasable` payload. `ShopConfig` (ScriptableObject): per-`ShopItemType` spawn weights via `ShopItemTypeWeight` entries (also `IRandomElement`). `IPurchasable` interface: `OnPurchase(PurchaseContext)`.
- **Repository**: `ShopRepository` (plain class): loads all `ShopItemDefinition` assets from `Resources/ShopItems/`, groups by `ShopItemType`, exposes `GetAll()` and `PickItem(typeRoll, itemRoll)` for two-step weighted selection (type first, then item within type).
- **Assets**: `Assets/Resources/ShopItems/` holds `ShopItemDefinition` assets for `Resources.LoadAll` discovery.

**Key types**: `ShopItemDefinition`, `ShopConfig`, `ShopRepository`, `IPurchasable`, `PurchaseContext`, `ShopItemType`, `Rarity`, `CurrencyType`.

---

## Inventory transfer (receptacles + occupants)

Runtime inventory is three typed collections plus a session stash, owned by `PlayerInventory` (cloned with the session `GameConfig`).

- **Occupants** (`IInventoryOccupant`): `RuntimeSpell`, `RuntimeGem`, `RuntimeItem`. Occupancy (`OccupancyCount`, `TileSideLength`, `GhostSprite`) only — no drop dispatch.
- **Collections**: typed bags. `SpellCollection.TryInsert` / `TryRemove` take spells; `ItemCollection` takes jokers; `RuntimeSpellGemCollection.TryInsert` enforces `CanApplyToSpell` + free socket; `Stash.TryPlace` is a type-blind grid (default 8×4).
- **UI receptacles** (`IItemReceptacle` on controllers/presenters): `VisitSpell` / `VisitGem` / `VisitItem` own admission and pointer-to-slot. `SpellInventoryController` takes spells; `ItemInventoryController` takes jokers; `SpellGemPresenter` takes gems; `StashCellPresenter` takes any occupant. Dropping a spell on stash unpacks socketed gems, then places the spell.
- **Transfer**: `InventoryTransferCoordinator` (ghost on cursor, drop preview). Protocol: preview `Accept(Commit: false)`, then extract from source and `Accept(Commit: true)`; restore to source if the commit fails.
- **Shop**: gem click-to-target is unchanged. Untargeted gem purchases go to the Stash via `TryPlace`. Jokers go to `ItemCollection`. `InventoryItem` was removed.

**Key types**: `IInventoryOccupant`, `IItemReceptacle`, `ReceptacleDropContext`, `Stash`, `ItemCollection`, `RuntimeSpellGemCollection`, `RuntimeItem`, `RuntimeGem`, `InventoryTransferCoordinator`.

---

## Weighted selection (generic probability)

Reusable probability primitives in `BridgeOfBlood.Data.Shared`.

- **`IRandomElement`**: interface with `float Weight { get; set; }`. Implemented by `ShopItemDefinition`, `ShopConfig.ShopItemTypeWeight`, and any future weighted asset.
- **`WeightedSelection`**: static utility with `Pick`, `TotalWeight`, `Normalize` operating on any `IReadOnlyList<T> where T : IRandomElement`.
- **Editor**: `ProbabilityEditorWindow` (menu: Tools > Bridge of Blood > Probability Editor): folder picker, weight editing, percentage display, normalize button. `ScriptableObjectImplementsDrawer` + `ScriptableObjectImplementsPickerWindow`: filtered asset list for `[ScriptableObjectImplements(typeof(I))]` fields (Unity's object picker cannot filter by interface).

---

## Key data splits

- **Authoring (ScriptableObject)**: `GameConfig`, `PlayerWallet`, `PlayerInventory`, `SpellAuthoringData`, `AttackEntityData`, `EnemyAuthoringData`, `EnemySpawnTable`, `SpawnPattern`, `IdolAuthoringData`, `SpellModificationsTestData`, `SpriteEntityVisual`, `SpriteRenderDatabase`, `ShopItemDefinition`, `ShopConfig`. Lives in project; session uses **`Instantiate`** copies of wallet/inventory templates so templates stay read-only on disk.
- **Runtime (structs / NativeList)**: `AttackEntity`, `Enemy`, `HitEvent`, `DamageEvent`, `DamageNumber`, chain/pierce/expiration/rehit policy structs, `EntityVisual`, `SpriteFrame`, `SpriteInstanceData`, `SpellCastResult`. Simulation-only, no MonoBehaviours in hot path.
- **Enums / shared**: `DamageType`, `SpellAttributeMask`, `Rarity`, `CurrencyType`, `ShopItemType` in `Data/Shared/Enums.cs`. `IRandomElement`, `WeightedSelection` in `Data/Shared/`.

---

## Namespaces

- `BridgeOfBlood.Data.Spells`: spell/modification types, applicator, SpellAuthoringData, ResolvedKeyframe, etc.
- `BridgeOfBlood.Data.Shared`: enums, `GameConfig`, `RoundConfig`, GameContext, snapshot structs (`FrameSnapshot`, `SpellCastSnapshot`, etc.).
- `BridgeOfBlood.Data.Enemies` / `BridgeOfBlood.Data.Idols`: runtime/authoring for enemies and idols.
- `BridgeOfBlood.Data.Inventory`: `PlayerInventory`, `PlayerWallet`, `ItemCollection`, `Stash`, `IInventoryOccupant`.
- `BridgeOfBlood.Data.Shop`: shop item definitions, config, repository, `IPurchasable`.
- Top-level (no namespace): many systems, CombatEvents, AttackEntity*, DamageNumber*, SpellInvoker, LoopedSpellCaster, SpriteInstancedRenderer, SpriteInstanceBuilder, etc.
