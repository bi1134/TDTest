using UnityEngine;

public static class PredictionHelpers
{
    /// <summary>
    /// Predicts where the target will be when the bullet hits it.
    /// Solves quadratic equation: |TargetPos + TargetVel*t - ShooterPos| = BulletSpeed*t
    /// </summary>
    public static Vector3 PredictPosition(Vector3 shooterPos, Vector3 targetPos, Vector3 targetVel, float bulletSpeed)
    {
        Vector3 d = targetPos - shooterPos;
        float dMag = d.magnitude;
        
        // If target is not moving, simple calculation
        if (targetVel.sqrMagnitude < 0.001f)
        {
            float tSimple = dMag / bulletSpeed;
            return targetPos;
        }

        // Quadratic Equation: At^2 + Bt + C = 0
        float A = Vector3.Dot(targetVel, targetVel) - bulletSpeed * bulletSpeed;
        float B = 2f * Vector3.Dot(d, targetVel);
        float C = Vector3.Dot(d, d);

        float det = B * B - 4f * A * C;

        if (det < 0) return targetPos; // No solution (can't hit)

        float t1 = (-B + Mathf.Sqrt(det)) / (2f * A);
        float t2 = (-B - Mathf.Sqrt(det)) / (2f * A);

        float t = -1f;
        if (t1 > 0 && t2 > 0) t = Mathf.Min(t1, t2);
        else if (t1 > 0) t = t1;
        else if (t2 > 0) t = t2;
        
        if (t < 0) return targetPos; // Should have positive time
        
        return targetPos + targetVel * t;
    }

    /// <summary>
    /// iteravely predicts interception point for Arc projectiles.
    /// Since Arc time-of-flight depends on the target distance (which changes), we need to iterate.
    /// </summary>
    /// <summary>
    /// Predicts interception for Arc shots with FIXED Vertical Impulse (upwardForce).
    /// Height is determined by upwardForce. Speed (horizontal) is adjusted to hit the target.
    /// </summary>
    public static Vector3 PredictArcInterception(Vector3 shooterPos, Vector3 targetPos, Vector3 targetVel, float upwardForce, float gravity = 9.81f)
    {
        // Iterative approach to find time T
        // We know Vy = upwardForce.
        // y(T) = y0 + Vy*T - 0.5*g*T^2
        // We need y(T) = target.y (approx).
        // Actually target.y changes if target is moving on a slope, but let's assume flat ground for T calculation first?
        // Or better: Iterate.
        
        Vector3 predictedPos = targetPos;
        float currentT = 0f;

        for (int i = 0; i < 3; i++)
        {
            // Solve T for vertical displacement
            float dy = predictedPos.y - shooterPos.y;
            
            // 0.5*g*T^2 - Vy*T + dy = 0
            // Quadratic: a=0.5g, b=-Vy, c=dy
            float a = 0.5f * gravity;
            float b = -upwardForce;
            float c = dy;
            
            float det = b*b - 4f*a*c;
            if (det < 0) 
            {
                // Cannot reach height? 
                // Fallback to simple time estimate (linear) or just default
                currentT = Vector3.Distance(shooterPos, predictedPos) / 20f; 
            }
            else
            {
                // Two solutions, we want the one that hits on the way down usually?
                // T = (-b +/- sqrt(det)) / 2a
                // T = (Vy +/- sqrt(Vy^2 - 2*g*dy)) / g
                // Using + gives longer time (hitting on way down). Using - gives shorter (hitting on way up).
                // Usually we want the longer arc.
                 currentT = (-b + Mathf.Sqrt(det)) / (2f * a);
            }
            
            // Update prediction
            predictedPos = targetPos + targetVel * currentT;
        }

        return predictedPos;
    }
}
