using System.Collections;
using UnityEngine;

public class BulletShooter : MonoBehaviour
{
    [SerializeField] [Tooltip("Seconds between bursts")] [Range(0.01f, 3f)] private float _fireInterval = 0.1f;
    [SerializeField] [Tooltip("Bullets spawned per burst")] [Range(1, 5000)] private int _bulletsPerBurst = 10;

    void Start()
    {
        StartCoroutine(CreateBullet());
    }

    IEnumerator CreateBullet()
    {
        while (true)
        {
            yield return new WaitForSeconds(_fireInterval);
            for (int i = 0; i < _bulletsPerBurst; i++)
            {
                var obj = GameManager.instance.GetBullet(transform.position);
                if (obj == null) continue;
                obj.transform.position = transform.position;
                obj.SetActive(true);
            }
        }
    }
}
