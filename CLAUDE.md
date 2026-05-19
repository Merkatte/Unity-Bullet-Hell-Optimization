# 탄막게임 최적화 테스트 프로젝트 가이드

## 프로젝트 개요

이 프로젝트는 Unity에서 수천 개의 총알(탄막)을 동시에 처리할 때 발생하는 성능 문제를 4가지 방법으로 비교하는 최적화 테스트입니다.

| 방법 | 난이도 | 성능 |
|------|--------|------|
| 1. Instantiate / Destroy | 쉬움 | 매우 낮음 |
| 2. Object Pooling | 보통 | 중간 |
| 3. ECS / DOTS | 어려움 | 매우 높음 |
| 4. C# Jobs + Burst Compiler | 어려움 | 매우 높음 |

---

## 방법 1 — Instantiate / Destroy (기준선)

총알이 발사될 때마다 `Instantiate()`로 오브젝트를 생성하고, 화면 밖으로 나가면 `Destroy()`로 삭제하는 가장 단순한 방식입니다.

### 왜 느린가?

- `Instantiate()`는 호출할 때마다 힙(Heap) 메모리를 새로 할당합니다.
- `Destroy()`는 가비지 컬렉터(GC)가 나중에 메모리를 회수하도록 예약합니다.
- GC가 실행되는 순간 게임이 수 밀리초 동안 **멈추는 스파이크(Spike)** 가 발생합니다.
- 총알 1,000개 이상이 되면 프레임 드랍이 눈에 띄게 나타납니다.

```csharp
// 방법 1 예시
public class BulletSpawner_Naive : MonoBehaviour
{
    [SerializeField] private GameObject bulletPrefab;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
            SpawnBullet();
    }

    void SpawnBullet()
    {
        // 매번 힙 할당 발생 → GC 압박
        GameObject bullet = Instantiate(bulletPrefab, transform.position, Quaternion.identity);
        Destroy(bullet, 3f);
    }
}
```

---

## 방법 2 — Object Pooling (중간 최적화)

미리 총알 오브젝트를 일정 개수 만들어두고, 필요할 때 꺼내 쓰고 다 쓰면 다시 돌려놓는 방식입니다. `Instantiate` / `Destroy` 대신 `SetActive(true)` / `SetActive(false)`를 사용합니다.

### 왜 빠른가?

- 힙 할당이 초기 1회만 발생합니다.
- GC 압박이 거의 없어 스파이크가 사라집니다.
- 다만 여전히 각 총알이 `MonoBehaviour`를 가지고 있어 Update() 오버헤드가 존재합니다.

```csharp
// 방법 2 예시 (간략화)
public class BulletPool : MonoBehaviour
{
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private int poolSize = 500;

    private Queue<GameObject> pool = new Queue<GameObject>();

    void Awake()
    {
        for (int i = 0; i < poolSize; i++)
        {
            var obj = Instantiate(bulletPrefab);
            obj.SetActive(false);
            pool.Enqueue(obj);
        }
    }

    public GameObject Get(Vector3 position)
    {
        if (pool.Count == 0) return null; // 풀이 비었을 때 처리
        var bullet = pool.Dequeue();
        bullet.transform.position = position;
        bullet.SetActive(true);
        return bullet;
    }

    public void Return(GameObject bullet)
    {
        bullet.SetActive(false);
        pool.Enqueue(bullet);
    }
}
```

---

## 방법 3 — ECS / DOTS (고급 최적화)

> **처음 접하는 분을 위한 개념 설명입니다. 천천히 읽어주세요.**

### 먼저, 기존 Unity 방식의 문제를 이해하자

기존 Unity는 **OOP(객체 지향 프로그래밍)** 기반입니다. 총알 1개 = GameObject 1개 = 여러 Component가 붙어있는 객체입니다.

문제는 메모리 구조에 있습니다.

```
[RAM 안에서 기존 방식]

총알A: [Transform | Rigidbody | SpriteRenderer | BulletScript | ...]  ← 메모리 어딘가
총알B: [Transform | Rigidbody | SpriteRenderer | BulletScript | ...]  ← 다른 메모리 어딘가
총알C: [Transform | Rigidbody | SpriteRenderer | BulletScript | ...]  ← 또 다른 메모리 어딘가
```

CPU가 총알 1,000개의 위치를 계산하려면, 1,000번 메모리 여기저기를 뛰어다녀야 합니다. 이를 **캐시 미스(Cache Miss)** 라고 하며, 현대 CPU에서 성능을 가장 크게 잡아먹는 원인 중 하나입니다.

### ECS란 무엇인가?

**ECS = Entity + Component + System**

