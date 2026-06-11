using UnityEngine;

public class WaterHorizonStabilizer : MonoBehaviour
{
    [Range(0f, 1f)]
    public float cancelPitch = 0.9f;

    [Range(0f, 1f)]
    public float cancelRoll = 0.9f;

    [Range(0f, 10f)]
    public float bobAmount = 0.08f;

    [Range(0.01f, 2f)]
    public float bobFrequency = 0.14f;

    private Quaternion _baseLocalRotation;
    private Vector3 _baseLocalPosition;

    private void OnEnable()
    {
        _baseLocalRotation = transform.localRotation;
        _baseLocalPosition = transform.localPosition;
    }

    private void LateUpdate()
    {
        Transform parent = transform.parent;
        if (parent == null)
        {
            return;
        }

        Vector3 parentEuler = parent.rotation.eulerAngles;
        float parentPitch = NormalizeAngle(parentEuler.x);
        float parentRoll = NormalizeAngle(parentEuler.z);

        float localBob = Mathf.Sin(Time.timeSinceLevelLoad * bobFrequency * Mathf.PI * 2f) * bobAmount;
        transform.localPosition = _baseLocalPosition + new Vector3(0f, localBob, 0f);
        transform.localRotation = _baseLocalRotation * Quaternion.Euler(-parentPitch * cancelPitch, 0f, -parentRoll * cancelRoll);
    }

    private static float NormalizeAngle(float angle)
    {
        while (angle > 180f)
        {
            angle -= 360f;
        }

        while (angle < -180f)
        {
            angle += 360f;
        }

        return angle;
    }
}