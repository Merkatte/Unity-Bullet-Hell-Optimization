using Unity.Collections;
using Unity.Entities;

public partial struct BulletLifetimeSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<OptimizationModeData>();
    }

    public void OnUpdate(ref SystemState state)
    {
        if (SystemAPI.GetSingleton<OptimizationModeData>().optimizationType != OptimizationType.ECS)
            return;
        
        float dt = SystemAPI.Time.DeltaTime;

        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (lifetime, entity) in SystemAPI.Query<RefRW<BulletLifetime>>().WithAll<BulletTag>()
                     .WithEntityAccess())
        {
            lifetime.ValueRW.Value -= dt;
            
            if (lifetime.ValueRO.Value <= 0f)
                ecb.DestroyEntity(entity);
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
