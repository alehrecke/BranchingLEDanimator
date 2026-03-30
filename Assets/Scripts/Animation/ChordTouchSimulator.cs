using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using BranchingLEDAnimator.Player;
using BranchingLEDAnimator.Core;

namespace BranchingLEDAnimator.Animation
{
    /// <summary>
    /// Simulates multiple people touching endpoints simultaneously.
    /// Creates realistic chord progressions and timing patterns to preview
    /// how the installation would feel with a room full of participants.
    /// </summary>
    public class ChordTouchSimulator : MonoBehaviour
    {
        [Header("Simulation Control")]
        [Tooltip("Enable/disable the simulation")]
        public bool simulationEnabled = true;
        
        [Tooltip("Pause simulation (keeps state but stops new events)")]
        public bool paused = false;
        
        [Header("Crowd Size")]
        [Tooltip("Minimum simultaneous touches")]
        [Range(1, 20)]
        public int minSimultaneousTouches = 2;
        
        [Tooltip("Maximum simultaneous touches")]
        [Range(1, 20)]
        public int maxSimultaneousTouches = 6;
        
        [Header("Timing")]
        [Tooltip("How long a simulated person holds a touch (min seconds)")]
        public float minHoldDuration = 2f;
        
        [Tooltip("How long a simulated person holds a touch (max seconds)")]
        public float maxHoldDuration = 8f;
        
        [Tooltip("Time between new people arriving (min seconds)")]
        public float minArrivalInterval = 0.5f;
        
        [Tooltip("Time between new people arriving (max seconds)")]
        public float maxArrivalInterval = 3f;
        
        [Header("Musical Intelligence")]
        [Tooltip("Prefer notes that form chords together")]
        public bool preferChordTones = true;
        
        [Tooltip("How strongly to prefer chord tones (0=random, 1=always chords)")]
        [Range(0f, 1f)]
        public float chordPreference = 0.7f;
        
        [Tooltip("Use common chord progressions")]
        public bool useChordProgressions = true;
        
        [Tooltip("Seconds between chord changes in progression")]
        public float chordChangeDuration = 4f;
        
        [Header("Behavior Patterns")]
        [Tooltip("Simulate coordinated group touches")]
        public bool allowGroupTouches = true;
        
        [Tooltip("Chance of 2-4 people touching at nearly the same time")]
        [Range(0f, 1f)]
        public float groupTouchChance = 0.3f;
        
        [Tooltip("Spread time for group touches (seconds)")]
        public float groupTouchSpread = 0.5f;
        
        [Header("Debug")]
        public bool showDebugInfo = true;
        
        // Internal state
        private class SimulatedTouch
        {
            public int endpointIndex;
            public float startTime;
            public float duration;
            public Vector3 position;
        }
        
        private List<SimulatedTouch> activeTouches = new List<SimulatedTouch>();
        private float lastArrivalTime = 0f;
        private float nextArrivalDelay = 0f;
        private float currentChordStartTime = 0f;
        private int currentChordIndex = 0;
        
        // Chord progression patterns (scale degrees, 0-indexed)
        // These work with the ChordTouchAnimation's scale system
        private static readonly int[][] ChordProgressions = new int[][]
        {
            // I - IV - V - I (Classic)
            new int[] { 0, 3, 4, 0 },
            // I - V - vi - IV (Pop progression)
            new int[] { 0, 4, 5, 3 },
            // I - vi - IV - V (50s progression)  
            new int[] { 0, 5, 3, 4 },
            // vi - IV - I - V (Axis progression)
            new int[] { 5, 3, 0, 4 },
            // I - IV - vi - V
            new int[] { 0, 3, 5, 4 },
            // I - iii - IV - V
            new int[] { 0, 2, 3, 4 },
        };
        
        // Chord tones for each scale degree (which other degrees sound good with it)
        private static readonly Dictionary<int, int[]> ChordTones = new Dictionary<int, int[]>
        {
            { 0, new int[] { 0, 2, 4 } },      // I chord: 1, 3, 5
            { 1, new int[] { 1, 3, 5 } },      // ii chord: 2, 4, 6
            { 2, new int[] { 2, 4, 6 } },      // iii chord: 3, 5, 7
            { 3, new int[] { 3, 5, 0 } },      // IV chord: 4, 6, 1
            { 4, new int[] { 4, 6, 1 } },      // V chord: 5, 7, 2
            { 5, new int[] { 5, 0, 2 } },      // vi chord: 6, 1, 3
            { 6, new int[] { 6, 1, 3 } },      // vii° chord: 7, 2, 4
        };
        
