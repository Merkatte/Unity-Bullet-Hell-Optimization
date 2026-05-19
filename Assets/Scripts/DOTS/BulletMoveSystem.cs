using Unity.Entities;
using Unity.Transforms;
public partial struct BulletMoveSystem: ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<OptimizationModeData>();
    }

    public void OnUpdate(ref SystemState state)
    {
        if (SystemAPI.GetSingleton<OptimizationModeData>().optimizationType != OptimizationType.ECS) return;
        
        float dt = SystemAPI.Time.DeltaTime;
        
        foreach (var (velocity, transform) in
                 SystemAPI.Query<RefRO<BulletVelocity>, RefRW<LocalTransform>>()
                     .WithAll<BulletTag>())
        {
            transform.ValueRW.Position.xy += velocity.ValueRO.Value * dt;
        }
    }
}
