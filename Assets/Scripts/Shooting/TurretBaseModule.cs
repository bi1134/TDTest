using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public class TurretBaseModule : MonoBehaviour
{
    [Header("Base Stats")]
    public TurretPropertiesSO weaponStats;

    [SerializeField] private TurretBarrelModule barrel;

    private float fireCooldown;
    [SerializeField] private Transform target;


    [SerializeField] private Turret parentNode;

    public Color hoverColor;
    private Color startColor;


    private Renderer rend;

    public void SetTarget(Transform newTarget) => target = newTarget;

    private BuildManager buildManager;

    private void Start()
    {
        rend = GetComponentInChildren<Renderer>();
        if (rend != null)
        {
            startColor = rend.material.color;
        }
        else
        {
            print("Renderer not found on TurretBaseModule!");
        }

        buildManager = BuildManager.instance;
    }

    private void Update()
    {
        if (target == null || weaponStats == null || barrel == null || !barrel.isActiveAndEnabled) return;

        fireCooldown -= Time.deltaTime;
        if (fireCooldown <= 0f)
        {
            fireCooldown = 1f / weaponStats.fireRate;
            Fire();
        }
    }

    private void Fire()
    {
        if (target == null) return;

        switch (weaponStats.fireMode)
        {
            case FireMode.Single:
                // exactly one bullet
                barrel.FireBullet(target.position, weaponStats, pelletsOverride: 1);
                break;

            case FireMode.MultiShot:
                // one volley with bulletsPerTap pellets (shotgun)
                barrel.FireBullet(target.position, weaponStats);
                break;

            case FireMode.Burst:
                StartCoroutine(FireBurst());
                break;
            case FireMode.Pulse:
                barrel.FireAOE(target.position, weaponStats);
                break;

            case FireMode.Arc:
                barrel.FireArc(target.position, weaponStats);
                break;
        }
    }

    private IEnumerator FireBurst()
    {
        int count = Mathf.Max(1, weaponStats.burstCount);
        for (int i = 0; i < count; i++)
        {
            // one bullet per burst tick
            barrel.FireBullet(target.position, weaponStats, pelletsOverride: 1);
            yield return new WaitForSeconds(weaponStats.burstInterval);
        }
    }

    #region build stuff

    private void OnMouseEnter()
    {
        // if theres no barrel to build or the mouse is over a UI element, do nothing
        if (!buildManager.HasBulletSelection || EventSystem.current.IsPointerOverGameObject() || barrel.isActiveAndEnabled) return;

        rend.material.color = hoverColor;
    }

    private void OnMouseDown()
    {
        if (EventSystem.current.IsPointerOverGameObject())
            return;

        if (!buildManager.HasBulletSelection)
        {
            Debug.Log("Cannot install ammo: projectile is null");
            return;
        }

        buildManager.TryInstallBullet(this);
    }

    public void SetBulletType(BulletBlueprint bulletType)
    {
        if (!barrel.isActiveAndEnabled || !barrel.gameObject.activeSelf)
        {
            barrel.SetBulletType(bulletType.bulletPrefab);
            parentNode.SetBarrelActive(true);
        }
    }

    private void OnMouseExit()
    {
        rend.material.color = startColor; // Reset to original color
    }

    #endregion
}
