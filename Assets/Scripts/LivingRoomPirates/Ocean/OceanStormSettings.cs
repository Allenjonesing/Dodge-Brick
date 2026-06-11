using UnityEngine;

[System.Serializable]
public class OceanStormSettings
{
    public float waveHeight = 1.2f;
    public float waveScale = 0.035f;
    public float waveSpeed = 0.06f;

    public float secondaryBlend = 0.45f;
    public float secondarySpeed = 0.11f;

    public float shipPitchDegrees = 3f;
    public float shipRollDegrees = 4f;
    public float shipBobHeight = 0.25f;

    public float horizonPitchCancel = 0.9f;
    public float horizonRollCancel = 0.9f;

    public static OceanStormSettings FromPreset(StormMotionPreset preset)
    {
        switch (preset)
        {
            case StormMotionPreset.Calm:
                return new OceanStormSettings
                {
                    waveHeight = 0.35f,
                    waveScale = 0.025f,
                    waveSpeed = 0.025f,
                    secondaryBlend = 0.2f,
                    secondarySpeed = 0.04f,
                    shipPitchDegrees = 0.7f,
                    shipRollDegrees = 1.0f,
                    shipBobHeight = 0.05f,
                    horizonPitchCancel = 0.8f,
                    horizonRollCancel = 0.8f
                };

            case StormMotionPreset.RollingSea:
                return new OceanStormSettings
                {
                    waveHeight = 1.2f,
                    waveScale = 0.035f,
                    waveSpeed = 0.06f,
                    secondaryBlend = 0.45f,
                    secondarySpeed = 0.11f,
                    shipPitchDegrees = 3f,
                    shipRollDegrees = 4f,
                    shipBobHeight = 0.25f,
                    horizonPitchCancel = 0.9f,
                    horizonRollCancel = 0.9f
                };

            case StormMotionPreset.HeavySwell:
            default:
                return new OceanStormSettings
                {
                    waveHeight = 2.4f,
                    waveScale = 0.022f,
                    waveSpeed = 0.045f,
                    secondaryBlend = 0.55f,
                    secondarySpeed = 0.085f,
                    shipPitchDegrees = 6f,
                    shipRollDegrees = 8f,
                    shipBobHeight = 0.55f,
                    horizonPitchCancel = 0.92f,
                    horizonRollCancel = 0.94f
                };
        }
    }
}