| 개념 | 설명 | 비유 |
|------|------|------|
| **Entity** | 그냥 숫자 ID입니다. 오브젝트가 아닙니다. | 직원 사번 |
| **Component** | 순수한 데이터만 담는 구조체(struct)입니다. | 직원의 개별 정보 카드 |
| **System** | 특정 Component를 가진 Entity를 일괄 처리하는 로직입니다. | 인사팀이 모든 직원 카드를 한꺼번에 처리 |

### 메모리 구조가 어떻게 달라지는가?

ECS에서는 같은 종류의 Component들이 **메모리에 연속으로 나란히** 저장됩니다. 이를 **Archetype Chunk** 라고 부릅니다.

```
[RAM 안에서 ECS 방식]

Position 배열: [P1][P2][P3][P4][P5]...[P1000]  ← 연속된 메모리
Velocity 배열: [V1][V2][V3][V4][V5]...[V1000]  ← 연속된 메모리
```

CPU는 이 연속된 메모리를 한 번에 캐시로 불러와서 처리합니다. 캐시 미스가 거의 발생하지 않아 처리 속도가 극적으로 빨라집니다.

### Component는 어떻게 생겼는가?

```csharp
// ECS Component = 데이터만 있는 struct
using Unity.Entities;
using Unity.Mathematics;

public struct BulletTag : IComponentData { }  // 총알임을 표시하는 태그

public struct BulletVelocity : IComponentData
{
    public float2 Value;  // 총알의 이동 방향과 속도
}

public struct BulletLifetime : IComponentData
{
    public float RemainingTime;  // 남은 수명
}
```

`MonoBehaviour`가 없습니다. 메서드도 없습니다. **오직 데이터만 있습니다.**

### System은 어떻게 생겼는가?

```csharp
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Burst;

// Burst 컴파일러 적용 (방법 4와 연계됨)
[BurstCompile]
public partial struct BulletMoveSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        float dt = SystemAPI.Time.DeltaTime;

        // BulletVelocity와 LocalTransform을 가진 모든 Entity를 한꺼번에 처리
        foreach (var (velocity, transform) in
            SystemAPI.Query<RefRO<BulletVelocity>, RefRW<LocalTransform>>()
                     .WithAll<BulletTag>())
        {
            transform.ValueRW.Position.xy += velocity.ValueRO.Value * dt;
        }
    }
}
```

**핵심 포인트:** `foreach` 하나로 총알 1,000개가 연속된 메모리에서 순서대로 처리됩니다.

### 총알을 생성하는 방법 (Spawn)

```csharp
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public class BulletSpawner_ECS : MonoBehaviour
{
    private EntityManager entityManager;
    private EntityArchetype bulletArchetype;

    void Start()
    {
        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

        // 총알의 "틀"(Archetype)을 미리 정의
        bulletArchetype = entityManager.CreateArchetype(
            typeof(LocalTransform),
            typeof(BulletTag),
            typeof(BulletVelocity),
            typeof(BulletLifetime)
        );
    }

    public void SpawnBullet(float2 position, float2 direction, float speed)
    {
        Entity bullet = entityManager.CreateEntity(bulletArchetype);

        entityManager.SetComponentData(bullet, LocalTransform.FromPosition(new float3(position, 0f)));
        entityManager.SetComponentData(bullet, new BulletVelocity { Value = direction * speed });
        entityManager.SetComponentData(bullet, new BulletLifetime { RemainingTime = 3f });
    }
}
```

### 수명이 다한 총알 제거 System

```csharp
[BurstCompile]
public partial struct BulletLifetimeSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        float dt = SystemAPI.Time.DeltaTime;
        var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

        foreach (var (lifetime, entity) in
            SystemAPI.Query<RefRW<BulletLifetime>>()
                     .WithAll<BulletTag>()
                     .WithEntityAccess())
        {
            lifetime.ValueRW.RemainingTime -= dt;

            if (lifetime.ValueRO.RemainingTime <= 0f)
                ecb.DestroyEntity(entity);  // 즉시 삭제하지 않고 예약
        }

        ecb.Playback(state.EntityManager);  // 일괄 삭제 실행
        ecb.Dispose();
    }
}
```

> **왜 EntityCommandBuffer(ECB)를 쓰는가?**
> System이 실행 중인 동안 Entity를 즉시 삭제하면 순회하는 배열이 흔들립니다. ECB는 "할 일 목록"에 삭제를 예약해두었다가, 순회가 끝난 후 한꺼번에 실행합니다.

### ECS 패키지 설치 방법

Unity Package Manager에서 아래를 설치하세요.

```
com.unity.entities          (Entities)
com.unity.entities.graphics (Entities Graphics, 렌더링용)
com.unity.mathematics       (수학 라이브러리)
com.unity.collections       (NativeArray 등)
```

---

## 방법 4 — C# Jobs + Burst Compiler (최고 성능)

### 먼저, 왜 더 빠른가?

