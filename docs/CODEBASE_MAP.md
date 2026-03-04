# Bridge of Blood – Codebase Map

Short reference for where systems live and how data flows. Update when adding major systems.

---

## Script layout

| Folder / area | Purpose |
|---------------|--------|
| `Assets/Scripts/` | All C# (no sub-namespace by folder; some use `BridgeOfBlood.Data.Spells` etc.) |
| `Assets/Scripts/Spells/` | Spell authoring, cast flow, attack entities, modifications |
| `Assets/Scripts/Systems/` | Combat pipeline: collision, hit resolve, chain, damage, pierce, expiration, damage numbers |
| `Assets/Scripts/Enemies/` | Enemy manager, movement, culling, spawner, grid, render |
| `Assets/Scripts/Data/` | Shared enums, runtime/authoring structs (Enemies, Idols, Shared) |
| `Assets/Scripts/Player/` | Player, PlayerRenderer, Stats |
| `Assets/Scripts/Editor/` | Unity editors/drawers (e.g. AttackEntityData) |
| `Assets/Shaders/` | DamageNumberUnlit, EnemyIndirectUnlit |

---

## Spell cast flow

1. **Entry**: `TestSceneManager` → `LoopedSpellCaster.AttemptToCastNextSpell(..., mods)` with optional `SpellModifications` from `castModifications` (SpellModificationsTestData).
2. **Resolve**: `spellData.Modify(modifications)` returns a clone of `SpellAuthoringData` with modifications baked in via `SpellModificationsApplicator.CloneAndApply` (damage, crit, chains, pierce, area, flat additive). If no mods, raw spell is used.
3. **Invoke**: `SpellInvoker.StartCast(spellToCast, origin, time)` stores active cast; each frame `Update(simulationTime, forward)` fires keyframes.
4. **Emit**: On keyframe, `ISpellEmissionHandler.OnKeyframeFired` (implemented by `SpellEmissionHandler`) builds payload from keyframe’s `AttackEntityData` via `AttackEntityBuilder.Build`, queues spawns with delays.
5. **Spawn**: `AttackEntityManager.Spawn(payload, position)` creates `AttackEntity` and policy lists (chain, pierce, expiration, rehit).

**Key types**: `SpellAuthoringData`, `SpellModifications`, `SpellModificationsApplicator`, `SpellInvoker`, `SpellEmissionHandler`, `AttackEntityBuilder`, `AttackEntitySpawnPayload`, `AttackEntityManager`, `AttackEntity`.

---

## Combat pipeline (per frame)

Order of steps in `TestSceneManager._steps` (after Move/BuildGrid/Cull/RemoveCulled):

1. **AttackTick** – Move attack entities (`AttackEntityMovementSystem`), tick time (`AttackEntityTimeSystem`).
2. **Collision** – `CollisionSystem`: overlap attack vs enemy → `CollisionEvent`s.
3. **Damage** (step name) – `HitResolver.Resolve` (collisions → `HitEvent`s, pierce/rehit filtering) → `ChainSystem.ResolveChains` (redirect projectiles, or exhaust chains if no target) → `DamageSystem.ProcessHits` (apply damage, crit roll, health, emit `DamageEvent`/`EnemyKilledEvent`) → `RecordRehitHits`. Then `DeadEnemyRemovalSystem` + remove dead enemies.
4. **AttackExpire** – `PierceSystem`, `ExpirationSystem`, `ChainSystem` each `CollectRemovals` into `_attackRemovalEvents`; `AttackEntityManager.ApplyRemovals`.

**Event types**: `HitEvent`, `DamageEvent`, `EnemyHitEvent`, `EnemyKilledEvent`, `AttackEntityRemovalEvent` (see `CombatEvents.cs`).

---

## Damage numbers

- **Source**: `DamageSystem.ProcessHits` fills `outDamageEvents` (`DamageEvent`: position, damageDealt, enemyIndex, isCrit).
- **Spawn**: `TestSceneManager.SpawnDamageNumbersFromEvents` → `DamageNumberManager.Spawn(position, damage, velocityX, isCrit)`. Scale from value and crit exclamation are applied in the manager.
- **Render**: `DamageNumberRenderSystem.Render(numbers, rect, camera)` – instance buffer with per-number scale and color (yellow for crit); shader `DamageNumberUnlit.shader` uses 11-cell atlas (digits + `!`).

---

## Modifications (spell stats)

- **Authoring**: `SpellModifications` (SpellModification.cs): ParamaterModifier for crit chance/mult, chains, pierce, area, damage type/attribute scaling, flat damage, etc. `ParamaterModifier`: flatAdditiveValue, percentIncreased, moreMultipliers (percent-based). `SpellModificationResolver` in same file: `ResolveToMultiplier`, `GetFlatAdditive` (used by applicator).
- **Application**: `SpellModificationsApplicator.CloneAndApply(AttackEntityData, spellAttributeMask, mods)` – clones entity data and applies all mods (damage, crit chance/mult, area, behaviors for chain/pierce). Used only from `SpellAuthoringData.Modify(modifications)` when building the spell to cast.
- **Test data**: `SpellModificationsTestData` (ScriptableObject) holds list-based authoring and `GetModifications()` builds runtime `SpellModifications`.

---

## Key data splits

- **Authoring (ScriptableObject)**: `SpellAuthoringData`, `AttackEntityData`, `EnemyAuthoringData`, `IdolAuthoringData`, `SpellModificationsTestData`. Lives in project; not mutated at runtime for “baked” values.
- **Runtime (structs / NativeList)**: `AttackEntity`, `Enemy`, `HitEvent`, `DamageEvent`, `DamageNumber`, chain/pierce/expiration/rehit policy structs. Simulation-only, no MonoBehaviours in hot path.
- **Enums / shared**: `DamageType`, `SpellAttributeMask` in `Data/Shared/Enums.cs`.

---

## Namespaces

- `BridgeOfBlood.Data.Spells`: spell/modification types, resolver, applicator, SpellAuthoringData, ResolvedKeyframe, etc.
- `BridgeOfBlood.Data.Shared`: enums, GameContext, telemetry structs.
- `BridgeOfBlood.Data.Enemies` / `BridgeOfBlood.Data.Idols`: runtime/authoring for enemies and idols.
- Top-level (no namespace): many systems, CombatEvents, AttackEntity*, DamageNumber*, SpellInvoker, LoopedSpellCaster, etc.
