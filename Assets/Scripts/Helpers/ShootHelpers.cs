using System.Collections.Generic;
using UnityEngine;

public static class ShootHelpers
{
    private const float EPS = 0.0001f;

    /// <summary>
    /// Generates pellet directions:
    /// - If spreadDeg > 0: samples a circular/elliptical cone (spreadDeg in degrees, spreadX/Y are multipliers).
    /// - If spreadDeg ~ 0: generates line/cross patterns driven by spreadX/spreadY (in degrees).
    ///   * spreadX > 0, spreadY = 0 -> horizontal line
    ///   * spreadX = 0, spreadY > 0 -> vertical line
    ///   * both > 0 -> plus or X depending on axisRotationDeg (0 = plus, 45 = X)
    /// </summary>
    #region spread helpers
    public static List<Vector3> GeneratePelletDirections(
        Vector3 baseDir,
        int bulletsPerTap,
        float spreadDeg,
        float spreadX,
        float spreadY,
        float axisRotationDeg,
        bool evenAxisDistribution,
        bool deterministicPelletSpacing
    )
    {
        var list = new List<Vector3>(bulletsPerTap);
        if (bulletsPerTap <= 0)
            return list;

        if (spreadDeg > EPS)
        {
            // Cone/ellipse mode
            for (int i = 0; i < bulletsPerTap; i++)
                list.Add(SampleCone(baseDir, spreadDeg, spreadX, spreadY));
            return list;
        }

        // Line/cross mode
        bool useX = spreadX > EPS;
        bool useY = spreadY > EPS;

        if (!useX && !useY)
        {
            // No spread at all — straight shots
            for (int i = 0; i < bulletsPerTap; i++)
                list.Add(baseDir.normalized);
            return list;
        }

        // Build basis and rotate axes to support X shape via axisRotationDeg
        BuildBasis(baseDir, out var forward, out var right, out var up);
        RotateAxesAroundForward(ref right, ref up, axisRotationDeg, forward);

        int n = bulletsPerTap;
        int countX = 0, countY = 0;

        if (useX && useY)
        {
            if (evenAxisDistribution)
            {
                // split as evenly as possible
                countX = (n + 1) / 2;
                countY = n / 2;
            }
            // else: we will pick axis per pellet randomly below
        }
        else if (useX)
        {
            countX = n;
        }
        else
        {
            countY = n;
        }

        if (useX && useY && !evenAxisDistribution)
        {
            // Randomly choose axis per pellet
            for (int i = 0; i < n; i++)
            {
                bool chooseX = Random.value < 0.5f;
                float extentDeg = chooseX ? spreadX : spreadY;
                Vector3 axis = chooseX ? right : up;
                float t = SampleLineParam(i, n, deterministicPelletSpacing); // [-1,1]
                list.Add(OffsetByAxis(forward, axis, extentDeg, t));
            }
        }
        else
        {
            for (int i = 0; i < countX; i++)
            {
                float t = SampleLineParam(i, countX, deterministicPelletSpacing);
                list.Add(OffsetByAxis(forward, right, spreadX, t));
            }
            for (int i = 0; i < countY; i++)
            {
                float t = SampleLineParam(i, countY, deterministicPelletSpacing);
                list.Add(OffsetByAxis(forward, up, spreadY, t));
            }
        }

        return list;
    }

    // --- math helpers ---

    private static void BuildBasis(Vector3 baseDir, out Vector3 forward, out Vector3 right, out Vector3 up)
    {
        forward = baseDir.normalized;
        right = Mathf.Abs(Vector3.Dot(forward, Vector3.right)) > 0.99f ? Vector3.up : Vector3.right;
        up = Vector3.zero;
        Vector3.OrthoNormalize(ref forward, ref right, ref up);
    }

    private static void RotateAxesAroundForward(ref Vector3 right, ref Vector3 up, float angleDeg, Vector3 forward)
    {
        if (Mathf.Abs(angleDeg) <= EPS) return;
        Quaternion rot = Quaternion.AngleAxis(angleDeg, forward);
        right = rot * right;
        up = rot * up;
    }

    // Uniform-in-disk cone sampler (spreadDeg is cone angle, X/Y act as scale multipliers)
    private static Vector3 SampleCone(Vector3 baseDir, float spreadDeg, float spreadXMul, float spreadYMul)
    {
        BuildBasis(baseDir, out var forward, out var right, out var up);

        Vector2 disk = Random.insideUnitCircle;
        float tan = Mathf.Tan(Mathf.Deg2Rad * Mathf.Max(0f, spreadDeg));
        float tanX = tan * (spreadXMul <= 0f ? 1f : spreadXMul);
        float tanY = tan * (spreadYMul <= 0f ? 1f : spreadYMul);

        return (forward + right * (disk.x * tanX) + up * (disk.y * tanY)).normalized;
    }

    // Parameter in [-1, 1]; either evenly spaced or random
    private static float SampleLineParam(int i, int countOnAxis, bool deterministic)
    {
        if (countOnAxis <= 1) return 0f;

        if (deterministic)
        {
            // centers in each segment: (i + 0.5)/N -> [0,1] then to [-1,1]
            float u = (i + 0.5f) / countOnAxis;
            return Mathf.Lerp(-1f, 1f, u);
        }
        else
        {
            return Random.Range(-1f, 1f);
        }
    }

