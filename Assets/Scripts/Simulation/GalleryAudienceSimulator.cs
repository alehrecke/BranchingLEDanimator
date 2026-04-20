using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using BranchingLEDAnimator.Core;
using BranchingLEDAnimator.Player;

namespace BranchingLEDAnimator.Simulation
{
    /// <summary>
    /// Simulates a gallery full of visitors interacting with all sculptures.
    /// Visitors wander between sculptures, approach endpoints, and trigger interactions.
    /// Attach to the Gallery root GameObject.
    /// </summary>
    public class GalleryAudienceSimulator : MonoBehaviour
    {
        [Header("Crowd Size")]
        [Range(1, 30)]
        [SerializeField] private int visitorCount = 6;

        [Header("Movement")]
        [Tooltip("How fast visitors walk (units/sec)")]
        [Range(0.5f, 50f)]
        [SerializeField] private float walkSpeed = 1.5f;

        [Tooltip("Distance at which a visitor can interact with an endpoint")]
        [Range(1f, 20f)]
        [SerializeField] private float reachDistance = 5f;

        [Tooltip("How long a visitor lingers near a sculpture before moving on")]
        [SerializeField] private Vector2 lingerDuration = new Vector2(3f, 12f);

        [Header("Interaction")]
        [Tooltip("How long a visitor holds a touch")]
        [SerializeField] private Vector2 holdDuration = new Vector2(0.5f, 4f);

        [Tooltip("Pause between releasing and touching again")]
        [SerializeField] private Vector2 pauseBetweenTouches = new Vector2(0.3f, 2f);

        [Tooltip("Chance a visitor touches an endpoint when in range (per second)")]
        [Range(0f, 1f)]
        [SerializeField] private float touchChancePerSecond = 0.7f;

        [Tooltip("Maximum simultaneous touches per visitor")]
        [Range(1, 3)]
        [SerializeField] private int maxTouchesPerVisitor = 1;

        [Header("Behavior Profiles")]
        [Tooltip("Mix of visitor behavior types")]
        [Range(0f, 1f)]
        [SerializeField] private float explorerRatio = 0.4f;
        [Range(0f, 1f)]
        [SerializeField] private float lingerRatio = 0.4f;

        [Header("Ground Plane")]
        [Tooltip("Fraction of sculpture height from the bottom to count an endpoint as ground-touching. " +
                 "Increase if sculptures have feet that sit above absolute world Y=0.")]
        [Range(0.01f, 0.4f)]
        [SerializeField] private float groundSnapThreshold = 0.12f;

        [Header("Visualization")]
        [SerializeField] private bool showVisitorGizmos = true;
        [SerializeField] private bool showDebugLog = false;
        [SerializeField] private Color visitorColor = new Color(0f, 1f, 0.5f, 0.6f);
        [SerializeField] private Color touchLineColor = new Color(1f, 1f, 0f, 0.4f);

        [Header("Control")]
        [SerializeField] private bool simulationEnabled = true;

        private struct SculptureInfo
        {
            public LEDGraphManager graph;
            public List<int> endpointIndices;
            public List<Vector3> endpointWorldPositions;
            public Vector3 worldCenter;
            /// <summary>Subset of endpoints whose world Y is within groundSnapThreshold of the scene floor.</summary>
            public List<int> groundEndpointIndices;
            public List<Vector3> groundEndpointPositions;
        }

        private enum VisitorState { Walking, Lingering, Idle }
        private enum VisitorType { Explorer, Lingerer, Hyperactive }

        private class Visitor
        {
            public Vector3 position;
            public Vector3 targetPosition;
            public VisitorState state;
            public VisitorType type;
            public int targetSculptureIndex;
            public float stateTimer;
            public float nextTouchTime;
            public List<ActiveTouch> activeTouches = new List<ActiveTouch>();
        }

        private struct ActiveTouch
        {
            public LEDGraphManager graph;
            public int endpointIndex;
            public Vector3 position;
            public float releaseTime;
        }

