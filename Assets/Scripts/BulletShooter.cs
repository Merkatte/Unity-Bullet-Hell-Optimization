using UnityEngine;

public class BulletShooter : MonoBehaviour
{
    [SerializeField] [Tooltip("Bullets spawned per second by this shooter")] [Min(0f)] private float _bulletsPerSecond = 125f;

    private float _spawnAccumulator;

    void Update()
    {
        _spawnAccumulator += _bulletsPerSecond * Time.deltaTime;

        int spawnCount = Mathf.FloorToInt(_spawnAccumulator);
        if (spawnCount <= 0) return;

        _spawnAccumulator -= spawnCount;

        for (int i = 0; i < spawnCount; i++)
            SpawnBullet();
    }

    private void SpawnBullet()
    {
        var obj = GameManager.instance.GetBullet(transform.position);
        if (obj == null) return;

        obj.transform.position = transform.position;
        obj.SetActive(true);
    }
}
