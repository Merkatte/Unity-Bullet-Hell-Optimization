# AGENTS

이 문서는 현재 프로젝트에서 직접 작성한 스크립트의 역할을 정리한다. 문서화 범위는 `Assets/Scripts` 아래의 C# 스크립트이며, `Library`, `PackageCache`, `obj` 등 Unity가 생성하거나 외부 패키지가 제공하는 코드는 제외한다.

## 프로젝트 스크립트 개요

이 프로젝트는 많은 탄환을 생성하고 이동시키는 상황에서 여러 최적화 방식을 비교하기 위한 Unity 프로젝트다. 실행 방식은 `OptimizationType` 값으로 선택되며, 크게 네 경로로 나뉜다.

| 방식 | 핵심 처리 |
|---|---|
| `None` | `Instantiate`와 `Destroy`로 GameObject 탄환을 직접 생성하고 제거한다. |
| `ObjectPool` | `UnityEngine.Pool.ObjectPool<GameObject>`로 탄환 GameObject를 재사용한다. |
| `ECS` | DOTS Entity 탄환을 생성하고 일반 ECS 시스템 반복문으로 이동과 수명을 처리한다. |
| `ECSWithJobs` | DOTS Entity 탄환을 생성하고 `IJobEntity` 병렬 Job으로 이동과 수명을 처리한다. |
| `ECSWithJobsAndBurst` | DOTS Entity 탄환을 생성하고 Burst 컴파일된 `IJobEntity` 병렬 Job으로 이동과 수명을 처리한다. |
| `ECSPool` | DOTS Entity 탄환을 미리 생성한 뒤 재사용하고, Burst Job으로 이동과 수명 비활성화를 처리한다. |

전체 흐름은 `BulletShooter`가 일정 간격으로 탄환 생성을 요청하고, `GameManager`가 현재 최적화 모드에 맞는 생성 경로를 선택하는 구조다. GameObject 방식에서는 `Bullet`과 `PoolManager`가 실제 이동, 수명, 재사용을 담당한다. ECS 방식에서는 `BulletSpawner`, authoring baker, component 데이터, movement/lifetime system들이 역할을 나눠 처리한다.

## 루트 스크립트

### `Assets/Scripts/BaseEnum.cs`

프로젝트의 최적화 방식을 선택하는 `OptimizationType` enum을 정의한다.

| 값 | 의미 |
|---|---|
| `None` | 오브젝트 풀이나 ECS를 쓰지 않고 `Instantiate` / `Destroy`를 사용한다. |
| `ObjectPool` | GameObject 기반 오브젝트 풀을 사용한다. |
| `ECS` | ECS Entity와 일반 시스템 반복문을 사용한다. |
| `ECSWithJobs` | ECS Entity와 C# Job 기반 병렬 처리를 사용한다. |
| `ECSWithJobsAndBurst` | ECS Entity, C# Job, Burst 컴파일을 함께 사용한다. |
| `ECSPool` | ECS Entity 풀링, C# Job, Burst 컴파일을 함께 사용한다. |

참고: 파일 내부 주석 일부는 문자 인코딩이 깨져 있지만 enum 값 자체는 정상적으로 읽힌다.

### `Assets/Scripts/GameManager.cs`

프로젝트의 실행 모드를 중앙에서 결정하는 MonoBehaviour 싱글턴이다.

주요 책임:

- Inspector에서 선택한 `_optimizationType`을 기준으로 현재 최적화 모드를 보관한다.
- `Awake()`에서 실행 모드에 따라 `PoolManager`, `BulletSpawner`, `SubScene` 활성 상태를 조정한다.
- `GetBullet(Vector3 position)`에서 탄환 생성 요청을 모드별로 분기한다.
- `ReleaseBullet(GameObject obj)`에서 GameObject 탄환 제거 또는 풀 반환을 모드별로 분기한다.

모드별 동작:

