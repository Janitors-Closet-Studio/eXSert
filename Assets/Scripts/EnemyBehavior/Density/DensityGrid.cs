// DensityGrid.cs
// Purpose: Spatial density grid used to stamp agent presence and provide cost/density samples for path planners and crowd steering.
// Works with: CrowdController, planners (FlowField, A*), EnemyBehaviorProfile for personal space radius.

using UnityEngine;
#pragma warning disable CS0414

namespace EnemyBehavior.Density
{
    public sealed class DensityGrid : MonoBehaviour
    {
        public static DensityGrid Instance { get; private set; }

        [Header("Component Help")]
        [SerializeField, TextArea(4, 8)] private string inspectorHelp =
            "DensityGrid: a 2D XZ grid that accumulates agent presence (via Stamp) and decays over time.\n" +
            "Planners (A*/FlowField) sample this to avoid high-density areas when density multipliers > 0.\n" +
            "Configure worldOrigin/worldSize to cover your arena (cyan gizmo), cellSize ~1m, and decay rate.\n" +
            "Tick Rate (Hz) controls how often decay runs. Larger scenes may prefer lower tick rates.\n" +
            "Optional: enable per-cell visualization gizmo for debug (cost overlay).";

        [Header("World Bounds")]
        [SerializeField] Vector2 worldOrigin = new Vector2(-50f, -50f);
        [SerializeField] Vector2 worldSize = new Vector2(100f, 100f);
        [SerializeField] float cellSize = 1.0f;

        [Header("Decay/Ticking")]
        [SerializeField, Tooltip("Exponential decay rate (per second) for stamped values.")] float decayPerSecond = 0.5f;
        [SerializeField, Tooltip("How many times per second the grid decay updates.")] float tickHz = 30f;

        [Header("Debug Visualization")]
        [SerializeField, Tooltip("Draw per-cell density as colored quads (Editor only, when selected). Heavy on large grids.")] private bool drawCells = false;
        [SerializeField, Tooltip("Max alpha for cell quads at highest density.")] private float cellMaxAlpha = 0.3f;

        private int _w, _h;
        private float[] _cells;
        private Coroutine _tick;

        void Awake()
        {
            Instance = this;
            _w = Mathf.Max(1, Mathf.CeilToInt(worldSize.x / cellSize));
            _h = Mathf.Max(1, Mathf.CeilToInt(worldSize.y / cellSize));
            _cells = new float[_w * _h];
        }
#pragma warning restore CS0414

        void OnEnable()
        {
            if (_tick != null) StopCoroutine(_tick);
            _tick = StartCoroutine(TickLoop());
        }

        void OnDisable()
        {
            if (_tick != null) { StopCoroutine(_tick); _tick = null; }
        }

        private System.Collections.IEnumerator TickLoop()
        {
            float interval = Mathf.Max(0.01f, tickHz > 0f ? 1f / tickHz : 0.05f);
            var wait = WaitForSecondsCache.Get(interval);
            while (enabled)
            {
                UpdateGrid(interval);
                yield return wait;
            }
        }

        public void Stamp(Vector3 pos, float radius, float weight)
        {
            int cx = Mathf.FloorToInt((pos.x - worldOrigin.x) / cellSize);
            int cz = Mathf.FloorToInt((pos.z - worldOrigin.y) / cellSize);
            int r = Mathf.CeilToInt(radius / cellSize);
            int xmin = Mathf.Clamp(cx - r, 0, _w - 1);
            int xmax = Mathf.Clamp(cx + r, 0, _w - 1);
            int zmin = Mathf.Clamp(cz - r, 0, _h - 1);
            int zmax = Mathf.Clamp(cz + r, 0, _h - 1);
            for (int z = zmin; z <= zmax; z++)
            {
                for (int x = xmin; x <= xmax; x++)
                {
                    int idx = z * _w + x;
                    // simple disk falloff
                    float dx = (x - cx) * cellSize;
                    float dz = (z - cz) * cellSize;
                    float d = Mathf.Sqrt(dx * dx + dz * dz);
                    if (d <= radius)
                    {
                        float fall = 1f - (d / radius);
                        _cells[idx] += weight * fall;
                    }
                }
            }
        }

        public void UpdateGrid(float dt)
        {
            float k = Mathf.Exp(-decayPerSecond * dt);
            for (int i = 0; i < _cells.Length; i++) _cells[i] *= k;
        }

        public float SampleCost(Vector3 pos)
        {
            float fx = (pos.x - worldOrigin.x) / cellSize;
            float fz = (pos.z - worldOrigin.y) / cellSize;
            int x0 = Mathf.FloorToInt(fx);
            int z0 = Mathf.FloorToInt(fz);

            // If out of range, return zero cost
            if (x0 < 0 || z0 < 0 || x0 >= _w - 1 || z0 >= _h - 1)
            {
                return 0f;
            }

            float tx = fx - x0;
            float tz = fz - z0;

            float c00 = _cells[z0 * _w + x0];
            float c10 = _cells[z0 * _w + (x0 + 1)];
            float c01 = _cells[(z0 + 1) * _w + x0];
            float c11 = _cells[(z0 + 1) * _w + (x0 + 1)];

            float cx0 = Mathf.Lerp(c00, c10, tx);
            float cx1 = Mathf.Lerp(c01, c11, tx);
            return Mathf.Lerp(cx0, cx1, tz);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Draw world bounds as a wireframe rectangle on XZ plane
            Vector3 center = new Vector3(worldOrigin.x + worldSize.x * 0.5f, 0f, worldOrigin.y + worldSize.y * 0.5f);
            Vector3 size = new Vector3(worldSize.x, 0.05f, worldSize.y);
            Gizmos.color = new Color(0f, 1f, 1f, 0.25f);
            Gizmos.DrawCube(center, size);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(center, size);

            // Optional per-cell overlay
            if (!drawCells || _cells == null || _cells.Length == 0) return;

            float max = 0f;
            for (int i = 0; i < _cells.Length; i++) if (_cells[i] > max) max = _cells[i];
            if (max <= 0f) return;

            for (int z = 0; z < _h; z++)
            {
                for (int x = 0; x < _w; x++)
                {
                    float v = _cells[z * _w + x] / max; // 0..1
                    if (v <= 0.001f) continue;
                    Vector3 c = new Vector3(worldOrigin.x + (x + 0.5f) * cellSize, 0.01f, worldOrigin.y + (z + 0.5f) * cellSize);
                    Vector3 s = new Vector3(cellSize * 0.95f, 0.02f, cellSize * 0.95f);
                    Gizmos.color = new Color(v, 0f, 1f - v, Mathf.Clamp01(v * cellMaxAlpha));
                    Gizmos.DrawCube(c, s);
                }
            }
        }
#endif
    }
}
