using UnityEngine;

public class Bullet : MonoBehaviour
{
    [SerializeField] [Range(0, 20)] private float _bulletSpeed;
    [SerializeField] [Range(0, 20)] private float _bulletLifeTime;

    private float _remainingLifetime;

    private void OnEnable()
    {
        _remainingLifetime = _bulletLifeTime;
    }

    private void Update()
    {
        transform.Translate(Vector2.up * _bulletSpeed * Time.deltaTime);

        _remainingLifetime -= Time.deltaTime;
        if (_remainingLifetime <= 0f)
        {
            GameManager.instance.ReleaseBullet(gameObject);
        }
    }
}
