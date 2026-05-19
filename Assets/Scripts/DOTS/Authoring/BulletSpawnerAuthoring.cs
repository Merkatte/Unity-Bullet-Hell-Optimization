using Unity.Entities;
using UnityEngine;

public class BulletSpawnerAuthoring : MonoBehaviour
{
    public GameObject BulletPrefab;

    class Baker : Baker<BulletSpawnerAuthoring>
    {
        public override void Bake(BulletSpawnerAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);

            AddComponent(entity, new BulletSpawnData
            {
                prefab = GetEntity(authoring.BulletPrefab, TransformUsageFlags.Dynamic)
            });
        }
    }
}