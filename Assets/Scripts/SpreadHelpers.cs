using System.Collections.Generic;
using UnityEngine;

public static class SpreadHelpers
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
}