    // Offset forward by a single axis by an angular extent (degrees) scaled by t in [-1,1]
    private static Vector3 OffsetByAxis(Vector3 forward, Vector3 axis, float extentDeg, float tMinus1To1)
    {
        float tan = Mathf.Tan(Mathf.Deg2Rad * Mathf.Max(0f, extentDeg));
        return (forward + axis * (tMinus1To1 * tan)).normalized;
    }
    #endregion

    #region balistic helpers
    // Ballistic solver (speed + gravity -> initial velocity). Returns false if target unreachable.
    public static bool TrySolveBallisticArc(
     Vector3 origin, Vector3 target, float speed, float gravityY, bool highArc, out Vector3 velocity)
    {
        Vector3 delta = target - origin;
        Vector3 deltaXZ = new Vector3(delta.x, 0f, delta.z);
        float x = deltaXZ.magnitude;
        float y = delta.y;
        float g = Mathf.Abs(gravityY);
        float v2 = speed * speed;

        float under = v2 * v2 - g * (g * x * x + 2f * y * v2);
        if (under < 0f) { velocity = Vector3.zero; return false; }

        float root = Mathf.Sqrt(under);
        float angle = Mathf.Atan((v2 + (highArc ? root : -root)) / (g * x));

        Vector3 dirXZ = x > 1e-4f ? deltaXZ.normalized : Vector3.forward;
        velocity = dirXZ * (speed * Mathf.Cos(angle)) + Vector3.up * (speed * Mathf.Sin(angle));
        return true;
    }

    public static Vector3 SolveBallisticByTime(Vector3 origin, Vector3 target, float T, Vector3 gravity)
    {
        return (target - origin - 0.5f * gravity * (T * T)) / T;
    }

    public static bool TrySolveBallisticWithApex(
    Vector3 origin, Vector3 target, float apexY, float gravityMag, out Vector3 velocity)
    {
        velocity = Vector3.zero;
        float y0 = origin.y, yT = target.y;
        float minApex = Mathf.Max(y0, yT) + 0.01f;
        if (apexY < minApex) apexY = minApex;

        float vy0 = Mathf.Sqrt(Mathf.Max(0f, 2f * gravityMag * (apexY - y0)));
        float tUp = vy0 / gravityMag;

        float drop = Mathf.Max(0f, apexY - yT);
        float tDown = Mathf.Sqrt(2f * drop / gravityMag);

        float tTotal = tUp + tDown;

        Vector3 delta = target - origin;
        Vector3 deltaXZ = new Vector3(delta.x, 0f, delta.z);
        float xz = deltaXZ.magnitude;
        if (tTotal <= 1e-4f) return false;

        float vx = xz / tTotal;
        Vector3 dirXZ = xz > 1e-4f ? deltaXZ.normalized : Vector3.forward;

        velocity = dirXZ * vx + Vector3.up * vy0;
        return true;
    }

    public static bool PathIsClear(
    Vector3 origin,
    Vector3 v0,
    Vector3 gravity,
    float projectileRadius,
    float totalTime,
    int steps,
    LayerMask obstacleMask,
    out RaycastHit hit)
    {
        Vector3 prev = origin;
        for (int i = 1; i <= steps; i++)
        {
            float t = totalTime * i / steps;
            Vector3 p = origin + v0 * t + 0.5f * gravity * (t * t);
            Vector3 seg = p - prev;
            float dist = seg.magnitude;
            if (dist > 0f && Physics.SphereCast(prev, projectileRadius, seg.normalized, out hit, dist, obstacleMask))
                return false;
            prev = p;
        }
        hit = default;
        return true;
    }

    public static float LaunchAngleDeg(Vector3 v0)
    {
        float vh = new Vector2(v0.x, v0.z).magnitude;
        return Mathf.Rad2Deg * Mathf.Atan2(v0.y, vh);
    }

    // Build a velocity that GUARANTEES launch angle >= thetaMinDeg by picking a sufficient time-of-flight.
    public static Vector3 SolveBallisticByMinAngle(
        Vector3 origin,
        Vector3 target,
        float thetaMinDeg,
        float baseTime,     // your "feel" time; we'll raise it if needed to meet the angle
        Vector3 gravity
    )
    {
        Vector3 d = target - origin;
        float x = new Vector2(d.x, d.z).magnitude; // horizontal distance
        float dy = d.y;
        float g = gravity.magnitude;               // assume downward gravity
        float tan = Mathf.Tan(thetaMinDeg * Mathf.Deg2Rad);

        // From angle constraint: (dy + 0.5*g*T^2) / x >= tan(thetaMin)  =>  T >= sqrt( 2*(x*tan - dy)/g )
        float rhs = x * tan - dy;
        float Tmin = (rhs <= 0f) ? 0f : Mathf.Sqrt(2f * rhs / g);
        float T = Mathf.Max(baseTime, Tmin);

        return SolveBallisticByTime(origin, target, T, gravity);
    }
    #endregion
}