        private List<SculptureInfo> sculptures = new List<SculptureInfo>();
        private List<Visitor> visitors = new List<Visitor>();
        private Bounds galleryBounds;
        private float groundY = 0f;
        private bool initialized = false;

        void Start()
        {
            Debug.Log($"🎭 GalleryAudienceSimulator.Start() — enabled={simulationEnabled}, visitors={visitorCount}");
            if (simulationEnabled)
                TryInitialize();
        }

        void Update()
        {
            if (!simulationEnabled) return;
            if (!initialized) { TryInitialize(); return; }

            float dt = Time.deltaTime;
            foreach (var visitor in visitors)
            {
                UpdateVisitorTouches(visitor);
                UpdateVisitorMovement(visitor, dt);
                UpdateVisitorInteraction(visitor, dt);
            }
        }

        void TryInitialize()
        {
            var allGraphs = FindObjectsByType<LEDGraphManager>(FindObjectsSortMode.None);
            if (allGraphs.Length == 0)
            {
                Debug.LogWarning("🎭 GalleryAudienceSimulator: No LEDGraphManager found in scene!");
                return;
            }

            // Wait for at least one graph to load, then include all loaded graphs
            var loadedGraphs = new List<LEDGraphManager>();
            foreach (var gm in allGraphs)
            {
                if (gm.DataLoaded) loadedGraphs.Add(gm);
            }
            if (loadedGraphs.Count == 0) return;

            sculptures.Clear();
            Vector3 overallMin = Vector3.one * float.MaxValue;
            Vector3 overallMax = Vector3.one * float.MinValue;

            foreach (var gm in loadedGraphs)
            {
                var info = new SculptureInfo
                {
                    graph = gm,
                    endpointIndices = new List<int>(),
                    endpointWorldPositions = new List<Vector3>(),
                    groundEndpointIndices = new List<int>(),
                    groundEndpointPositions = new List<Vector3>()
                };

                var nodes = gm.NodePositions;
                var edges = gm.EdgeConnections;
                var valences = new int[nodes.Count];
                foreach (var e in edges)
                {
                    if (e.x < valences.Length) valences[e.x]++;
                    if (e.y < valences.Length) valences[e.y]++;
                }

                Vector3 sum = Vector3.zero;
                for (int i = 0; i < valences.Length; i++)
                {
                    Vector3 wp = gm.transform.TransformPoint(nodes[i]);
                    sum += wp;
                    overallMin = Vector3.Min(overallMin, wp);
                    overallMax = Vector3.Max(overallMax, wp);

                    if (valences[i] == 1)
                    {
                        info.endpointIndices.Add(i);
                        info.endpointWorldPositions.Add(wp);
                    }
                }

                info.worldCenter = sum / nodes.Count;
                sculptures.Add(info);
            }

            // Determine the ground Y from the lowest node across all sculptures
            groundY = overallMin.y;
            float sculHeight = overallMax.y - overallMin.y;
            float groundBand = sculHeight * groundSnapThreshold;

            // Second pass: classify ground-touching endpoints for each sculpture
            for (int si = 0; si < sculptures.Count; si++)
            {
                var info = sculptures[si];
                for (int i = 0; i < info.endpointIndices.Count; i++)
                {
                    if (info.endpointWorldPositions[i].y <= groundY + groundBand)
                    {
                        info.groundEndpointIndices.Add(info.endpointIndices[i]);
                        // Snap the navigation point to the ground plane
                        Vector3 gp = info.endpointWorldPositions[i];
                        gp.y = groundY;
                        info.groundEndpointPositions.Add(gp);
                    }
                }
                sculptures[si] = info;
            }

            galleryBounds = new Bounds((overallMin + overallMax) / 2f, overallMax - overallMin);
            float pad = Mathf.Max(galleryBounds.size.x, galleryBounds.size.z) * 0.3f;
            galleryBounds.Expand(new Vector3(pad, 0, pad));

            // Auto-scale movement and reach to match geometry size
            float gallerySpan = Mathf.Max(galleryBounds.size.x, galleryBounds.size.z);
            float avgSculptureRadius = 0f;
            foreach (var s in sculptures)
            {
                float maxDist = 0f;
                foreach (var ep in s.endpointWorldPositions)
                    maxDist = Mathf.Max(maxDist, Vector3.Distance(s.worldCenter, ep));
                avgSculptureRadius += maxDist;
            }
            avgSculptureRadius /= Mathf.Max(1, sculptures.Count);

            reachDistance = Mathf.Max(reachDistance, avgSculptureRadius * 1.2f);
            walkSpeed = Mathf.Max(walkSpeed, gallerySpan * 0.08f);

            SpawnVisitors();
            initialized = true;

            int totalGround = sculptures.Sum(s => s.groundEndpointPositions.Count);
            Debug.Log($"🎭 Gallery Audience Simulator: {visitors.Count} visitors, " +
                      $"{sculptures.Count} sculptures, {sculptures.Sum(s => s.endpointIndices.Count)} total endpoints " +
                      $"({totalGround} ground-level)\n" +
                      $"   Ground Y: {groundY:F2}, Gallery span: {gallerySpan:F0}, Reach: {reachDistance:F1}, Walk speed: {walkSpeed:F1}");
        }