| 모드 | `GetBullet` 동작 | `ReleaseBullet` 동작 |
|---|---|---|
| `None` | `_bulletPrefab`을 `Instantiate`한다. | `Destroy(obj)`로 제거한다. |
| `ObjectPool` | `_poolManager.GetItem()`으로 풀에서 꺼낸다. | `_poolManager.ReleaseItem(obj)`로 풀에 반환한다. |
| ECS 계열 | `_bulletSpawner.SpawnBullet(...)`로 Entity 탄환 생성을 요청한다. | GameObject 제거 작업은 하지 않는다. Entity 수명 시스템이 제거한다. |

주의할 점:

- `GameManager.instance`를 직접 사용하는 스크립트가 있으므로 씬에 하나만 존재해야 한다.
- ECS 계열에서는 `GetBullet`이 Entity 생성을 요청한 뒤 `null`을 반환한다. 그래서 `BulletShooter`는 ECS 모드에서 GameObject 활성화 처리를 건너뛴다.

### `Assets/Scripts/BulletShooter.cs`

일정 간격으로 여러 발의 탄환 생성을 요청하는 발사기 MonoBehaviour다.

주요 필드:

| 필드 | 역할 |
|---|---|
| `_bulletsPerSecond` | 이 슈터가 1초 동안 생성 요청할 탄환 수다. |
| `_spawnAccumulator` | 프레임별 소수점 발사량을 누적해 평균 발사량을 맞추는 값이다. |

동작:

- `Update()`에서 `_bulletsPerSecond * Time.deltaTime`만큼 발사량을 누적한다.
- 누적값의 정수 부분만큼 `GameManager.instance.GetBullet(transform.position)`를 호출한다.
- 정수 발사량을 뺀 나머지는 다음 프레임으로 넘겨 평균 발사량을 유지한다.
- GameObject 탄환이 반환되면 위치를 발사기 위치로 맞추고 활성화한다.
- ECS 모드에서는 `GetBullet`이 `null`을 반환하므로 GameObject 활성화는 하지 않고, 내부적으로 Entity 생성 요청만 수행된다.

### `Assets/Scripts/Bullet.cs`

GameObject 기반 탄환 하나의 이동과 수명 관리를 담당하는 MonoBehaviour다. `None`과 `ObjectPool` 모드에서 사용하는 탄환 프리팹에 붙는 스크립트다.

주요 필드:

| 필드 | 역할 |
|---|---|
| `_bulletSpeed` | 매 프레임 위쪽 방향으로 이동할 속도다. |
| `_bulletLifeTime` | 활성화 후 살아있는 시간이다. |
| `_remainingLifetime` | 현재 남은 수명이다. |

동작:

- `OnEnable()`에서 남은 수명을 `_bulletLifeTime`으로 초기화한다.
- `Update()`에서 `Vector2.up * _bulletSpeed * Time.deltaTime`만큼 이동한다.
- 매 프레임 남은 수명을 감소시킨다.
- 수명이 0 이하가 되면 `GameManager.instance.ReleaseBullet(gameObject)`를 호출한다.

모드별 결과:

- `None` 모드에서는 `ReleaseBullet`이 `Destroy`를 호출해 오브젝트가 제거된다.
- `ObjectPool` 모드에서는 `ReleaseBullet`이 풀에 반환해 비활성화한다.

## 오브젝트 풀링 스크립트

### `Assets/Scripts/POOLING/PoolManager.cs`

GameObject 탄환을 재사용하기 위한 오브젝트 풀 관리자다. Unity의 `ObjectPool<GameObject>`를 사용한다.

주요 필드:

| 필드 | 역할 |
|---|---|
| `_bulletObject` | 풀에서 생성할 탄환 프리팹이다. |
| `_warmupCount` | 시작 시 미리 생성해 둘 탄환 수이자 풀 기본 용량이다. |
| `_pool` | 실제 `ObjectPool<GameObject>` 인스턴스다. |

동작:

- `Awake()`에서 `ObjectPool<GameObject>`를 생성한다.
- `createFunc`, `actionOnGet`, `actionOnRelease`, `actionOnDestroy` 콜백을 연결한다.
- 시작 시 `_warmupCount`만큼 `Get` 후 다시 `Release`하여 미리 인스턴스를 만들어 둔다.
- `CreateItem()`에서 프리팹을 Instantiate하고, 풀 매니저가 있는 씬으로 이동시킨 뒤 비활성화한다.
- `GetItem()`은 풀에서 탄환을 꺼낸다.
- `ReleaseItem(GameObject obj)`는 탄환을 풀에 되돌린다.

