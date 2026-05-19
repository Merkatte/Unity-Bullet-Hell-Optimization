
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.Rendering;

public struct BulletTag : IComponentData { }

public struct OptimizationModeData : IComponentData
{
    public OptimizationType optimizationType;
}

public struct BulletSpawnData : IComponentData
{
    public Entity prefab;
}

public struct BulletVelocity : IComponentData
{
    public float2 Value;
}

public struct BulletLifetime : IComponentData
{
    public float Value;
}

public struct BulletPoolState : IComponentData
{
    public byte IsActive;
    public float InitialLifetime;
}