        void SpawnVisitors()
        {
            visitors.Clear();
            for (int i = 0; i < visitorCount; i++)
            {
                float roll = Random.value;
                VisitorType vtype;
                if (roll < explorerRatio)
                    vtype = VisitorType.Explorer;
                else if (roll < explorerRatio + lingerRatio)
                    vtype = VisitorType.Lingerer;
                else
                    vtype = VisitorType.Hyperactive;

                var visitor = new Visitor
                {
                    position = RandomGalleryPosition(),
                    state = VisitorState.Walking,
                    type = vtype,
                    targetSculptureIndex = Random.Range(0, sculptures.Count),
                    stateTimer = 0f,
                    nextTouchTime = Time.time + Random.Range(0.5f, 2f)
                };

                visitor.targetPosition = GetTargetNearSculpture(visitor.targetSculptureIndex);
                visitors.Add(visitor);
            }
        }

        void UpdateVisitorMovement(Visitor visitor, float dt)
        {
            switch (visitor.state)
            {
                case VisitorState.Walking:
                    Vector3 dir = visitor.targetPosition - visitor.position;
                    dir.y = 0;
                    float dist = dir.magnitude;

                    if (dist < 1f)
                    {
                        visitor.state = VisitorState.Lingering;
                        float baseLinger = Random.Range(lingerDuration.x, lingerDuration.y);
                        visitor.stateTimer = visitor.type == VisitorType.Lingerer
                            ? baseLinger * 1.5f
                            : visitor.type == VisitorType.Explorer
                                ? baseLinger * 0.6f
                                : baseLinger * 0.3f;
                    }
                    else
                    {
                        float speed = walkSpeed * (visitor.type == VisitorType.Hyperactive ? 1.4f : 1f);
                        visitor.position += dir.normalized * speed * dt;
                        visitor.position.y = groundY;
                    }
                    break;

                case VisitorState.Lingering:
                    visitor.stateTimer -= dt;
                    // Slight drift while lingering (ground plane only)
                    visitor.position += new Vector3(
                        Mathf.Sin(Time.time * 0.5f + visitor.GetHashCode()) * 0.2f * dt,
                        0,
                        Mathf.Cos(Time.time * 0.7f + visitor.GetHashCode()) * 0.2f * dt
                    );
                    visitor.position.y = groundY;

                    if (visitor.stateTimer <= 0f)
                    {
                        ReleaseAllTouches(visitor);
                        PickNewTarget(visitor);
                        visitor.state = VisitorState.Walking;
                    }
                    break;
            }
        }