방법 3(ECS)도 이미 빠르지만, 기본적으로 **메인 스레드 1개** 에서 실행됩니다. 현대 PC는 CPU 코어가 4개~16개이지만, 방법 3만 쓰면 나머지 코어들이 놀고 있는 셈입니다.

**C# Job System**은 이 작업을 여러 CPU 코어에 자동으로 나눠 병렬 처리합니다.
**Burst Compiler**는 C# 코드를 CPU 친화적인 네이티브 코드(SIMD 명령어 포함)로 변환해줍니다.

```
[단일 스레드(방법 3)]        [멀티 스레드(방법 4)]
코어1: ████████████████    코어1: ████
코어2: (유휴)               코어2: ████
코어3: (유휴)               코어3: ████
코어4: (유휴)               코어4: ████
       16ms 소요                   4ms 소요
```

### Job이란 무엇인가?

**Job = 메인 스레드가 워커 스레드에게 넘기는 작업 단위**

Job은 반드시 아래 규칙을 따라야 합니다.
1. `struct`여야 합니다 (class 불가).
2. 참조 타입(class, string 등)을 직접 가질 수 없습니다.
3. `NativeArray`, `NativeList` 같은 Unity의 특수 컨테이너만 사용 가능합니다.

이 제약 덕분에 여러 스레드가 동시에 실행해도 데이터 충돌(Race Condition)이 발생하지 않습니다.

### IJobParallelFor — 배열을 병렬로 처리

가장 자주 쓰이는 Job 타입입니다. 배열의 각 요소를 여러 코어가 나눠서 동시에 처리합니다.

```csharp
using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections;

// [BurstCompile] = Burst 컴파일러로 네이티브 코드 변환
[BurstCompile]
public struct BulletMoveJob : IJobParallelFor
{
    // [ReadOnly] = 이 데이터는 읽기만 함 → 여러 스레드가 동시에 읽어도 안전
    [ReadOnly] public NativeArray<float2> Velocities;
    [ReadOnly] public float DeltaTime;

    // 쓰기 가능 (각 인덱스는 해당 스레드만 접근 → 충돌 없음)
    public NativeArray<float2> Positions;

    // index = 현재 이 스레드가 담당하는 배열 인덱스
    public void Execute(int index)
    {
        Positions[index] += Velocities[index] * DeltaTime;
    }
}
```

### Job을 실제로 실행하는 방법

```csharp
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public class BulletSystem_Jobs : MonoBehaviour
{
    private const int BulletCount = 10000;

    private NativeArray<float2> positions;
    private NativeArray<float2> velocities;
    private JobHandle moveJobHandle;

    void Start()
    {
        // NativeArray: 관리되지 않는 메모리(Unmanaged Memory)에 할당 → GC 없음
        positions  = new NativeArray<float2>(BulletCount, Allocator.Persistent);
        velocities = new NativeArray<float2>(BulletCount, Allocator.Persistent);

        // 초기화 (예시)
        for (int i = 0; i < BulletCount; i++)
        {
            positions[i]  = new float2(0f, 0f);
            velocities[i] = new float2(math.cos(i * 0.1f), math.sin(i * 0.1f)) * 5f;
        }
    }

    void Update()
    {
        // 1. Job 구조체에 데이터를 넘겨줌
        var moveJob = new BulletMoveJob
        {
            Positions  = positions,
            Velocities = velocities,
            DeltaTime  = Time.deltaTime
        };

        // 2. 스케줄: 워커 스레드로 작업 보내기 (innerloopBatchCount = 코어당 처리량)
        moveJobHandle = moveJob.Schedule(BulletCount, 64);

        // 메인 스레드는 이 사이에 다른 작업 가능 (렌더링 준비 등)

        // 3. 완료 대기: 이 줄부터는 Job이 완료되었음이 보장됨
        moveJobHandle.Complete();

        // 4. 결과(positions)를 이용해 렌더링 등 후처리
    }

    void OnDestroy()
    {
        // NativeArray는 GC가 관리하지 않으므로 반드시 수동 해제
        moveJobHandle.Complete();
        positions.Dispose();
        velocities.Dispose();
    }
}
```

### Allocator의 종류와 선택 기준

| Allocator | 수명 | 용도 |
|-----------|------|------|
| `Allocator.Temp` | 1 프레임 이내 | Job 내부의 임시 데이터 |
| `Allocator.TempJob` | 최대 4 프레임 | Job에 전달할 단기 데이터 |
| `Allocator.Persistent` | 직접 Dispose할 때까지 | 시스템 수명과 동일한 영구 데이터 |

### ECS + Jobs 함께 쓰기 (IJobEntity)

ECS의 System 안에서 Job을 병렬로 실행하는 가장 현업적인 패턴입니다.

