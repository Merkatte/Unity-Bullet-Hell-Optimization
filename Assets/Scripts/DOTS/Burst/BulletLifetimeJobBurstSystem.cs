using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

[BurstCompile]
public partial struct BulletLifetimeJobBurstSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<OptimizationModeData>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (SystemAPI.GetSingleton<OptimizationModeData>().optimizationType
            != OptimizationType.ECSWithJobsAndBurst) return;

        var ecb = new EntityCommandBuffer(Allocator.TempJob);

        var job = new BulletLifetimeJobBurst
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
            ECBWriter  = ecb.AsParallelWriter()
        };

        state.Dependency = job.ScheduleParallel(state.Dependency);
        state.Dependency.Complete();

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}

[BurstCompile]
[WithAll(typeof(BulletTag))]
public partial struct BulletLifetimeJobBurst : IJobEntity
{
    public float DeltaTime;
    public EntityCommandBuffer.ParallelWriter ECBWriter;

    void Execute([EntityIndexInQuery] int index, Entity entity, ref BulletLifetime lifetime)
    {
        lifetime.Value -= DeltaTime;
        if (lifetime.Value <= 0f)
            ECBWriter.DestroyEntity(index, entity);
    }
}