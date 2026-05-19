using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.SceneManagement;

public class PoolManager : MonoBehaviour
{
    [SerializeField] private GameObject _bulletObject;
    [SerializeField] private int _warmupCount = 1000;
    private ObjectPool<GameObject> _pool;

    void Awake()
    {
        _pool = new ObjectPool<GameObject>(
            createFunc: CreateItem,
            actionOnGet: OnGet,
            actionOnRelease: OnRelease,
            actionOnDestroy: OnDestroyItem,
            collectionCheck: true,
            defaultCapacity: _warmupCount
        );

        // 테스트 시작 전 Instantiate 비용을 미리 지불해 두어 측정 구간을 공정하게 만듦
        var temp = new List<GameObject>(_warmupCount);
        for (int i = 0; i < _warmupCount; i++)
            temp.Add(_pool.Get());
        foreach (var obj in temp)
            _pool.Release(obj);
    }

    public GameObject GetItem() => _pool.Get();
    public void ReleaseItem(GameObject obj) => _pool.Release(obj);

    private GameObject CreateItem()
    {
        var obj = Instantiate(_bulletObject);
        SceneManager.MoveGameObjectToScene(obj, gameObject.scene);
        obj.SetActive(false);
        return obj;
    }

    private void OnGet(GameObject obj) => obj.SetActive(true);
    private void OnRelease(GameObject obj) => obj.SetActive(false);
    private void OnDestroyItem(GameObject obj) => Destroy(obj);
}