콜백 역할:

| 콜백 | 역할 |
|---|---|
| `OnGet` | 오브젝트를 활성화한다. |
| `OnRelease` | 오브젝트를 비활성화한다. |
| `OnDestroyItem` | 풀에서 제거될 때 GameObject를 Destroy한다. |

주의할 점:

- `collectionCheck: true`라 같은 오브젝트를 중복 반환하면 Unity가 검사할 수 있다.
- 워밍업은 런타임 측정 중 Instantiate 비용이 섞이지 않게 하기 위한 준비 작업이다.

## DOTS 공통 데이터

### `Assets/Scripts/DOTS/BulletComponents.cs`

ECS 탄환 처리에 필요한 component data를 정의한다.

정의된 컴포넌트:

| 타입 | 역할 |
|---|---|
| `BulletTag` | Entity가 탄환임을 표시하는 태그 컴포넌트다. |
| `OptimizationModeData` | 현재 실행 중인 `OptimizationType`을 ECS 월드에 전달한다. |
| `BulletSpawnData` | Baker가 변환한 탄환 prefab Entity를 저장한다. |
| `BulletVelocity` | 탄환의 2D 이동 속도 벡터를 저장한다. |
| `BulletLifetime` | 탄환의 남은 수명을 저장한다. |

이 파일은 런타임 로직을 직접 실행하지 않고, 다른 ECS 시스템과 Baker가 공유하는 데이터 타입만 제공한다.

### `Assets/Scripts/DOTS/BulletSpawner.cs`

MonoBehaviour 영역에서 ECS Entity 탄환 생성을 요청하는 연결 스크립트다.

주요 책임:

- Unity ECS의 `EntityManager`를 가져온다.
- 현재 `GameManager` 최적화 모드를 `OptimizationModeData` singleton 성격의 Entity로 생성한다.
- `BulletSpawnData`가 준비될 때까지 기다렸다가 탄환 prefab Entity를 캐싱한다.
- `SpawnBullet(...)` 호출 시 prefab Entity를 instantiate하고 위치와 속도를 설정한다.

동작 흐름:

1. `Start()`에서 `World.DefaultGameObjectInjectionWorld.EntityManager`를 가져온다.
2. 새 Entity를 만들고 `OptimizationModeData`를 추가해 ECS 시스템들이 현재 모드를 확인할 수 있게 한다.
3. `Update()`에서 `BulletSpawnData`를 가진 Entity가 생겼는지 확인한다.
4. 있으면 `BulletSpawnData.prefab`을 `_bullet`에 저장하고 `_isReady`를 true로 바꾼다.
5. `SpawnBullet(float3 position, float2 direction, float speed)`가 호출되면 `_bullet` prefab Entity를 instantiate한다.
6. 새 Entity의 `LocalTransform`과 `BulletVelocity`를 설정한다.

주의할 점:

- `_isReady`가 false이면 `SpawnBullet`은 아무 것도 하지 않는다. SubScene baking과 ECS prefab 준비가 끝난 뒤부터 탄환이 생성된다.
- 수명 값은 `BulletAuthoring`이 prefab Entity에 추가한 `BulletLifetime` 초기값을 사용한다.

## DOTS Authoring 스크립트

### `Assets/Scripts/DOTS/Authoring/BulletAuthoring.cs`

GameObject 프리팹을 ECS 탄환 Entity prefab으로 변환하기 위한 authoring MonoBehaviour와 Baker를 정의한다.

주요 필드:

| 필드 | 역할 |
|---|---|
| `Speed` | Inspector에서 설정 가능한 속도 값이다. 현재 Baker에서는 사용되지 않는다. |
| `Lifetime` | Entity 탄환에 부여할 초기 수명 값이다. |

Baker 동작:

