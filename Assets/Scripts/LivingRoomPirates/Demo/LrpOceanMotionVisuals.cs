using System.Collections.Generic;
using UnityEngine;

namespace LivingRoomPirates.Demo
{
    /// <summary>
    /// Low-cost motion cues for the locked-ship treadmill illusion.
    /// Wind wisps are airborne and travel with the wave/wind direction.
    /// Wake ripples spawn at the hull and remain on the water surface until they fade.
    /// </summary>
    [DefaultExecutionOrder(250)]
    public sealed class LrpOceanMotionVisuals : MonoBehaviour
    {
        public Transform shipRoot;
        public Transform waterOne;
        public OceanWorldController ocean;

        [Header("Wind")]
        public int windLineCount = 22;
        public float windRadius = 24f;
        public float windLineLength = 2.6f;
        public float windHeight = 2.15f;
        public float windSpeed = 5.2f;

        [Header("Wake / Ripples")]
        public int wakePoolCount = 28;
        public float wakeSpawnIntervalAtFullSpeed = 0.85f;
        public float wakeLifetime = 4.2f;
        public float wakeWidth = 2.4f;
        public float wakeSternOffset = 0.95f;
        public float surfaceLift = 0.045f;
        public float minimumWakeSpeed = 0.05f;
        public bool buildOnStart = true;

        private readonly List<Transform> _windLines = new List<Transform>();
        private readonly List<WakeRipple> _wakePool = new List<WakeRipple>();
        private Material _windMat;
        private Material _wakeMat;
        private float _wakeTimer;
        private int _wakeCursor;
        private float _windPhase;

        private struct WakeRipple
        {
            public Transform t;
            public float age;
            public float life;
            public Vector3 drift;
            public float baseScale;
            public Vector2 oceanPosition;
        }

        private void Start()
        {
            if (buildOnStart) Build();
        }

        private void LateUpdate()
        {
            ResolveRefs();
            if (_windLines.Count == 0 || _wakePool.Count == 0) Build();
            UpdateWindLines();
            UpdateWakeRipples();
        }

        private void ResolveRefs()
        {
            if (shipRoot == null)
            {
                GameObject root = GameObject.Find("shipGeneratedRoot");
                if (root != null) shipRoot = root.transform;
            }
            if (waterOne == null)
            {
                GameObject w = GameObject.Find("Water1");
                if (w != null) waterOne = w.transform;
            }
            if (ocean == null) ocean = OceanWorldController.Instance != null ? OceanWorldController.Instance : FindObjectOfType<OceanWorldController>();
        }

        public void Build()
        {
            ClearWind();
            ClearWake();

            _windMat = MakeTransparentMat("LRP_WindLine_Mat", new Color(0.85f, 0.95f, 1f, 0.30f));
            _wakeMat = MakeTransparentMat("LRP_WakeFoam_Mat", new Color(0.92f, 0.98f, 1f, 0.46f));

            for (int i = 0; i < windLineCount; i++)
            {
                Transform t = CreateFlatCue("AirWindWisp_" + i, _windMat, new Vector3(0.025f, 0.018f, windLineLength)).transform;
                _windLines.Add(t);
            }

            for (int i = 0; i < wakePoolCount; i++)
            {
                Transform t = CreateFlatCue("ShipSurfaceRipple_" + i, _wakeMat, new Vector3(0.35f, 0.012f, 0.075f)).transform;
                t.gameObject.SetActive(false);
                _wakePool.Add(new WakeRipple { t = t, age = 99f, life = wakeLifetime, drift = Vector3.zero, baseScale = 1f });
            }
        }

        private GameObject CreateFlatCue(string name, Material mat, Vector3 scale)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(transform, true);
            go.transform.localScale = scale;
            Renderer r = go.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = mat;
            Collider c = go.GetComponent<Collider>();
            if (c != null) Destroy(c);
            return go;
        }

