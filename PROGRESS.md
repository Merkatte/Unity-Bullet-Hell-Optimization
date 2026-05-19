# PROGRESS

## Current Status
Added an `ECSPool` optimization mode that prewarms ECS bullet entities and reuses them instead of instantiating and destroying entities per bullet.

## Completed Tasks
| Task ID | Date | Summary | Evidence | Related REQs |
|---|---|---|---|---|
| ECSPool-001 | 2026-05-19 | Added the sixth optimization enum value and ECS pooled bullet runtime path. | `dotnet build Bullet_Hell_Optimization.sln --no-restore` succeeded with 0 warnings and 0 errors. | User requested an enum mode below the existing enum and a pooled ECS implementation. |

## In Progress
| Task ID | Started | Current Step | Remaining Work |
|---|---|---|---|

## Files Changed
| Path | Change Summary | Reason |
|---|---|---|
| `Assets/Scripts/BaseEnum.cs` | Added `ECSPool` as the sixth `OptimizationType` value and normalized comments. | Expose the new pooled ECS mode in the Unity inspector enum. |
| `Assets/Scripts/GameManager.cs` | Routed `ECSPool` through the ECS/SubScene/BulletSpawner path. | Allow the new mode to run from the existing shooter flow. |
| `Assets/Scripts/DOTS/BulletComponents.cs` | Added `BulletPoolState`. | Store active state and initial lifetime for pooled bullets. |
| `Assets/Scripts/DOTS/BulletSpawner.cs` | Added ECS pool prewarming and pooled spawn reuse. | Avoid per-shot ECS entity instantiation in `ECSPool` mode. |
| `Assets/Scripts/DOTS/Burst/BulletPoolJobBurstSystem.cs` | Added Burst job system for pooled bullet movement and lifetime expiration. | Avoid `DestroyEntity` during pooled runtime updates. |
| `Assembly-CSharp.csproj` | Added the new pool Burst system to compile items. | Ensure local `dotnet build` verifies the new script before Unity regenerates project files. |
| `AGENTS.md` | Documented the new `ECSPool` mode. | Keep the script overview current. |
| `PROGRESS.md` | Recorded implementation and verification. | Maintain implementation evidence. |

## Implementation Notes
| ID | Note |
|---|---|
| IMP-001 | `ECSPool` uses a fixed-size circular Entity pool. If spawn pressure exceeds `_ecsPoolCapacity` before bullets expire, older slots are reused. |
| IMP-002 | Expired pooled bullets are hidden by setting scale to 0 and moving them offscreen instead of destroying the Entity. |
| IMP-003 | The current implementation removes repeated `Instantiate`/`DestroyEntity` from the pooled path, but still performs several `SetComponentData` calls per fired bullet. |

## Blockers
| ID | Blocking Task | Problem | Required Decision |
|---|---|---|---|

## Verification Performed
| Task ID | Check | Result | Notes |
|---|---|---|---|
| ECSPool-001 | `dotnet build Bullet_Hell_Optimization.sln --no-restore` | Passed | 0 warnings, 0 errors. Unity Editor play-mode behavior was not run from this shell. |

## Next Actions
1. Retest all modes in Unity Editor with four shooters at `_bulletsPerSecond = 150`.
2. Tune `_ecsPoolCapacity` to at least `spawnRatePerSecond * bulletLifetime`.
3. Profile `ECSPool` against `ECSWithJobsAndBurst` and inspect `EntityManager.Instantiate`, `EntityCommandBuffer.Playback`, `SetComponentData`, and rendering costs.

## Follow-up Changes
| Date | Summary | Evidence |
|---|---|---|
| 2026-05-20 | Replaced burst interval spawning with per-second accumulator spawning. Updated the scene's four shooters to 125 bullets/sec each, for 500 bullets/sec total. | `dotnet build Bullet_Hell_Optimization.sln --no-restore` succeeded with 0 warnings and 0 errors. |
| 2026-05-20 | Updated the scene's four shooters to 150 bullets/sec each, for 600 bullets/sec total. Added first-pass FPS observations for `None`, `ObjectPool`, and `ECSWithJobsAndBurst` to README. | `dotnet build Bullet_Hell_Optimization.sln --no-restore` succeeded with 0 warnings and 0 errors. |
