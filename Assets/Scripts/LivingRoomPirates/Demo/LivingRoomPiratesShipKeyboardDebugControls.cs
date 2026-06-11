using System;
using System.Collections.Generic;
using UnityEngine;

public class LivingRoomPiratesShipKeyboardDebugControls : MonoBehaviour
{
    private static readonly string[] StationTokens =
    {
        "Steering",
        "Cannon",
        "Anchor",
        "Sail",
        "Spyglass",
        "Oar",
        "Repair",
        "Ammo",
        "Treasure",
        "Mast"
    };

    [Header("Scene References")]
    public BoundaryShipGenerator boundaryShipGenerator;
    public Transform shipRoot;

    [Header("Keyboard Movement")]
    public float translationSpeed = 1.8f;
    public float rotationSpeed = 75f;
    public float focusDistance = 1.1f;

    [Header("Overlay")]
    public bool showOverlay = true;

    private readonly List<Transform> stationTargets = new List<Transform>();
    private int selectedStationIndex = -1;
    private Camera debugCamera;

    private void Start()
    {
        RefreshStationTargets();
        Debug.Log("[ShipKeyboardDebugControls] Keyboard debug enabled. Use H to toggle the overlay.");
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.H))
        {
            showOverlay = !showOverlay;
        }

        HandleMovement();
        HandleActions();
    }

    private void HandleMovement()
    {
        if (shipRoot == null)
        {
            return;
        }

        Camera activeCamera = GetDebugCamera();
        Vector3 forward = Flatten(activeCamera != null ? activeCamera.transform.forward : Vector3.forward);
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.forward;
        }

        forward.Normalize();
        Vector3 right = new Vector3(forward.z, 0f, -forward.x);

        Vector3 translation = Vector3.zero;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
        {
            translation += forward;
        }

        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
        {
            translation -= forward;
        }

        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
        {
            translation -= right;
        }

        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
        {
            translation += right;
        }

        if (translation.sqrMagnitude > 1f)
        {
            translation.Normalize();
        }

        if (translation.sqrMagnitude > 0f)
        {
            shipRoot.position += translation * translationSpeed * Time.unscaledDeltaTime;
        }

        float rotationInput = 0f;
        if (Input.GetKey(KeyCode.Q))
        {
            rotationInput -= 1f;
        }

        if (Input.GetKey(KeyCode.E))
        {
            rotationInput += 1f;
        }

        if (Mathf.Abs(rotationInput) > 0.001f)
        {
            Vector3 pivot = GetPlayerFloorPosition();
            shipRoot.RotateAround(pivot, Vector3.up, rotationInput * rotationSpeed * Time.unscaledDeltaTime);
        }
    }

    private void HandleActions()
    {
        if (Input.GetKeyDown(KeyCode.G))
        {
            RegenerateShip();
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetShipRoot();
        }

        if (Input.GetKeyDown(KeyCode.LeftBracket))
        {
            SelectRelative(-1);
        }

        if (Input.GetKeyDown(KeyCode.RightBracket))
        {
            SelectRelative(1);
        }

        if (Input.GetKeyDown(KeyCode.F))
        {
            FocusSelectedStation();
        }

        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            FocusStationContaining("Steering");
        }

        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            FocusStationContaining("CannonForward");
        }

        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            FocusStationContaining("CannonPort");
        }

        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            FocusStationContaining("CannonStar");
        }

        if (Input.GetKeyDown(KeyCode.Alpha5))
        {
            FocusStationContaining("Anchor");
        }

        if (Input.GetKeyDown(KeyCode.Alpha6))
        {
            FocusStationContaining("Sail");
        }

        if (Input.GetKeyDown(KeyCode.Alpha7))
        {
            FocusStationContaining("Ammo");
        }

        if (Input.GetKeyDown(KeyCode.Alpha8))
        {
            FocusStationContaining("Repair");
        }

        if (Input.GetKeyDown(KeyCode.Alpha9))
        {
            FocusStationContaining("Spyglass");
        }

        if (Input.GetKeyDown(KeyCode.Alpha0))
        {
            FocusStationContaining("Mast");
        }
    }

    private void RegenerateShip()
    {
        if (boundaryShipGenerator == null)
        {
            return;
        }

        boundaryShipGenerator.RegenerateShip();
        RefreshStationTargets();
        Debug.Log("[ShipKeyboardDebugControls] Regenerated the debug ship layout.");
    }

    private void ResetShipRoot()
    {
        if (shipRoot == null)
        {
            return;
        }

        Vector3 playerFloor = GetPlayerFloorPosition();
        shipRoot.position = new Vector3(playerFloor.x, shipRoot.position.y, playerFloor.z);
        shipRoot.rotation = Quaternion.identity;
        Debug.Log("[ShipKeyboardDebugControls] Reset ship root to the player position.");
    }

    private void SelectRelative(int direction)
    {
        RefreshStationTargets();
        if (stationTargets.Count == 0)
        {
            return;
        }

        if (selectedStationIndex < 0)
        {
            selectedStationIndex = 0;
        }
        else
        {
            selectedStationIndex = (selectedStationIndex + direction + stationTargets.Count) % stationTargets.Count;
        }

        Debug.Log($"[ShipKeyboardDebugControls] Selected station {stationTargets[selectedStationIndex].name}.");
    }

    private void FocusSelectedStation()
    {
        RefreshStationTargets();
        if (stationTargets.Count == 0)
        {
            return;
        }

        if (selectedStationIndex < 0)
        {
            selectedStationIndex = 0;
        }

        FocusStation(stationTargets[selectedStationIndex]);
    }

    private void FocusStationContaining(string token)
    {
        RefreshStationTargets();
        if (stationTargets.Count == 0)
        {
            return;
        }

        for (int i = 0; i < stationTargets.Count; i++)
        {
            if (stationTargets[i].name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                selectedStationIndex = i;
                FocusStation(stationTargets[i]);
                return;
            }
        }

        Debug.Log($"[ShipKeyboardDebugControls] No station matching '{token}' exists on the current ship tier.");
    }

    private void FocusStation(Transform station)
    {
        if (shipRoot == null || station == null)
        {
            return;
        }

        Camera activeCamera = GetDebugCamera();
        Vector3 forward = Flatten(activeCamera != null ? activeCamera.transform.forward : Vector3.forward);
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.forward;
        }

        forward.Normalize();
        Vector3 desiredPosition = GetPlayerFloorPosition() + forward * focusDistance;
        Vector3 delta = desiredPosition - station.position;
        delta.y = 0f;
        shipRoot.position += delta;
        Debug.Log($"[ShipKeyboardDebugControls] Focused {station.name} at {focusDistance:F1}m in front of the player.");
    }

    private void RefreshStationTargets()
    {
        stationTargets.Clear();

        Transform stationRoot = null;
        if (boundaryShipGenerator != null)
        {
            stationRoot = boundaryShipGenerator.shipGeneratedRoot != null
                ? boundaryShipGenerator.shipGeneratedRoot
                : boundaryShipGenerator.transform;
        }

        if (stationRoot == null)
        {
            selectedStationIndex = -1;
            return;
        }

        foreach (Transform child in stationRoot)
        {
            if (!IsStationRoot(child.name))
            {
                continue;
            }

            stationTargets.Add(child);
        }

        stationTargets.Sort(CompareStations);
        if (stationTargets.Count == 0)
        {
            selectedStationIndex = -1;
        }
        else if (selectedStationIndex >= stationTargets.Count)
        {
            selectedStationIndex = stationTargets.Count - 1;
        }
    }

    private static int CompareStations(Transform left, Transform right)
    {
        int depthCompare = right.localPosition.z.CompareTo(left.localPosition.z);
        if (depthCompare != 0)
        {
            return depthCompare;
        }

        int widthCompare = left.localPosition.x.CompareTo(right.localPosition.x);
        if (widthCompare != 0)
        {
            return widthCompare;
        }

        return string.Compare(left.name, right.name, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsStationRoot(string objectName)
    {
        if (string.IsNullOrEmpty(objectName) || objectName.Contains("_"))
        {
            return false;
        }

        for (int i = 0; i < StationTokens.Length; i++)
        {
            if (objectName.IndexOf(StationTokens[i], StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private Camera GetDebugCamera()
    {
        if (debugCamera != null && debugCamera.isActiveAndEnabled)
        {
            return debugCamera;
        }

        debugCamera = Camera.main;
        if (debugCamera != null)
        {
            return debugCamera;
        }

        debugCamera = FindObjectOfType<Camera>();
        return debugCamera;
    }

    private Vector3 GetPlayerFloorPosition()
    {
        Camera activeCamera = GetDebugCamera();
        Vector3 playerPosition = activeCamera != null ? activeCamera.transform.position : Vector3.zero;
        playerPosition.y = shipRoot != null ? shipRoot.position.y : 0f;
        return playerPosition;
    }

    private static Vector3 Flatten(Vector3 vector)
    {
        vector.y = 0f;
        return vector;
    }

    private void OnGUI()
    {
        if (!showOverlay)
        {
            return;
        }

        string stationLabel = selectedStationIndex >= 0 && selectedStationIndex < stationTargets.Count
            ? stationTargets[selectedStationIndex].name
            : "None";

        GUI.Box(
            new Rect(12f, 12f, 420f, 230f),
            "Ship Keyboard Debug\n\n" +
            "Move ship to you: WASD or Arrow Keys\n" +
            "Orbit around you: Q / E\n" +
            "Cycle stations: [ / ]\n" +
            "Focus selected station: F\n" +
            "Reset ship root: R\n" +
            "Regenerate ship: G\n" +
            "Common jumps: 1 Wheel, 2 Bow Cannon, 3 Port, 4 Starboard, 5 Anchor\n" +
            "Common jumps: 6 Sail, 7 Ammo, 8 Repair, 9 Spyglass, 0 Mast\n" +
            "Storm presets: F1 Calm, F2 Swell, F3 Storm\n" +
            $"Selected: {stationLabel}\n" +
            "Toggle overlay: H");
    }
}