        void UpdateVisitorInteraction(Visitor visitor, float dt)
        {
            if (visitor.state != VisitorState.Lingering) return;
            if (Time.time < visitor.nextTouchTime) return;
            if (visitor.activeTouches.Count >= maxTouchesPerVisitor) return;

            if (Random.value > touchChancePerSecond * dt * 10f) return;

            int si = visitor.targetSculptureIndex;
            if (si < 0 || si >= sculptures.Count) return;
            var sculpture = sculptures[si];
            if (sculpture.endpointIndices.Count == 0) return;

            // Find nearby reachable endpoints not already touched
            var touchedSet = new HashSet<int>(visitor.activeTouches
                .Where(t => t.graph == sculpture.graph)
                .Select(t => t.endpointIndex));

            var reachable = new List<int>();
            for (int i = 0; i < sculpture.endpointWorldPositions.Count; i++)
            {
                float d = Vector3.Distance(visitor.position, sculpture.endpointWorldPositions[i]);
                if (d < reachDistance && !touchedSet.Contains(sculpture.endpointIndices[i]))
                    reachable.Add(i);
            }

            if (reachable.Count == 0) return;

            int pick = reachable[Random.Range(0, reachable.Count)];
            int epIdx = sculpture.endpointIndices[pick];
            Vector3 epPos = sculpture.endpointWorldPositions[pick];

            GraphPlayerController.SimulatePress(sculpture.graph, epIdx, epPos);

            float hold = Random.Range(holdDuration.x, holdDuration.y);
            if (visitor.type == VisitorType.Lingerer) hold *= 1.5f;
            if (visitor.type == VisitorType.Hyperactive) hold *= 0.4f;

            visitor.activeTouches.Add(new ActiveTouch
            {
                graph = sculpture.graph,
                endpointIndex = epIdx,
                position = epPos,
                releaseTime = Time.time + hold
            });

            visitor.nextTouchTime = Time.time + Random.Range(pauseBetweenTouches.x, pauseBetweenTouches.y);

            if (showDebugLog)
                Debug.Log($"👆 Visitor touched endpoint {epIdx} on {sculpture.graph.gameObject.name}");
        }

        void UpdateVisitorTouches(Visitor visitor)
        {
            for (int i = visitor.activeTouches.Count - 1; i >= 0; i--)
            {
                var touch = visitor.activeTouches[i];
                if (Time.time >= touch.releaseTime)
                {
                    if (touch.graph != null)
                        GraphPlayerController.SimulateRelease(touch.graph, touch.endpointIndex, touch.position);

                    visitor.activeTouches.RemoveAt(i);

                    if (showDebugLog)
                        Debug.Log($"🚶 Visitor released endpoint {touch.endpointIndex}");
                }
                else
                {
                    // Fire held event every frame so animations like GraphJuggle detect continuous presence
                    GraphPlayerController.SimulateHold(touch.graph, touch.endpointIndex, touch.position);
                }
            }
        }

        void PickNewTarget(Visitor visitor)
        {
            if (sculptures.Count <= 1)
            {
                visitor.targetSculptureIndex = 0;
            }
            else if (visitor.type == VisitorType.Explorer)
            {
                // Explorers always go to a different sculpture
                int next;
                do { next = Random.Range(0, sculptures.Count); }
                while (next == visitor.targetSculptureIndex);
                visitor.targetSculptureIndex = next;
            }
            else
            {
                // Others might revisit or move on
                if (Random.value < 0.6f)
                {
                    int next;
                    do { next = Random.Range(0, sculptures.Count); }
                    while (next == visitor.targetSculptureIndex && sculptures.Count > 1);
                    visitor.targetSculptureIndex = next;
                }
            }

            visitor.targetPosition = GetTargetNearSculpture(visitor.targetSculptureIndex);
        }