        private void UpdateWindLines()
        {
            Vector3 center = shipRoot != null ? shipRoot.position : Vector3.zero;
            Vector3 windDir = ResolveWaveDirection();
            Quaternion rot = Quaternion.LookRotation(windDir, Vector3.up);
            _windPhase += Time.deltaTime * windSpeed;

            for (int i = 0; i < _windLines.Count; i++)
            {
                float lane = (i * 137.508f) * Mathf.Deg2Rad;
                float lateral = Mathf.Lerp(-windRadius, windRadius, ((i * 47) % 100) / 100f);
                float along = Mathf.Repeat(_windPhase + i * (windRadius * 0.37f), windRadius * 2f) - windRadius;
                Vector3 right = new Vector3(windDir.z, 0f, -windDir.x).normalized;
                Vector3 p = center + windDir * along + right * lateral;
                p += new Vector3(Mathf.Cos(lane), 0f, Mathf.Sin(lane)) * 1.7f;
                p.y = center.y + windHeight + Mathf.Sin(Time.time * 1.6f + i) * 0.12f;

                Transform line = _windLines[i];
                line.position = p;
                line.rotation = rot;
            }
        }

        private void UpdateWakeRipples()
        {
            float speed = ResolveTravelSpeed();
            Vector3 travelDir = ResolveTravelDirection();

            if (speed > minimumWakeSpeed && shipRoot != null)
            {
                float interval = wakeSpawnIntervalAtFullSpeed / Mathf.Clamp(speed, 0.5f, 3.5f);
                _wakeTimer += Time.deltaTime;
                while (_wakeTimer >= interval)
                {
                    _wakeTimer -= interval;
                    SpawnWakePair(travelDir, speed);
                }
            }
            else
            {
                _wakeTimer = 0f;
            }

            for (int i = 0; i < _wakePool.Count; i++)
            {
                WakeRipple w = _wakePool[i];
                if (w.t == null) continue;
                if (!w.t.gameObject.activeSelf) { _wakePool[i] = w; continue; }

                w.age += Time.deltaTime;
                if (w.age >= w.life)
                {
                    w.t.gameObject.SetActive(false);
                    _wakePool[i] = w;
                    continue;
                }

                // Wake ripples are ocean-space marks, just like debris. They do not fake-drift
                // along a line; they remain at their ocean coordinate while the virtual ship
                // moves away from them.
                w.oceanPosition += new Vector2(w.drift.x, w.drift.z) * Time.deltaTime;
                Vector3 p = OceanToWorld(w.oceanPosition);
                p.y = SampleY(p) + surfaceLift;
                w.t.position = p;

                float t = Mathf.Clamp01(w.age / w.life);
                float alphaScale = 1f - t;
                w.t.localScale = new Vector3(w.baseScale * Mathf.Lerp(0.55f, 1.45f, t), 0.012f, Mathf.Lerp(0.08f, 0.20f, t));

                Renderer r = w.t.GetComponent<Renderer>();
                if (r != null && r.material != null)
                {
                    Color c = r.material.color;
                    c.a = 0.46f * alphaScale;
                    r.material.color = c;
                }

                _wakePool[i] = w;
            }
        }

        private void SpawnWakePair(Vector3 travelDir, float speed)
        {
            Vector3 center = shipRoot != null ? shipRoot.position : Vector3.zero;
            Vector3 right = new Vector3(travelDir.z, 0f, -travelDir.x).normalized;
            Quaternion rot = Quaternion.LookRotation(travelDir, Vector3.up);
            for (int sideIndex = 0; sideIndex < 2; sideIndex++)
            {
                float side = sideIndex == 0 ? -1f : 1f;
                WakeRipple w = _wakePool[_wakeCursor];
                _wakeCursor = (_wakeCursor + 1) % Mathf.Max(1, _wakePool.Count);
                if (w.t == null) continue;

                Vector3 p = center - travelDir * wakeSternOffset + right * side * (wakeWidth * 0.32f);
                p.y = SampleY(p) + surfaceLift;
                w.oceanPosition = WorldToOcean2(p);
                w.t.position = p;
                w.t.rotation = rot;
                w.t.gameObject.SetActive(true);
                w.age = 0f;
                w.life = wakeLifetime;
                w.baseScale = Mathf.Lerp(0.25f, wakeWidth, Mathf.Clamp01(speed / 4f));
                w.drift = right * side * 0.08f;
                _wakePool[(_wakeCursor - 1 + _wakePool.Count) % _wakePool.Count] = w;
            }
        }


