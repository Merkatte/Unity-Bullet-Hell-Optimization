using Unity.Mathematics;
using Unity.Scenes;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    [SerializeField] OptimizationType _optimizationType;
    [SerializeField] PoolManager _poolManager;
    [SerializeField] BulletSpawner _bulletSpawner;
    [SerializeField] GameObject _bulletPrefab;
    [SerializeField] private SubScene _subScene;

    public OptimizationType OptimizationMode
    {
        get { return _optimizationType; }
    }
    
    public static GameManager instance;

    private void Awake()
    {
        instance = this;
        switch (_optimizationType)
        {
            case OptimizationType.None:
                _poolManager.gameObject.SetActive(false);
                _bulletSpawner.gameObject.SetActive(false);
                _subScene.gameObject.SetActive(false);
                break;
            case OptimizationType.ObjectPool:
                _poolManager.gameObject.SetActive(true);
                _bulletSpawner.gameObject.SetActive(false);
                _subScene.gameObject.SetActive(false);
                break;
            case OptimizationType.ECS:
            case OptimizationType.ECSWithJobs:
            case OptimizationType.ECSWithJobsAndBurst:
            case OptimizationType.ECSPool:
                _poolManager.gameObject.SetActive(false);
                _bulletSpawner.gameObject.SetActive(true);
                break;
        }
        _subScene.gameObject.SetActive(_optimizationType >= OptimizationType.ECS);
    }

    public GameObject GetBullet(Vector3 position)
    {
        switch (_optimizationType)
        {
            case OptimizationType.None:
                var obj = Instantiate(_bulletPrefab);
                return obj;
            case OptimizationType.ObjectPool:
                return _poolManager.GetItem();
            case OptimizationType.ECS:
            case OptimizationType.ECSWithJobs:
            case OptimizationType.ECSWithJobsAndBurst:
            case OptimizationType.ECSPool:
                _bulletSpawner.SpawnBullet(new float3(position.x, position.y, position.z), new float2(0f, 1f), 10f);
                break;
        }
        return null;
    }

    public void ReleaseBullet(GameObject obj)
    {
        switch (_optimizationType)
        {
            case OptimizationType.None:
                Destroy(obj);
                break;
            case OptimizationType.ObjectPool:
                _poolManager.ReleaseItem(obj);
                break;
            case OptimizationType.ECS:
                break;
            case OptimizationType.ECSWithJobs:
                break;
            case OptimizationType.ECSWithJobsAndBurst:
                break;
            case OptimizationType.ECSPool:
                break;
        }
    }
}
