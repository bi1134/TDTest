using UnityEngine;

public class Turret : MonoBehaviour
{
    [Header("Parts")]
    [SerializeField] private TurretBaseModule baseModule;
    [SerializeField] public Transform partToRotate;

    [Header("Targeting")]
    public float range = 15f;
    public float rotationSpeed = 10f;
    public string enemyTag = "Enemy";

    private Transform target;

    private void Start()
    {
        InvokeRepeating(nameof(UpdateTarget), 0f, 0.5f);
    }

    private void Update()
    {
        if (target == null)
        {
            baseModule.SetTarget(target); 
            return;
        }
        // Rotate
        if (partToRotate == null) return;

        Vector3 dir = target.position - transform.position;
        Quaternion look = Quaternion.LookRotation(dir);
        Vector3 rot = Quaternion.Lerp(partToRotate.rotation, look, Time.deltaTime * rotationSpeed).eulerAngles;
        partToRotate.rotation = Quaternion.Euler(0f, rot.y, 0f);

        baseModule.SetTarget(target);
    }

    private void UpdateTarget()
    {
        float shortest = Mathf.Infinity;
        GameObject nearest = null;

        foreach (var enemy in GameObject.FindGameObjectsWithTag(enemyTag))
        {
            float dist = Vector3.Distance(transform.position, enemy.transform.position);
            if (dist < shortest)
            {
                shortest = dist;
                nearest = enemy;
            }
        }

        target = (nearest != null && shortest <= range) ? nearest.transform : null;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, range);
    }
}
