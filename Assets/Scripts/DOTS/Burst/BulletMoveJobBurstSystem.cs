using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

[BurstCompile]
public partial struct BulletMoveJobBurstSystem : ISystem
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

        var job = new BulletMoveJobBurst { DeltaTime = SystemAPI.Time.DeltaTime };
        state.Dependency = job.ScheduleParallel(state.Dependency);
    }
}

[BurstCompile]
[WithAll(typeof(BulletTag))]
public partial struct BulletMoveJobBurst : IJobEntity
{
    public float DeltaTime;

    void Execute(ref LocalTransform transform, in BulletVelocity velocity)
    {
        transform.Position.xy += velocity.Value * DeltaTime;
    }
}