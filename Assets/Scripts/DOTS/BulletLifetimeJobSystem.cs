using Unity.Collections;
using Unity.Entities;

public partial struct BulletLifetimeJobSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<OptimizationModeData>();
    }

    public void OnUpdate(ref SystemState state)
    {
        if (SystemAPI.GetSingleton<OptimizationModeData>().optimizationType != OptimizationType.ECSWithJobs)
            return;

        var ecb = new EntityCommandBuffer(Allocator.TempJob);

        var job = new BulletLifetimeJob
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

[WithAll(typeof(BulletTag))]
public partial struct BulletLifetimeJob : IJobEntity
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