        void ReleaseAllTouches(Visitor visitor)
        {
            foreach (var touch in visitor.activeTouches)
            {
                if (touch.graph != null)
                    GraphPlayerController.SimulateRelease(touch.graph, touch.endpointIndex, touch.position);
            }
            visitor.activeTouches.Clear();
        }

        Vector3 GetTargetNearSculpture(int sculptureIndex)
        {
            if (sculptureIndex < 0 || sculptureIndex >= sculptures.Count)
                return RandomGalleryPosition();

            var sculpture = sculptures[sculptureIndex];

            // Navigate to a ground-touching endpoint; fall back to ground-projected center
            if (sculpture.groundEndpointPositions.Count > 0)
            {
                Vector3 ep = sculpture.groundEndpointPositions[Random.Range(0, sculpture.groundEndpointPositions.Count)];
                float spread = reachDistance * 0.4f;
                return new Vector3(
                    ep.x + Random.Range(-spread, spread),
                    groundY,
                    ep.z + Random.Range(-spread, spread)
                );
            }

            // Fallback: near the sculpture center projected onto the ground plane
            float fallbackSpread = reachDistance * 0.8f;
            return new Vector3(
                sculpture.worldCenter.x + Random.Range(-fallbackSpread, fallbackSpread),
                groundY,
                sculpture.worldCenter.z + Random.Range(-fallbackSpread, fallbackSpread)
            );
        }

        Vector3 RandomGalleryPosition()
        {
            return new Vector3(
                Random.Range(galleryBounds.min.x, galleryBounds.max.x),
                groundY,
                Random.Range(galleryBounds.min.z, galleryBounds.max.z)
            );
        }

        void OnDisable()
        {
            if (visitors == null) return;
            foreach (var visitor in visitors)
                ReleaseAllTouches(visitor);
        }

        // --- Inspector Controls ---

        [ContextMenu("Reset Simulation")]
        void ResetSimulation()
        {
            if (visitors != null)
                foreach (var v in visitors) ReleaseAllTouches(v);
            initialized = false;
            visitors.Clear();
            sculptures.Clear();
        }

        [ContextMenu("Add 3 Visitors")]
        void AddVisitors()
        {
            visitorCount += 3;
            ResetSimulation();
        }

        [ContextMenu("Remove 3 Visitors")]
        void RemoveVisitors()
        {
            visitorCount = Mathf.Max(1, visitorCount - 3);
            ResetSimulation();
        }

        // --- Gizmos ---

        void OnDrawGizmos()
        {
            if (!showVisitorGizmos || visitors == null) return;

            foreach (var visitor in visitors)
            {
                // Visitor position
                Color c = visitorColor;
                if (visitor.state == VisitorState.Lingering) c = Color.Lerp(c, Color.yellow, 0.3f);
                if (visitor.activeTouches.Count > 0) c = Color.Lerp(c, Color.white, 0.4f);
                Gizmos.color = c;
                Gizmos.DrawSphere(visitor.position, 1.5f);

                // Walking direction
                if (visitor.state == VisitorState.Walking)
                {
                    Gizmos.color = new Color(c.r, c.g, c.b, 0.3f);
                    Gizmos.DrawLine(visitor.position, visitor.targetPosition);
                }

                // Active touch lines
                Gizmos.color = touchLineColor;
                foreach (var touch in visitor.activeTouches)
                {
                    Gizmos.DrawLine(visitor.position, touch.position);
                }
            }

            // Gallery bounds (ground footprint)
            Gizmos.color = new Color(1f, 1f, 1f, 0.1f);
            Gizmos.DrawWireCube(galleryBounds.center, galleryBounds.size);

            // Ground-touching endpoints (navigation targets)
            Gizmos.color = new Color(0f, 1f, 1f, 0.5f);
            foreach (var s in sculptures)
            {
                foreach (var gp in s.groundEndpointPositions)
                    Gizmos.DrawWireSphere(gp, 2f);
            }
        }
    }
}
