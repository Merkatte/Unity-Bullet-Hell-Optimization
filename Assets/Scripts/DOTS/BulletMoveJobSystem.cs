using Unity.Entities;
using Unity.Transforms;

public partial struct BulletMoveJobSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<OptimizationModeData>();
    }

    public void OnUpdate(ref SystemState state)
    {
        if (SystemAPI.GetSingleton<OptimizationModeData>().optimizationType != OptimizationType.ECSWithJobs)
            return;

        var job = new BulletMoveJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime
        };
        job.ScheduleParallel();
    }
}

[WithAll(typeof(BulletTag))]
public partial struct BulletMoveJob : IJobEntity
{
    public float DeltaTime;

    void Execute(ref LocalTransform transform, in BulletVelocity velocity)
    {
        transform.Position.xy += velocity.Value * DeltaTime;
    }
}