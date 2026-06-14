using UnityEngine;

namespace LivingRoomPirates.Demo
{
    /// <summary>
    /// Runtime safety net for Quest performance: replaces Unity's high-poly
    /// primitive sphere/cylinder meshes with low-poly versions for all generated
    /// primitive gameplay objects. It does not touch authored meshes with unique names.
    /// </summary>
    public sealed class LrpPrimitivePerformanceOptimizer : MonoBehaviour
    {
        public bool optimizeOnStart = true;
        public bool keepScanningBriefly = true;
        public float scanInterval = 1f;
        public float scanDuration = 12f;

        private float _nextScan;
        private float _stopTime;
        private static Mesh _lowSphere;
        private static Mesh _lowCylinder;

        private void Start()
        {
            _stopTime = Time.time + scanDuration;
            if (optimizeOnStart) OptimizeNow();
        }

        private void Update()
        {
            if (!keepScanningBriefly || Time.time > _stopTime || Time.time < _nextScan) return;
            _nextScan = Time.time + Mathf.Max(0.2f, scanInterval);
            OptimizeNow();
        }

        [ContextMenu("Optimize Primitive Meshes Now")]
        public void OptimizeNow()
        {
            EnsureMeshes();
            foreach (MeshFilter mf in FindObjectsOfType<MeshFilter>())
            {
                if (mf == null || mf.sharedMesh == null) continue;
                string meshName = mf.sharedMesh.name.ToLowerInvariant();
                string objName = mf.gameObject.name.ToLowerInvariant();
                if (meshName.Contains("sphere") || objName.Contains("cannonball") || objName.Contains("knob") || objName.Contains("lantern") || objName.Contains("puff") || objName.Contains("flash"))
                {
                    mf.sharedMesh = _lowSphere;
                }
                else if (meshName.Contains("cylinder") || objName.Contains("rope") || objName.Contains("wheel") || objName.Contains("barrel") || objName.Contains("capstan"))
                {
                    mf.sharedMesh = _lowCylinder;
                }
            }
        }

        private static void EnsureMeshes()
        {
            if (_lowSphere == null) _lowSphere = BuildLowPolySphere(8, 4);
            if (_lowCylinder == null) _lowCylinder = BuildLowPolyCylinder(10);
        }

        private static Mesh BuildLowPolyCylinder(int sides)
        {
            sides = Mathf.Max(6, sides);
            Mesh mesh = new Mesh { name = "LRP_LowPolyCylinder" };
            Vector3[] verts = new Vector3[sides * 2 + 2];
            int topCenter = sides * 2;
            int bottomCenter = sides * 2 + 1;
            verts[topCenter] = new Vector3(0f, 0.5f, 0f);
            verts[bottomCenter] = new Vector3(0f, -0.5f, 0f);
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                float x = Mathf.Cos(a) * 0.5f;
                float z = Mathf.Sin(a) * 0.5f;
                verts[i] = new Vector3(x, 0.5f, z);
                verts[i + sides] = new Vector3(x, -0.5f, z);
            }
            int[] tris = new int[sides * 12];
            int t = 0;
            for (int i = 0; i < sides; i++)
            {
                int n = (i + 1) % sides;
                tris[t++] = i; tris[t++] = i + sides; tris[t++] = n;
                tris[t++] = n; tris[t++] = i + sides; tris[t++] = n + sides;
                tris[t++] = topCenter; tris[t++] = n; tris[t++] = i;
                tris[t++] = bottomCenter; tris[t++] = i + sides; tris[t++] = n + sides;
            }
            ReverseWinding(tris);
            ReverseWinding(tris);
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildLowPolySphere(int longitude, int latitude)
        {
            longitude = Mathf.Max(6, longitude);
            latitude = Mathf.Max(3, latitude);
            Mesh mesh = new Mesh { name = "LRP_LowPolySphere" };
            int vertCount = (latitude + 1) * longitude;
            Vector3[] verts = new Vector3[vertCount];
            int v = 0;
            for (int lat = 0; lat <= latitude; lat++)
            {
                float p = Mathf.PI * lat / latitude;
                float y = Mathf.Cos(p) * 0.5f;
                float r = Mathf.Sin(p) * 0.5f;
                for (int lon = 0; lon < longitude; lon++)
                {
                    float a = Mathf.PI * 2f * lon / longitude;
                    verts[v++] = new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
                }
            }
            int[] tris = new int[latitude * longitude * 6];
            int t = 0;
            for (int lat = 0; lat < latitude; lat++)
            {
                for (int lon = 0; lon < longitude; lon++)
                {
                    int a = lat * longitude + lon;
                    int b = lat * longitude + (lon + 1) % longitude;
                    int c = (lat + 1) * longitude + lon;
                    int d = (lat + 1) * longitude + (lon + 1) % longitude;
                    tris[t++] = a; tris[t++] = c; tris[t++] = b;
                    tris[t++] = b; tris[t++] = c; tris[t++] = d;
                }
            }
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void ReverseWinding(int[] tris)
        {
            if (tris == null) return;
            for (int i = 0; i + 2 < tris.Length; i += 3)
            {
                int tmp = tris[i + 1];
                tris[i + 1] = tris[i + 2];
                tris[i + 2] = tmp;
            }
        }
    }
}
