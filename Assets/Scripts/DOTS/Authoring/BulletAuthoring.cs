using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class BulletAuthoring : MonoBehaviour
{
    public float Speed = 10f;
    public float Lifetime = 1f;

    class BulletBaker : Baker<BulletAuthoring>
    {
        public override void Bake(BulletAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            {
                AddComponent(entity, new BulletTag());
                AddComponent(entity, new BulletVelocity());
                AddComponent(entity, new BulletLifetime
                {
                    Value = authoring.Lifetime,
                });
            }
        }
    }
}
