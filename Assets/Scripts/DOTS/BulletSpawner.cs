using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using System.Collections.Generic;

public class BulletSpawner : MonoBehaviour
{
    private const float InactiveBulletY = -100000f;

    [SerializeField] [Range(1000, 200000)] private int _ecsPoolCapacity = 100000;

    private EntityManager _entityManager;
    private Entity _bullet;
    private OptimizationType _optimizationMode;
    
    private bool _isReady;
    private bool _isPoolCreated;
    private int _nextPooledBulletIndex;
    private float _pooledBulletLifetime = 1f;
    private List<Entity> _pooledBullets;
    
    void Start()
    {
        _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        _optimizationMode = GameManager.instance.OptimizationMode;

        var modeEntity = _entityManager.CreateEntity();
        _entityManager.AddComponentData(modeEntity, new OptimizationModeData
        {
            optimizationType = _optimizationMode
        });
    }

    void Update()
    {
        if (_isReady) return;

        var query = _entityManager.CreateEntityQuery(typeof(BulletSpawnData));
        if (query.CalculateEntityCount() == 0)
        {
            query.Dispose();
            return;
        }

        _bullet = _entityManager.GetComponentData<BulletSpawnData>(
            query.GetSingletonEntity()
        ).prefab;

        query.Dispose();

        _isReady = true;
        if (_optimizationMode == OptimizationType.ECSPool)
            CreatePool();
    }

    public void SpawnBullet(float3 position, float2 direction, float speed)
    {
        if (!_isReady) return;

        if (_optimizationMode == OptimizationType.ECSPool)
        {
            SpawnPooledBullet(position, direction, speed);
            return;
        }

        Entity bullet = _entityManager.Instantiate(_bullet);

        _entityManager.SetComponentData(bullet, LocalTransform.FromPositionRotationScale(
            position,
            quaternion.identity,
            1
        ));
        _entityManager.SetComponentData(bullet, new BulletVelocity
        {
            Value = direction * speed
        });
    }

    private void CreatePool()
    {
        if (_isPoolCreated) return;

        int poolCapacity = Mathf.Max(1, _ecsPoolCapacity);
        _pooledBullets = new List<Entity>(poolCapacity);
        _nextPooledBulletIndex = 0;

        if (_entityManager.HasComponent<BulletLifetime>(_bullet))
            _pooledBulletLifetime = _entityManager.GetComponentData<BulletLifetime>(_bullet).Value;

        for (int i = 0; i < poolCapacity; i++)
        {
            Entity bullet = _entityManager.Instantiate(_bullet);

            _entityManager.SetComponentData(bullet, LocalTransform.FromPositionRotationScale(
                new float3(0f, InactiveBulletY, 0f),
                quaternion.identity,
                0f
            ));

            _entityManager.SetComponentData(bullet, new BulletVelocity
            {
                Value = float2.zero
            });

            _entityManager.SetComponentData(bullet, new BulletLifetime
            {
                Value = _pooledBulletLifetime
            });

            var poolState = new BulletPoolState
            {
                IsActive = 0,
                InitialLifetime = _pooledBulletLifetime
            };

            if (_entityManager.HasComponent<BulletPoolState>(bullet))
                _entityManager.SetComponentData(bullet, poolState);
            else
                _entityManager.AddComponentData(bullet, poolState);

            _pooledBullets.Add(bullet);
        }

        _isPoolCreated = true;
    }

    private void SpawnPooledBullet(float3 position, float2 direction, float speed)
    {
        if (_pooledBullets == null || _pooledBullets.Count == 0) return;

        Entity bullet = _pooledBullets[_nextPooledBulletIndex];
        _nextPooledBulletIndex = (_nextPooledBulletIndex + 1) % _pooledBullets.Count;

        _entityManager.SetComponentData(bullet, LocalTransform.FromPositionRotationScale(
            position,
            quaternion.identity,
            1f
        ));

        _entityManager.SetComponentData(bullet, new BulletVelocity
        {
            Value = direction * speed
        });

        _entityManager.SetComponentData(bullet, new BulletLifetime
        {
            Value = _pooledBulletLifetime
        });

        _entityManager.SetComponentData(bullet, new BulletPoolState
        {
            IsActive = 1,
            InitialLifetime = _pooledBulletLifetime
        });
    }
}
