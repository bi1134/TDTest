using UnityEngine;

public class BossEnemy : Enemy
{
    [Header("Boss Specifics")]
    [SerializeField] private bool enrageOnLowHealth = true;
    [SerializeField] private float enrageThreshold = 0.3f;
    [SerializeField] private float enrageSpeedMultiplier = 1.5f;

    private bool isEnraged = false;

    protected override void Update()
    {
        base.Update();
        
        // Custom Boss Logic
        if (enrageOnLowHealth && !isEnraged && CurrentHealth <= maxHealth * enrageThreshold)
        {
            Enrage();
        }
    }

    private void Enrage()
    {
        isEnraged = true;
        baseSpeed *= enrageSpeedMultiplier;
        Debug.Log("BOSS ENRAGED! Speed Increased!");
        
        // Visual Feedback (e.g. Turn Dark Red)
        var renderer = GetComponentInChildren<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = Color.Lerp(renderer.material.color, Color.black, 0.5f);
        }
    }

    protected override void Die()
    {
        Debug.Log("BOSS DEFEATED!");
        // Maybe trigger special game event?
        base.Die();
    }
}