- `GetEntity(TransformUsageFlags.Dynamic)`으로 변환 대상 Entity를 가져온다.
- `BulletTag`를 추가해 탄환 Entity로 표시한다.
- `BulletVelocity`를 추가한다. 실제 값은 생성 시 `BulletSpawner.SpawnBullet`에서 설정된다.
- `BulletLifetime`을 추가하고 `Value`를 `authoring.Lifetime`으로 초기화한다.

주의할 점:

- `Speed` 필드는 선언되어 있지만 현재 변환 과정에서 사용되지 않는다. 실제 이동 속도는 `BulletSpawner.SpawnBullet(..., speed)`의 인자로 결정된다.

### `Assets/Scripts/DOTS/Authoring/BulletSpawnerAuthoring.cs`

탄환 prefab GameObject를 ECS prefab Entity 참조로 변환하기 위한 authoring MonoBehaviour와 Baker를 정의한다.

주요 필드:

| 필드 | 역할 |
|---|---|
| `BulletPrefab` | ECS로 변환할 탄환 GameObject prefab이다. |

Baker 동작:

- `GetEntity(TransformUsageFlags.None)`으로 spawner 설정 Entity를 가져온다.
- `BulletPrefab`을 `TransformUsageFlags.Dynamic`으로 Entity prefab 참조로 변환한다.
- 변환된 prefab Entity를 `BulletSpawnData.prefab`에 저장한다.

이 데이터는 `BulletSpawner`가 런타임에 `BulletSpawnData` singleton을 찾아 탄환 prefab Entity를 캐싱할 때 사용된다.

## DOTS 일반 ECS 시스템

### `Assets/Scripts/DOTS/BulletMoveSystem.cs`

`OptimizationType.ECS` 모드에서 탄환 Entity의 이동을 처리하는 일반 ECS 시스템이다.

동작:

- `OnCreate()`에서 `OptimizationModeData`가 있어야 업데이트되도록 요구한다.
- `OnUpdate()`에서 현재 최적화 모드가 `ECS`가 아니면 즉시 반환한다.
- `SystemAPI.Query<RefRO<BulletVelocity>, RefRW<LocalTransform>>()`로 탄환 속도와 transform을 조회한다.
- `WithAll<BulletTag>()`로 탄환 Entity만 대상으로 삼는다.
- `transform.ValueRW.Position.xy += velocity.ValueRO.Value * dt`로 2D 위치를 갱신한다.

이 시스템은 Job을 사용하지 않고 메인 스레드에서 foreach 반복문으로 이동을 처리한다.

### `Assets/Scripts/DOTS/BulletLifetimeSystem.cs`

`OptimizationType.ECS` 모드에서 탄환 Entity의 수명을 감소시키고 만료된 Entity를 제거하는 일반 ECS 시스템이다.

동작:

- `OnCreate()`에서 `OptimizationModeData`가 있어야 업데이트되도록 요구한다.
- `OnUpdate()`에서 현재 최적화 모드가 `ECS`가 아니면 즉시 반환한다.
- `EntityCommandBuffer(Allocator.Temp)`를 생성한다.
- `BulletLifetime`과 `BulletTag`를 가진 Entity를 순회한다.
- 매 프레임 `BulletLifetime.Value`에서 `DeltaTime`을 뺀다.
- 수명이 0 이하이면 command buffer에 `DestroyEntity`를 기록한다.
- 순회가 끝나면 command buffer를 playback하고 dispose한다.

EntityCommandBuffer를 쓰는 이유는 ECS 쿼리 순회 중 Entity를 즉시 파괴하지 않고 안전하게 구조 변경을 지연하기 위해서다.

## DOTS Job 시스템

### `Assets/Scripts/DOTS/BulletMoveJobSystem.cs`

`OptimizationType.ECSWithJobs` 모드에서 탄환 이동을 `IJobEntity` 병렬 Job으로 처리한다.

구성:

- `BulletMoveJobSystem`은 시스템 진입점이다.
- `BulletMoveJob`은 실제 탄환 이동을 수행하는 Job이다.

`BulletMoveJobSystem` 동작:

- `OptimizationModeData`가 있어야 업데이트된다.
- 현재 모드가 `ECSWithJobs`가 아니면 반환한다.
- `BulletMoveJob`에 `DeltaTime`을 넣고 `ScheduleParallel()`로 병렬 스케줄링한다.

`BulletMoveJob` 동작:

- `[WithAll(typeof(BulletTag))]`로 탄환 Entity만 처리한다.
- `Execute(ref LocalTransform transform, in BulletVelocity velocity)`에서 위치를 갱신한다.
- Burst 컴파일 속성은 붙어 있지 않다.

### `Assets/Scripts/DOTS/BulletLifetimeJobSystem.cs`

`OptimizationType.ECSWithJobs` 모드에서 탄환 수명 감소와 만료 Entity 제거를 `IJobEntity` 병렬 Job으로 처리한다.

구성:

- `BulletLifetimeJobSystem`은 command buffer 생성, Job 스케줄링, playback을 담당한다.
- `BulletLifetimeJob`은 각 탄환의 수명 감소와 제거 명령 기록을 담당한다.

`BulletLifetimeJobSystem` 동작:

- `OptimizationModeData`가 있어야 업데이트된다.
- 현재 모드가 `ECSWithJobs`가 아니면 반환한다.
- `EntityCommandBuffer(Allocator.TempJob)`를 생성한다.
- `BulletLifetimeJob`에 `DeltaTime`과 `ECBWriter`를 전달한다.
- `ScheduleParallel(state.Dependency)`로 병렬 스케줄링한다.
- `state.Dependency.Complete()`로 Job 완료를 기다린다.
- command buffer를 playback하고 dispose한다.

`BulletLifetimeJob` 동작:

- `[WithAll(typeof(BulletTag))]`로 탄환 Entity만 처리한다.
- `BulletLifetime.Value`에서 `DeltaTime`을 뺀다.
- 수명이 0 이하이면 `ECBWriter.DestroyEntity(index, entity)`로 제거 명령을 기록한다.

주의할 점:

- 이 시스템은 playback을 같은 프레임에 수행해야 하므로 `Complete()`를 호출한다. 완전 비동기 체인보다 동기화 비용이 있지만, command buffer 재생 시점을 명확하게 만든다.

## DOTS Burst Job 시스템

### `Assets/Scripts/DOTS/Burst/BulletMoveJobBurstSystem.cs`

`OptimizationType.ECSWithJobsAndBurst` 모드에서 탄환 이동을 Burst 컴파일된 `IJobEntity` 병렬 Job으로 처리한다.

구성:

- `BulletMoveJobBurstSystem`은 Burst 컴파일되는 ECS 시스템이다.
- `BulletMoveJobBurst`는 Burst 컴파일되는 이동 Job이다.

동작:

- `OptimizationModeData`가 있어야 업데이트된다.
- 현재 모드가 `ECSWithJobsAndBurst`가 아니면 반환한다.
- `BulletMoveJobBurst`에 `DeltaTime`을 넣는다.
- `state.Dependency = job.ScheduleParallel(state.Dependency)`로 기존 dependency에 연결해 병렬 스케줄링한다.
- `BulletMoveJobBurst.Execute(...)`에서 `LocalTransform.Position.xy`를 속도와 DeltaTime에 따라 갱신한다.

이 스크립트는 `BulletMoveJobSystem`과 같은 이동 로직을 수행하지만 `[BurstCompile]`을 적용해 Burst 최적화 비교 대상으로 사용된다.

### `Assets/Scripts/DOTS/Burst/BulletLifetimeJobBurstSystem.cs`

`OptimizationType.ECSWithJobsAndBurst` 모드에서 탄환 수명 감소와 만료 Entity 제거를 Burst 컴파일된 `IJobEntity` 병렬 Job으로 처리한다.

구성:

- `BulletLifetimeJobBurstSystem`은 command buffer 생성, Burst Job 스케줄링, playback을 담당한다.
- `BulletLifetimeJobBurst`는 각 탄환 수명 갱신과 제거 명령 기록을 담당한다.

동작:

- `OptimizationModeData`가 있어야 업데이트된다.
- 현재 모드가 `ECSWithJobsAndBurst`가 아니면 반환한다.
- `EntityCommandBuffer(Allocator.TempJob)`를 생성한다.
- `BulletLifetimeJobBurst`에 `DeltaTime`과 `ECBWriter`를 전달한다.
- `ScheduleParallel(state.Dependency)`로 병렬 스케줄링한다.
- `state.Dependency.Complete()`로 Job 완료를 기다린다.
- command buffer를 playback하고 dispose한다.
- Job 내부에서 수명이 0 이하인 Entity를 `ECBWriter.DestroyEntity(index, entity)`로 제거 예약한다.

이 스크립트는 `BulletLifetimeJobSystem`과 같은 수명 처리 로직을 수행하지만 시스템과 Job 모두에 `[BurstCompile]`을 적용해 Burst 최적화 비교 대상으로 사용된다.

## 실행 모드별 관련 스크립트

| 실행 모드 | 관련 스크립트 |
|---|---|
| `None` | `GameManager`, `BulletShooter`, `Bullet` |
| `ObjectPool` | `GameManager`, `BulletShooter`, `Bullet`, `PoolManager` |
| `ECS` | `GameManager`, `BulletShooter`, `BulletSpawner`, `BulletComponents`, `BulletAuthoring`, `BulletSpawnerAuthoring`, `BulletMoveSystem`, `BulletLifetimeSystem` |
| `ECSWithJobs` | `GameManager`, `BulletShooter`, `BulletSpawner`, `BulletComponents`, `BulletAuthoring`, `BulletSpawnerAuthoring`, `BulletMoveJobSystem`, `BulletLifetimeJobSystem` |
| `ECSWithJobsAndBurst` | `GameManager`, `BulletShooter`, `BulletSpawner`, `BulletComponents`, `BulletAuthoring`, `BulletSpawnerAuthoring`, `BulletMoveJobBurstSystem`, `BulletLifetimeJobBurstSystem` |
| `ECSPool` | `GameManager`, `BulletShooter`, `BulletSpawner`, `BulletComponents`, `BulletAuthoring`, `BulletSpawnerAuthoring`, `BulletPoolJobBurstSystem` |

## 주요 런타임 흐름

### GameObject 직접 생성 모드

1. `BulletShooter`가 `GameManager.GetBullet`을 호출한다.
2. `GameManager`가 `_bulletPrefab`을 Instantiate한다.
3. `BulletShooter`가 위치를 맞추고 GameObject를 활성화한다.
4. `Bullet.Update`가 매 프레임 이동과 수명 감소를 처리한다.
5. 수명이 끝나면 `Bullet`이 `GameManager.ReleaseBullet`을 호출한다.
6. `GameManager`가 `Destroy`로 GameObject를 제거한다.

### 오브젝트 풀 모드

1. `PoolManager.Awake`가 `_warmupCount`만큼 탄환을 미리 생성하고 풀에 넣는다.
2. `BulletShooter`가 `GameManager.GetBullet`을 호출한다.
3. `GameManager`가 `PoolManager.GetItem`으로 탄환을 꺼낸다.
4. `Bullet.Update`가 이동과 수명 감소를 처리한다.
5. 수명이 끝나면 `Bullet`이 `GameManager.ReleaseBullet`을 호출한다.
6. `GameManager`가 `PoolManager.ReleaseItem`으로 탄환을 비활성화하고 풀에 반환한다.

### ECS 계열 모드

1. SubScene과 Baker가 탄환 prefab과 spawn data를 ECS Entity 데이터로 준비한다.
2. `BulletSpawner.Start`가 현재 최적화 모드를 `OptimizationModeData`로 ECS 월드에 기록한다.
3. `BulletSpawner.Update`가 `BulletSpawnData`를 찾아 prefab Entity를 캐싱한다.
4. `BulletShooter`가 `GameManager.GetBullet`을 호출한다.
5. `GameManager`가 `BulletSpawner.SpawnBullet`을 호출한다.
6. `BulletSpawner`가 prefab Entity를 instantiate하고 위치와 속도를 설정한다.
7. 현재 모드에 맞는 이동 시스템과 수명 시스템만 실행된다.
8. 수명이 끝난 Entity는 해당 lifetime 시스템의 command buffer를 통해 제거된다.

