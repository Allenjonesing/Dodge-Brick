using UnityEngine;

/// <summary>
/// Single source of truth for Living Room Pirates water contact.
/// Attach to the scene-authored Water1 object. This component does not create water.
/// It samples the same wave equation used by OceanWaveMeshDeformer and solves Water1 Y directly.
/// </summary>
[DefaultExecutionOrder(50)]
[DisallowMultipleComponent]
public sealed class AuthoritativeOceanSurface : MonoBehaviour
{
    [Header("References")]
    public OceanWaveMeshDeformer deformer;

    [Header("Contact")]
    [Tooltip("Negative value means the water surface is slightly below the contact point, making the object appear slightly raised.")]
    public float surfaceOffset = -0.05f;
    public bool snapWaterRootInstantly = true;
    public float snapSharpness = 30f;

    [Header("Debug")]
    public bool drawDebugContact = true;
    public bool logDebug = false;
    public float logInterval = 1f;

    private float _nextLog;
    private Vector3 _lastContact;
    private Vector3 _lastSurface;

    private void Awake()
    {
        if (deformer == null)
        {
            deformer = GetComponent<OceanWaveMeshDeformer>();
        }
    }

    private void OnDrawGizmos()
    {
        if (!drawDebugContact)
        {
            return;
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(_lastContact, 0.08f);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(_lastSurface, 0.08f);
        Gizmos.DrawLine(_lastContact, _lastSurface);
    }

    public float SampleWaveOffset(Vector3 worldPosition)
    {
        if (deformer == null)
        {
            deformer = GetComponent<OceanWaveMeshDeformer>();
        }

        if (deformer != null)
        {
            return deformer.SampleWaveOffsetWorld(worldPosition);
        }

        return 0f;
    }

    public float SampleSurfaceY(Vector3 worldPosition)
    {
        return transform.position.y + SampleWaveOffset(worldPosition);
    }

    public Vector3 SnapObjectToSurface(Vector3 objectPosition, float contactY, float objectSurfaceOffset)
    {
        float surfaceY = SampleSurfaceY(objectPosition);
        float delta = (surfaceY + objectSurfaceOffset) - contactY;
        objectPosition.y += delta;
        return objectPosition;
    }

    public float SolveWaterRootYForContact(Vector3 contactPoint, float offset)
    {
        // surfaceY = waterRootY + waveOffset(contactPoint.xz)
        // desired surfaceY = contactPoint.y + offset
        // waterRootY = desired - waveOffset
        return contactPoint.y + offset - SampleWaveOffset(contactPoint);
    }

    public void SnapWaterRootToContact(Vector3 contactPoint, float offset)
    {
        float targetY = SolveWaterRootYForContact(contactPoint, offset);
        Vector3 p = transform.position;
        p.y = snapWaterRootInstantly || snapSharpness <= 0f
            ? targetY
            : Mathf.Lerp(p.y, targetY, 1f - Mathf.Exp(-snapSharpness * Time.deltaTime));
        transform.position = p;

        _lastContact = contactPoint;
        _lastSurface = new Vector3(contactPoint.x, SampleSurfaceY(contactPoint), contactPoint.z);

        if (logDebug && Application.isPlaying && Time.time >= _nextLog)
        {
            _nextLog = Time.time + Mathf.Max(0.1f, logInterval);
            Debug.Log($"[AuthoritativeOceanSurface] contactY={contactPoint.y:F3} surfaceY={_lastSurface.y:F3} rootY={transform.position.y:F3} offset={offset:F3} wave={SampleWaveOffset(contactPoint):F3}", this);
        }
    }
}
