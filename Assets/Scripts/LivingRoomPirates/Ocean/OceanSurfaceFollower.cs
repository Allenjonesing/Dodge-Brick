using UnityEngine;

public class OceanSurfaceFollower : MonoBehaviour
{
    public float heightOffset = 0f;
    public bool alignToWaveNormal = true;
    public float normalAlignmentStrength = 4f;
    public float heightFollowStrength = 8f;

    private void LateUpdate()
    {
        OceanWorldController ocean = OceanWorldController.Instance;

        if (ocean == null)
        {
            return;
        }

        Vector3 position = transform.position;
        float targetY = ocean.SampleHeight(position) + heightOffset;

        position.y = Mathf.Lerp(position.y, targetY, Time.deltaTime * heightFollowStrength);
        transform.position = position;

        if (alignToWaveNormal)
        {
            Vector3 normal = ocean.SampleNormal(transform.position);
            Quaternion targetRotation = Quaternion.FromToRotation(transform.up, normal) * transform.rotation;
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * normalAlignmentStrength);
        }
    }
}