## 현재 코드에서 눈에 띄는 점

- `BulletAuthoring.Speed`는 현재 Baker나 Spawn 로직에서 사용되지 않는다. 실제 ECS 탄환 속도는 `GameManager.GetBullet`이 `SpawnBullet(..., 10f)`로 넘기는 고정값이다.
- `BulletShooter`는 GameObject 모드와 ECS 모드 모두 같은 `GetBullet` 호출을 사용한다. ECS 모드에서 반환값이 `null`인 것은 의도된 흐름이다.
- ECS lifetime Job 시스템들은 command buffer playback을 위해 Job 완료를 즉시 기다린다.
- `BaseEnum.cs`, `PoolManager.cs`, `CLAUDE.md`에는 일부 주석/문서 텍스트의 인코딩 깨짐이 보인다. 실행 로직 자체를 설명하는 데 필요한 코드 식별자는 정상적으로 확인된다.

## ECSPool 추가 모드

`OptimizationType.ECSPool`은 6번째 최적화 모드다. 이 모드는 ECS Entity를 매 발사마다 새로 만들고 수명 종료 때 파괴하는 대신, `BulletSpawner`가 시작 시 `_ecsPoolCapacity`만큼 Entity 탄환을 미리 생성하고 원형 인덱스로 재사용한다.

관련 스크립트:

| 스크립트 | 역할 |
|---|---|
| `Assets/Scripts/BaseEnum.cs` | `ECSPool` enum 값을 추가한다. |
| `Assets/Scripts/GameManager.cs` | `ECSPool`을 ECS 계열 모드로 취급해 `BulletSpawner`와 SubScene을 활성화한다. |
| `Assets/Scripts/DOTS/BulletComponents.cs` | `BulletPoolState`를 추가해 풀 탄환의 활성 상태와 초기 수명을 저장한다. |
| `Assets/Scripts/DOTS/BulletSpawner.cs` | `ECSPool` 모드에서 사전 생성된 Entity 풀을 만들고, 발사 요청마다 다음 Entity 슬롯을 재사용한다. |
| `Assets/Scripts/DOTS/Burst/BulletPoolJobBurstSystem.cs` | 활성 풀 탄환의 이동과 수명 감소를 Burst Job으로 처리하고, 만료 시 Destroy 대신 비활성화한다. |

`ECSPool` 실행 흐름:

1. `GameManager`가 `ECSPool` 모드를 선택하면 `BulletSpawner`와 SubScene을 켠다.
2. `BulletSpawner`가 `BulletSpawnData`에서 prefab Entity를 찾는다.
3. `BulletSpawner.CreatePool()`이 `_ecsPoolCapacity`만큼 Entity를 미리 instantiate한다.
4. 생성된 Entity는 `BulletPoolState.IsActive = 0`, scale 0, 화면 밖 위치로 초기화된다.
5. 발사 요청이 오면 `SpawnPooledBullet()`이 원형 인덱스의 Entity를 활성화하고 위치, 속도, 수명을 재설정한다.
6. `BulletPoolJobBurstSystem`이 활성 탄환만 이동시키고 수명을 감소시킨다.
7. 수명이 끝난 탄환은 Destroy되지 않고 `IsActive = 0`, scale 0, 화면 밖 위치로 돌아가 다음 발사를 기다린다.

주의할 점:

- `_ecsPoolCapacity`는 동시에 유지할 수 있는 최대 탄환 슬롯 수다. 발사량과 수명을 곱한 활성 탄환 수보다 작으면 아직 살아있는 탄환 슬롯이 원형 인덱스에 의해 덮어써질 수 있다.
- 이 모드는 Entity 생성/삭제 비용을 런타임 중 반복 지불하지 않는 것을 목표로 한다. 다만 발사 시 `SetComponentData`는 여전히 발생한다.
- 비활성 탄환도 풀 안에 존재하므로 `BulletPoolJobBurstSystem`은 풀 전체를 순회하면서 `IsActive`를 확인한다.