        private Vector2 GetVirtualShipOceanPosition()
        {
            WaterOneGrid3x3 grid = waterOne != null ? waterOne.GetComponent<WaterOneGrid3x3>() : null;
            return grid != null ? grid.virtualShipOceanPosition : Vector2.zero;
        }

        private float GetHeadingDegrees()
        {
            WaterOneGrid3x3 grid = waterOne != null ? waterOne.GetComponent<WaterOneGrid3x3>() : null;
            return grid != null ? grid.visualYawDegrees : (ocean != null ? ocean.shipHeadingDegrees : 0f);
        }

        private Vector2 WorldToOcean2(Vector3 world)
        {
            Vector3 center = shipRoot != null ? shipRoot.position : Vector3.zero;
            Vector3 relWorld = world - center;
            Vector3 relOcean = Quaternion.Euler(0f, GetHeadingDegrees(), 0f) * relWorld;
            Vector2 shipOcean = GetVirtualShipOceanPosition();
            return shipOcean + new Vector2(relOcean.x, relOcean.z);
        }

        private Vector3 OceanToWorld(Vector2 oceanPos)
        {
            Vector3 center = shipRoot != null ? shipRoot.position : Vector3.zero;
            Vector2 shipOcean = GetVirtualShipOceanPosition();
            Vector2 rel = oceanPos - shipOcean;
            Vector3 relWorld = Quaternion.Euler(0f, -GetHeadingDegrees(), 0f) * new Vector3(rel.x, 0f, rel.y);
            return center + relWorld;
        }

        private Vector3 ResolveWaveDirection()
        {
            WaterOneGrid3x3 grid = waterOne != null ? waterOne.GetComponent<WaterOneGrid3x3>() : null;
            Transform root = grid != null ? grid.TileGridRoot : null;
            Vector3 dir = root != null ? root.TransformDirection(Vector3.left) : Vector3.left;
            dir.y = 0f;
            return dir.sqrMagnitude > 0.001f ? dir.normalized : Vector3.left;
        }

        private Vector3 ResolveTravelDirection()
        {
            Vector2 d = ocean != null && ocean.waterEscalatorDirection.sqrMagnitude > 0.001f ? ocean.waterEscalatorDirection.normalized : Vector2.up;
            Vector3 v = new Vector3(d.x, 0f, d.y);
            return v.sqrMagnitude > 0.001f ? v.normalized : Vector3.forward;
        }

        private float ResolveTravelSpeed()
        {
            if (ocean == null || !ocean.enableWaterEscalatorTravel) return 0f;
            return Mathf.Max(0f, ocean.waterEscalatorSpeed);
        }

        private float SampleY(Vector3 p)
        {
            return ocean != null ? ocean.SampleHeight(p) : p.y;
        }

        private static Material MakeTransparentMat(string name, Color color)
        {
            Material mat = new Material(Shader.Find("Standard"));
            mat.name = name;
            mat.color = color;
            mat.SetFloat("_Mode", 3f);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
            return mat;
        }

        private void ClearWind()
        {
            for (int i = _windLines.Count - 1; i >= 0; i--)
            {
                if (_windLines[i] == null) continue;
                if (Application.isPlaying) Destroy(_windLines[i].gameObject); else DestroyImmediate(_windLines[i].gameObject);
            }
            _windLines.Clear();
        }

        private void ClearWake()
        {
            for (int i = _wakePool.Count - 1; i >= 0; i--)
            {
                if (_wakePool[i].t == null) continue;
                if (Application.isPlaying) Destroy(_wakePool[i].t.gameObject); else DestroyImmediate(_wakePool[i].t.gameObject);
            }
            _wakePool.Clear();
        }
    }
}