        private LEDGraphManager graphManager;
        
        // Cached endpoint info
        private List<int> availableEndpoints = new List<int>();
        private Dictionary<int, Vector3> endpointPositions = new Dictionary<int, Vector3>();
        private Dictionary<int, int> endpointToScaleDegree = new Dictionary<int, int>();
        private bool initialized = false;
        
        // Current chord progression
        private int[] currentProgression;
        
        void Start()
        {
            SelectRandomProgression();
        }
        
        void OnEnable()
        {
            initialized = false;
            activeTouches.Clear();
        }
        
        void OnDisable()
        {
            // Release all simulated touches
            foreach (var touch in activeTouches)
            {
                if (graphManager != null)
                    GraphPlayerController.SimulateRelease(graphManager, touch.endpointIndex, touch.position);
            }
            activeTouches.Clear();
        }
        
        void Update()
        {
            if (!simulationEnabled) return;
            
            // Try to initialize if not done
            if (!initialized)
            {
                TryInitialize();
                if (!initialized) return;
            }
            
            float currentTime = Time.time;
            
            // Update chord progression
            if (useChordProgressions && currentTime - currentChordStartTime > chordChangeDuration)
            {
                AdvanceChordProgression();
                currentChordStartTime = currentTime;
            }
            
            if (!paused)
            {
                // Check for expired touches
                UpdateActiveTouches(currentTime);
                
                // Maybe add new touches
                ConsiderNewArrivals(currentTime);
            }
        }
        
        void TryInitialize()
        {
            // Find the graph manager to get endpoint info
            graphManager = FindFirstObjectByType<LEDGraphManager>();
            if (graphManager == null || graphManager.NodePositions == null) return;
            
            var nodePositions = graphManager.NodePositions;
            var edges = graphManager.EdgeConnections;
            if (nodePositions.Count == 0 || edges.Count == 0) return;
            
            // Build adjacency to find endpoints
            var adjacency = new Dictionary<int, List<int>>();
            for (int i = 0; i < nodePositions.Count; i++)
            {
                adjacency[i] = new List<int>();
            }
            
            foreach (var edge in edges)
            {
                if (edge.x < nodePositions.Count && edge.y < nodePositions.Count)
                {
                    if (!adjacency[edge.x].Contains(edge.y))
                        adjacency[edge.x].Add(edge.y);
                    if (!adjacency[edge.y].Contains(edge.x))
                        adjacency[edge.y].Add(edge.x);
                }
            }
            
            // Find endpoints (valence 1)
            availableEndpoints.Clear();
            endpointPositions.Clear();
            
            foreach (var kvp in adjacency)
            {
                if (kvp.Value.Count == 1)
                {
                    availableEndpoints.Add(kvp.Key);
                    endpointPositions[kvp.Key] = nodePositions[kvp.Key];
                }
            }
            
            if (availableEndpoints.Count == 0)
            {
                Debug.LogWarning("ChordTouchSimulator: No endpoints found in graph!");
                return;
            }
            
            // Sort endpoints consistently (same order as ChordTouchAnimation)
            availableEndpoints = availableEndpoints
                .OrderBy(e => nodePositions[e].x + nodePositions[e].z * 0.1f)
                .ToList();
            
            // Map endpoints to scale degrees
            endpointToScaleDegree.Clear();
            for (int i = 0; i < availableEndpoints.Count; i++)
            {
                endpointToScaleDegree[availableEndpoints[i]] = i % 7; // Assume 7-note scale
            }
            
            initialized = true;
            lastArrivalTime = Time.time;
            nextArrivalDelay = Random.Range(minArrivalInterval, maxArrivalInterval);
            
            if (showDebugInfo)
            {
                Debug.Log($"🎭 ChordTouchSimulator initialized with {availableEndpoints.Count} endpoints");
            }
        }
        
        void SelectRandomProgression()
        {
            currentProgression = ChordProgressions[Random.Range(0, ChordProgressions.Length)];
            currentChordIndex = 0;
            currentChordStartTime = Time.time;
            
            if (showDebugInfo)
            {
                string progStr = string.Join(" -> ", currentProgression.Select(d => GetChordName(d)));
                Debug.Log($"🎵 New chord progression: {progStr}");
            }
        }
        