```csharp
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Burst;

[BurstCompile]
public partial struct BulletMoveJobSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float dt = SystemAPI.Time.DeltaTime;

        // IJobEntity: Entity를 대상으로 하는 Job
        var job = new MoveBulletsJob { DeltaTime = dt };

        // ScheduleParallel: 워커 스레드 여러 개에 분산 실행
        job.ScheduleParallel();
    }
}

[BurstCompile]
public partial struct MoveBulletsJob : IJobEntity
{
    public float DeltaTime;

    // BulletVelocity(읽기)와 LocalTransform(쓰기)를 가진 모든 Entity에 실행
    void Execute(in BulletVelocity velocity, ref LocalTransform transform)
    {
        transform.Position.xy += velocity.Value * DeltaTime;
    }
}
```

**이것이 방법 3과 방법 4의 결합입니다.** 이 패턴 하나로 ECS의 캐시 친화적 메모리 구조 + Job System의 멀티코어 병렬처리 + Burst의 네이티브 코드 최적화를 모두 얻습니다.

---

## Burst Compiler 상세

### Burst가 하는 일

```
일반 C# 코드                    Burst 컴파일 후
─────────────────────────       ─────────────────────────────────────
float result = 0;               ; CPU SIMD 명령어 (AVX2 예시)
for (int i = 0; i < 8; i++)    vmovups ymm0, [positions]
    result += positions[i];     vaddps  ymm0, ymm0, [velocities]
                                ; 8개를 1개 명령어로 동시 처리
```

SIMD(Single Instruction, Multiple Data)는 CPU가 여러 데이터를 한 번에 처리하는 기술입니다. Burst는 일반 C# 코드를 분석해서 자동으로 SIMD 명령어를 생성합니다.

### Burst 제약사항 (반드시 숙지)

```csharp
[BurstCompile]
struct MyJob : IJob
{
    // 가능: 값 타입(struct), NativeArray
    public float Speed;
    public NativeArray<float2> Positions;

    // 불가능: 관리 타입(class, string, delegate, object)
    // public List<Vector2> ManagedList;   // 컴파일 에러
    // public string Name;                  // 컴파일 에러
    // public GameObject Target;            // 컴파일 에러

    public void Execute()
    {
        // 불가능: static 필드 접근 (전역 상태는 스레드 안전하지 않음)
        // Debug.Log("...");  // 불가능 (Burst 모드에서)

        // 가능: Unity.Mathematics 함수
        float dist = math.length(Positions[0]);
    }
}
```

---

## 성능 비교 요약

| 방법 | 총알 1,000개 (ms) | 총알 10,000개 (ms) | GC 스파이크 | 멀티코어 |
|------|-------------------|---------------------|-------------|---------|
| 1. Instantiate/Destroy | ~8ms | 게임 불가 | 있음 | 미사용 |
| 2. Object Pooling | ~3ms | ~30ms | 거의 없음 | 미사용 |
| 3. ECS/DOTS | ~0.5ms | ~5ms | 없음 | 미사용 |
| 4. ECS + Jobs + Burst | ~0.1ms | ~1ms | 없음 | 사용 |

> 위 수치는 환경에 따라 다르며, 비교를 위한 참고값입니다.

---

## 학습 순서 권장

1. **방법 1, 2** 로 기본 탄막 시스템 구현 → Unity Profiler로 병목 확인
2. **방법 3** : ECS 없이 `IJobParallelFor` 만 먼저 적용 → 멀티코어 개념 익히기
3. **방법 3** : ECS Component/System 구조 학습 → 메모리 레이아웃 이해
4. **방법 4** : `IJobEntity + ScheduleParallel` 로 ECS + Jobs 결합
5. Unity Profiler / Entities Hierarchy 창으로 각 방법 프레임 타임 측정 및 비교

---

## 자주 하는 실수

### NativeArray Dispose 누락
```csharp
// 잘못된 예
void OnDestroy()
{
    // positions.Dispose() 빠뜨림 → 메모리 누수 경고
}

// 올바른 예
void OnDestroy()
{
    moveJobHandle.Complete(); // 반드시 Job 완료 후 Dispose
    positions.Dispose();
    velocities.Dispose();
}
```

### Job Complete 전에 데이터 접근
```csharp
// 잘못된 예
moveJobHandle = moveJob.Schedule(count, 64);
float2 pos = positions[0]; // 아직 Job이 실행 중 → 예외 발생

// 올바른 예
moveJobHandle = moveJob.Schedule(count, 64);
moveJobHandle.Complete();
float2 pos = positions[0]; // 안전
```

### ECB 없이 System 순회 중 Entity 삭제
```csharp
// 잘못된 예
foreach (var (lifetime, entity) in SystemAPI.Query<...>().WithEntityAccess())
{
    state.EntityManager.DestroyEntity(entity); // 순회 중 삭제 → 예외
}

// 올바른 예: ECB로 예약 후 일괄 처리 (위 BulletLifetimeSystem 참고)
```
