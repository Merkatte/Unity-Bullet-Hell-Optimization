using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
public partial struct BulletPoolJobBurstSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<OptimizationModeData>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (SystemAPI.GetSingleton<OptimizationModeData>().optimizationType != OptimizationType.ECSPool)
            return;

        var job = new BulletPoolJobBurst
        {
            DeltaTime = SystemAPI.Time.DeltaTime
        };

        state.Dependency = job.ScheduleParallel(state.Dependency);
    }
}

[BurstCompile]
[WithAll(typeof(BulletTag))]
public partial struct BulletPoolJobBurst : IJobEntity
{
    public float DeltaTime;

    void Execute(
        ref LocalTransform transform,
        ref BulletLifetime lifetime,
        ref BulletPoolState poolState,
        in BulletVelocity velocity)
    {
        if (poolState.IsActive == 0)
            return;

        transform.Position.xy += velocity.Value * DeltaTime;
        lifetime.Value -= DeltaTime;

        if (lifetime.Value > 0f)
            return;

        poolState.IsActive = 0;
        lifetime.Value = poolState.InitialLifetime;
        transform.Position = new float3(0f, -100000f, 0f);
        transform.Scale = 0f;
    }
}