        void AdvanceChordProgression()
        {
            currentChordIndex = (currentChordIndex + 1) % currentProgression.Length;
            
            // Occasionally switch to a new progression
            if (currentChordIndex == 0 && Random.value < 0.3f)
            {
                SelectRandomProgression();
            }
            else if (showDebugInfo)
            {
                Debug.Log($"🎵 Chord change: {GetChordName(currentProgression[currentChordIndex])}");
            }
        }
        
        void UpdateActiveTouches(float currentTime)
        {
            for (int i = activeTouches.Count - 1; i >= 0; i--)
            {
                var touch = activeTouches[i];
                
                if (currentTime - touch.startTime >= touch.duration)
                {
                    // Release this touch
                    if (graphManager != null)
                        GraphPlayerController.SimulateRelease(graphManager, touch.endpointIndex, touch.position);
                    
                    if (showDebugInfo)
                    {
                        Debug.Log($"🚶 Simulated release: endpoint {touch.endpointIndex}");
                    }
                    
                    activeTouches.RemoveAt(i);
                }
            }
        }
        
        void ConsiderNewArrivals(float currentTime)
        {
            // Check if it's time for new arrivals
            if (currentTime - lastArrivalTime < nextArrivalDelay) return;
            
            // Check if we're at capacity
            if (activeTouches.Count >= maxSimultaneousTouches) return;
            
            // Determine how many people arrive
            int arrivalsCount = 1;
            if (allowGroupTouches && Random.value < groupTouchChance)
            {
                arrivalsCount = Random.Range(2, 5);
            }
            
            // Cap to not exceed max
            arrivalsCount = Mathf.Min(arrivalsCount, maxSimultaneousTouches - activeTouches.Count);
            
            // Also respect min (try to maintain at least min touches)
            if (activeTouches.Count < minSimultaneousTouches)
            {
                arrivalsCount = Mathf.Max(arrivalsCount, minSimultaneousTouches - activeTouches.Count);
            }
            
            // Get available endpoints (not currently touched)
            var touchedEndpoints = new HashSet<int>(activeTouches.Select(t => t.endpointIndex));
            var freeEndpoints = availableEndpoints.Where(e => !touchedEndpoints.Contains(e)).ToList();
            
            if (freeEndpoints.Count == 0) return;
            
            // Select endpoints to touch
            var selectedEndpoints = SelectEndpointsToTouch(freeEndpoints, arrivalsCount);
            
            // Create touches with slight time offsets for groups
            for (int i = 0; i < selectedEndpoints.Count; i++)
            {
                int endpoint = selectedEndpoints[i];
                float delay = allowGroupTouches && arrivalsCount > 1 
                    ? Random.Range(0f, groupTouchSpread) * i 
                    : 0f;
                
                StartCoroutine(CreateTouchAfterDelay(endpoint, delay, currentTime));
            }
            
            lastArrivalTime = currentTime;
            nextArrivalDelay = Random.Range(minArrivalInterval, maxArrivalInterval);
        }
        
        System.Collections.IEnumerator CreateTouchAfterDelay(int endpoint, float delay, float baseTime)
        {
            if (delay > 0)
            {
                yield return new WaitForSeconds(delay);
            }
            
            // Double-check endpoint is still free
            if (activeTouches.Any(t => t.endpointIndex == endpoint))
            {
                yield break;
            }
            
            var touch = new SimulatedTouch
            {
                endpointIndex = endpoint,
                startTime = Time.time,
                duration = Random.Range(minHoldDuration, maxHoldDuration),
                position = endpointPositions[endpoint]
            };
            
            activeTouches.Add(touch);
            if (graphManager != null)
                GraphPlayerController.SimulatePress(graphManager, endpoint, touch.position);
            
            if (showDebugInfo)
            {
                int scaleDegree = endpointToScaleDegree.ContainsKey(endpoint) ? endpointToScaleDegree[endpoint] : 0;
                Debug.Log($"👆 Simulated press: endpoint {endpoint} (scale degree {scaleDegree + 1}), duration {touch.duration:F1}s");
            }
        }
        
