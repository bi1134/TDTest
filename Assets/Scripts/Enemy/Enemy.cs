using UnityEngine;

public class Enemy : MonoBehaviour
{
    public void TakeDamage(float damage)
    {
        print(this + " is taking " + damage );
    }    
}
