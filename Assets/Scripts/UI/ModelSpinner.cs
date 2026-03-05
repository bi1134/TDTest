using UnityEngine;

/// <summary>
/// Rotates this transform slowly around the Y axis. Attach to 3D mesh previews in UI.
/// </summary>
public class ModelSpinner : MonoBehaviour
{
    public float rotationSpeed = 30f;

    void Update()
    {
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.Self);
    }
}