        List<int> SelectEndpointsToTouch(List<int> freeEndpoints, int count)
        {
            var selected = new List<int>();
            count = Mathf.Min(count, freeEndpoints.Count);
            
            if (!preferChordTones || Random.value > chordPreference)
            {
                // Random selection
                var shuffled = freeEndpoints.OrderBy(x => Random.value).ToList();
                return shuffled.Take(count).ToList();
            }
            
            // Prefer chord tones based on current chord
            int currentChordRoot = useChordProgressions 
                ? currentProgression[currentChordIndex] 
                : 0;
            
            int[] preferredDegrees = ChordTones.ContainsKey(currentChordRoot) 
                ? ChordTones[currentChordRoot] 
                : new int[] { 0, 2, 4 };
            
            // Score each endpoint by how well it fits the current chord
            var scored = freeEndpoints
                .Select(e => {
                    int degree = endpointToScaleDegree.ContainsKey(e) ? endpointToScaleDegree[e] : 0;
                    float score = preferredDegrees.Contains(degree) ? 1f : 0.3f;
                    score += Random.Range(0f, 0.5f); // Add some randomness
                    return new { endpoint = e, score = score };
                })
                .OrderByDescending(x => x.score)
                .ToList();
            
            return scored.Take(count).Select(x => x.endpoint).ToList();
        }
        
        string GetChordName(int scaleDegree)
        {
            string[] names = { "I", "ii", "iii", "IV", "V", "vi", "vii°" };
            return scaleDegree < names.Length ? names[scaleDegree] : scaleDegree.ToString();
        }
        
        // Context menu actions for testing
        [ContextMenu("Force Single Touch")]
        void ForceSingleTouch()
        {
            if (!initialized) TryInitialize();
            if (!initialized || availableEndpoints.Count == 0) return;
            
            var touchedEndpoints = new HashSet<int>(activeTouches.Select(t => t.endpointIndex));
            var freeEndpoints = availableEndpoints.Where(e => !touchedEndpoints.Contains(e)).ToList();
            
            if (freeEndpoints.Count > 0)
            {
                int endpoint = freeEndpoints[Random.Range(0, freeEndpoints.Count)];
                StartCoroutine(CreateTouchAfterDelay(endpoint, 0f, Time.time));
            }
        }
        
        [ContextMenu("Force Chord (3 touches)")]
        void ForceChord()
        {
            if (!initialized) TryInitialize();
            if (!initialized || availableEndpoints.Count == 0) return;
            
            var touchedEndpoints = new HashSet<int>(activeTouches.Select(t => t.endpointIndex));
            var freeEndpoints = availableEndpoints.Where(e => !touchedEndpoints.Contains(e)).ToList();
            
            var selected = SelectEndpointsToTouch(freeEndpoints, 3);
            for (int i = 0; i < selected.Count; i++)
            {
                StartCoroutine(CreateTouchAfterDelay(selected[i], i * 0.1f, Time.time));
            }
        }
        
        [ContextMenu("Release All")]
        void ReleaseAll()
        {
            foreach (var touch in activeTouches)
            {
                if (graphManager != null)
                    GraphPlayerController.SimulateRelease(graphManager, touch.endpointIndex, touch.position);
            }
            activeTouches.Clear();
            Debug.Log("🚶 Released all simulated touches");
        }
        
        [ContextMenu("New Progression")]
        void ForceNewProgression()
        {
            SelectRandomProgression();
        }
        
        void OnGUI()
        {
            if (!showDebugInfo || !simulationEnabled) return;
            
            GUILayout.BeginArea(new Rect(10, 10, 300, 200));
            GUILayout.BeginVertical("box");
            
            GUILayout.Label($"🎭 Chord Touch Simulator");
            GUILayout.Label($"Active touches: {activeTouches.Count}/{maxSimultaneousTouches}");
            
            if (useChordProgressions && currentProgression != null)
            {
                string progStr = "";
                for (int i = 0; i < currentProgression.Length; i++)
                {
                    string chord = GetChordName(currentProgression[i]);
                    if (i == currentChordIndex)
                        progStr += $"[{chord}] ";
                    else
                        progStr += $"{chord} ";
                }
                GUILayout.Label($"Progression: {progStr}");
            }
            
            if (activeTouches.Count > 0)
            {
                string touches = string.Join(", ", activeTouches.Select(t => t.endpointIndex));
                GUILayout.Label($"Touched: {touches}");
            }
            
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
    }
}
