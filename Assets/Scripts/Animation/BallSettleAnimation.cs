using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using BranchingLEDAnimator.Player;
using BranchingLEDAnimator.Core;

namespace BranchingLEDAnimator.Animation
{
    /// <summary>
    /// Relaxed ball animation where balls travel to endpoints and settle.
    /// When a settled endpoint is touched, balls are dispersed to random destinations.
    /// Multiple balls can stack on the same endpoint until disturbed.
    /// </summary>
    [CreateAssetMenu(fileName = "BallSettleAnimation", menuName = "LED Animations/Interactive/Ball Settle")]
    public class BallSettleAnimation : LEDAnimationType
    {
        [Header("Ball Settings")]
        [Tooltip("Speed of traveling balls")]
        [Range(1f, 20f)]
        [SerializeField] private float ballSpeed = 6f;
        
        [Tooltip("Total number of balls in the system")]
        [Range(1, 20)]
        [SerializeField] private int totalBalls = 3;
        
        [Tooltip("Spawn balls at start or wait for interaction")]
        [SerializeField] private bool autoSpawnAtStart = true;
        
        [Header("Visual Settings")]
        [Tooltip("Use rainbow colors for balls")]
        [SerializeField] private bool useRainbowColors = true;
        
        [Tooltip("Saturation of rainbow colors")]
        [Range(0.5f, 1f)]
        [SerializeField] private float rainbowSaturation = 0.9f;
        
        [Tooltip("Brightness of balls")]
        [Range(0.5f, 1f)]
        [SerializeField] private float rainbowBrightness = 1f;
        
        public enum PulseStyle { SymmetricGlow, VelocityTrail }
        
        [Header("Pulse Style")]
        [Tooltip("How traveling balls are rendered")]
        [SerializeField] private PulseStyle pulseStyle = PulseStyle.VelocityTrail;
        
        [Tooltip("Width of the ball glow")]
        [Range(1f, 15f)]
        [SerializeField] private float pulseWidth = 4f;
        
        [Tooltip("Length of velocity trail")]
        [Range(0f, 30f)]
        [SerializeField] private float trailLength = 10f;
        
        [Tooltip("Trail intensity")]
        [Range(0f, 1f)]
        [SerializeField] private float trailIntensity = 0.5f;
        
        [SerializeField] private AnimationCurve pulseShape = AnimationCurve.EaseInOut(0, 0, 1, 1);
        
        [Header("Settled Ball Appearance")]
        [Tooltip("Brightness of settled balls")]
        [Range(0.5f, 2f)]
        [SerializeField] private float settledIntensity = 1.0f;
        
        [Tooltip("Settled balls pulse gently")]
        [SerializeField] private bool settledPulse = true;
        
        [Tooltip("Pulse speed for settled balls")]
        [Range(0.5f, 3f)]
        [SerializeField] private float settledPulseSpeed = 1f;
        
        [Header("Dispatch Behavior")]
        [Tooltip("Delay between dispatching stacked balls (0 = all at once, higher = more spread out)")]
        [Range(0f, 1f)]
        [SerializeField] private float dispatchStagger = 0.15f;
        
        [Header("Audio")]
        [SerializeField] private bool enableAudio = true;
        [Range(0f, 1f)]
        [SerializeField] private float toneVolume = 0.3f;
        
        public enum InputMode { PlayerController, MouseClick, None }
        
        [Header("Input")]
        [SerializeField] private InputMode inputMode = InputMode.PlayerController;
        
        // Ball classes
        private class TravelingBall
        {
            public List<int> path;
            public float progress;
            public float lastProgress;
            public int targetEndpoint;
            public float spawnTime;
            public float startDelay; // Delay before ball starts moving
            public float hue;
            public int ballIndex;
            public int targetStackPosition; // Where this ball will land in the stack
            
            public TravelingBall(List<int> nodePath, float time, float assignedHue, int index, float delay = 0f)
            {
                path = nodePath;
                progress = 0f;
                lastProgress = 0f;
                targetEndpoint = nodePath[nodePath.Count - 1];
                spawnTime = time;
                startDelay = delay;
                hue = assignedHue;
                ballIndex = index;
                targetStackPosition = 0;
            }
        }
        
        private class SettledBall
        {
            public int endpointIndex;
            public float hue;
            public int ballIndex;
            public float settleTime;
            public int stackPosition; // 0 = bottom, 1, 2... = stacked on top
            
            public SettledBall(int endpoint, float assignedHue, int index, float time)
            {
                endpointIndex = endpoint;
                hue = assignedHue;
                ballIndex = index;
                settleTime = time;
                stackPosition = 0;
            }
        }
        
        // State
        private List<int> endpointNodes = new List<int>();
        private Dictionary<int, List<int>> adjacency = new Dictionary<int, List<int>>();
        private Dictionary<int, float> endpointFrequencies = new Dictionary<int, float>();
        private List<Vector3> cachedPositions;
        private float minHeight, maxHeight;
        private int topNode = -1; // Highest point in the graph (spawn point)
        private bool analysisComplete = false;
        
        private List<TravelingBall> travelingBalls = new List<TravelingBall>();
        private List<SettledBall> settledBalls = new List<SettledBall>();
        private float lastRealTime = 0f;
        private int nextBallIndex = 0;
        private int lastTotalBalls = 0; // Track changes to totalBalls
        private bool initialized = false;
        
        // Audio
        private GameObject audioContainer;
        private AudioSource toneSource;
        private Dictionary<int, AudioClip> toneClips = new Dictionary<int, AudioClip>();
        private int sampleRate = 44100;
        private bool audioInitialized = false;
        
        // Input
        private Queue<int> pendingTouches = new Queue<int>();
        
        // Cached graph data
        private List<Vector2Int> cachedEdges;
        
        // LED mode support - disabled for now as node mode works reliably
        // TODO: Fix LED mode mapping issues before re-enabling
        public override bool SupportsLEDMode => false;
        private MappedGraph cachedLEDGraph;
        private Dictionary<int, int> nodeToLEDEndpoint;
        private Dictionary<int, List<int>> stripLEDPaths;
        private bool ledMappingInitialized;
        
        void OnEnable()
        {
            ledMappingInitialized = false;
            initialized = false;
            analysisComplete = false;
            settledBalls.Clear();
            travelingBalls.Clear();
            nextBallIndex = 0;
            lastTotalBalls = 0;
            topNode = -1;
        }
        
        public override Color[] CalculateLEDColors(MappedGraph mappedGraph, float time, int frame)
        {
            if (mappedGraph == null || !mappedGraph.IsValid) return null;
            
            Color[] colors = new Color[mappedGraph.TotalLEDCount];
            
            // Initialize background with inactive color
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = inactiveColor;
            }
            
            // Initialize from LED positions if we haven't been initialized via node mode
            if (!initialized && cachedPositions == null)
            {
                // Build minimal graph from LED positions
                cachedPositions = mappedGraph.LEDPositions;
                var edges = new List<Vector2Int>();
                foreach (var edge in mappedGraph.LEDEdges)
                {
                    edges.Add(edge);
                }
                Initialize(Time.realtimeSinceStartup, cachedPositions, edges);
            }
            
            if (!ledMappingInitialized)
            {
                InitializeLEDMapping(mappedGraph);
            }
            
            float currentTime = Time.realtimeSinceStartup;
            float deltaTime = Mathf.Clamp(currentTime - lastRealTime, 0.001f, 0.1f);
            lastRealTime = currentTime;
            
            // Check for ball count changes
            CheckBallCountChange(currentTime);
            
            // Update traveling balls
            UpdateTravelingBalls(deltaTime, currentTime);
            
            // Process any pending input
            ProcessInput();
            
            // Render settled balls
            RenderSettledBallsLED(colors, mappedGraph, currentTime);
            
            // Render traveling balls
            RenderTravelingBallsLED(colors, mappedGraph);
            
            return colors;
        }
        
        private void InitializeLEDMapping(MappedGraph mappedGraph)
        {
            cachedLEDGraph = mappedGraph;
            nodeToLEDEndpoint = new Dictionary<int, int>();
            stripLEDPaths = new Dictionary<int, List<int>>();
            
            // Map each NODE endpoint to its closest LED endpoint
            foreach (int nodeEndpoint in endpointNodes)
            {
                if (nodeEndpoint >= cachedPositions.Count) continue;
                
                Vector3 nodePos = cachedPositions[nodeEndpoint];
                int closestLED = -1;
                float closestDist = float.MaxValue;
                
                foreach (int ledEndpoint in mappedGraph.LEDEndpoints)
                {
                    if (ledEndpoint < 0 || ledEndpoint >= mappedGraph.LEDPositions.Count)
                        continue;
                        
                    float dist = Vector3.Distance(nodePos, mappedGraph.LEDPositions[ledEndpoint]);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        closestLED = ledEndpoint;
                    }
                }
                
                if (closestLED >= 0)
                {
                    nodeToLEDEndpoint[nodeEndpoint] = closestLED;
                }
            }
            
            // Build LED paths for each strip
            for (int s = 0; s < mappedGraph.StripCount; s++)
            {
                stripLEDPaths[s] = mappedGraph.GetLEDsForStrip(s);
            }
            
            ledMappingInitialized = true;
        }
        
        protected override Color[] CalculateBaseNodeColors(
            List<Vector3> nodePositions,
            List<Vector2Int> edgeConnections,
            List<int> sourceNodes,
            float time,
            int frame)
        {
            if (nodePositions == null || nodePositions.Count == 0)
                return new Color[0];
            
            // Cache graph data
            cachedPositions = nodePositions;
            cachedEdges = edgeConnections;
            
            Color[] colors = new Color[nodePositions.Count];
            
            // Initialize background with inactive color
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = inactiveColor;
            }
            
            float currentTime = Time.realtimeSinceStartup;
            float deltaTime = Mathf.Clamp(currentTime - lastRealTime, 0.001f, 0.1f);
            lastRealTime = currentTime;
            
            if (!initialized)
            {
                Initialize(currentTime, nodePositions, edgeConnections);
            }
            
            // Check for ball count changes
            CheckBallCountChange(currentTime);
            
            UpdateTravelingBalls(deltaTime, currentTime);
            ProcessInput();
            
            // Render
            RenderSettledBalls(colors, nodePositions, currentTime);
            RenderTravelingBalls(colors, nodePositions);
            
            
            return colors;
        }
        
        private void Initialize(float time, List<Vector3> nodePositions, List<Vector2Int> edgeConnections)
        {
            if (nodePositions == null || nodePositions.Count == 0) return;
            
            AnalyzeGraph(nodePositions, edgeConnections);
            
            
            if (!audioInitialized && enableAudio)
            {
                SetupAudio();
            }
            
            SubscribeToInput();
            
            // Spawn initial balls
            if (autoSpawnAtStart && endpointNodes.Count > 0)
            {
                SpawnInitialBalls(time);
            }
            
            initialized = true;
        }
        
        private void SpawnInitialBalls(float time)
        {
            SpawnBallsFromTop(totalBalls, time);
            lastTotalBalls = totalBalls;
        }
        
        /// <summary>
        /// Spawn balls from the top node, traveling to random endpoints
        /// </summary>
        private void SpawnBallsFromTop(int count, float time)
        {
            if (topNode < 0 || endpointNodes.Count == 0) return;
            
            // Shuffle endpoints for random distribution
            var shuffledEndpoints = endpointNodes.OrderBy(x => Random.value).ToList();
            
            // Calculate starting hue offset to distribute new balls' colors evenly
            int existingBallCount = settledBalls.Count + travelingBalls.Count;
            
            for (int i = 0; i < count; i++)
            {
                // Pick a random endpoint target
                int targetEndpoint = shuffledEndpoints[i % shuffledEndpoints.Count];
                
                // Use golden ratio for even hue distribution regardless of spawn order
                // This ensures colors are spread across the spectrum even when adding balls incrementally
                float hue = ((existingBallCount + i) * 0.618033988749895f) % 1f;
                
                // Find path from top node to target endpoint
                var path = FindPath(topNode, targetEndpoint);
                if (path == null || path.Count < 2)
                {
                    // Fallback: just settle at the endpoint
                    var ball = new SettledBall(targetEndpoint, hue, nextBallIndex, time);
                    settledBalls.Add(ball);
                    nextBallIndex++;
                    continue;
                }
                
                // Create traveling ball with staggered start delay
                float delay = i * 0.15f; // Stagger spawns
                var traveling = new TravelingBall(path, time, hue, nextBallIndex, delay);
                
                // Calculate target stack position
                int existingAtTarget = settledBalls.Count(b => b.endpointIndex == targetEndpoint);
                int incomingToTarget = travelingBalls.Count(b => b.targetEndpoint == targetEndpoint);
                traveling.targetStackPosition = existingAtTarget + incomingToTarget;
                
                travelingBalls.Add(traveling);
                nextBallIndex++;
            }
            
        }
        
        /// <summary>
        /// Check if totalBalls has changed and spawn/remove balls accordingly
        /// </summary>
        private void CheckBallCountChange(float currentTime)
        {
            if (!initialized) return;
            
            int currentBallCount = settledBalls.Count + travelingBalls.Count;
            
            // Check if totalBalls setting changed
            if (totalBalls != lastTotalBalls)
            {
                if (totalBalls > currentBallCount)
                {
                    // Need to add more balls - spawn from top
                    int toAdd = totalBalls - currentBallCount;
                    SpawnBallsFromTop(toAdd, currentTime);
                }
                else if (currentBallCount > totalBalls)
                {
                    // Need to remove balls - remove from settled first, then traveling
                    int toRemove = currentBallCount - totalBalls;
                    
                    while (toRemove > 0 && settledBalls.Count > 0)
                    {
                        settledBalls.RemoveAt(settledBalls.Count - 1);
                        toRemove--;
                    }
                    while (toRemove > 0 && travelingBalls.Count > 0)
                    {
                        travelingBalls.RemoveAt(travelingBalls.Count - 1);
                        toRemove--;
                    }
                    
                    UpdateStackPositions();
                }
                
                // Always update lastTotalBalls when the setting changes
                lastTotalBalls = totalBalls;
            }
        }
        
        private void AnalyzeGraph(List<Vector3> nodePositions, List<Vector2Int> edgeConnections)
        {
            if (analysisComplete) return;
            
            cachedPositions = nodePositions;
            if (cachedPositions == null || cachedPositions.Count == 0) return;
            
            // Find height range and top node
            minHeight = float.MaxValue;
            maxHeight = float.MinValue;
            topNode = 0;
            for (int i = 0; i < cachedPositions.Count; i++)
            {
                float y = cachedPositions[i].y;
                minHeight = Mathf.Min(minHeight, y);
                if (y > maxHeight)
                {
                    maxHeight = y;
                    topNode = i;
                }
            }
            
            // Build adjacency
            adjacency.Clear();
            if (edgeConnections != null)
            {
                foreach (var edge in edgeConnections)
                {
                    if (!adjacency.ContainsKey(edge.x))
                        adjacency[edge.x] = new List<int>();
                    if (!adjacency.ContainsKey(edge.y))
                        adjacency[edge.y] = new List<int>();
                    
                    adjacency[edge.x].Add(edge.y);
                    adjacency[edge.y].Add(edge.x);
                }
            }
            
            // Find endpoints (nodes with only one connection)
            endpointNodes.Clear();
            foreach (var kvp in adjacency)
            {
                if (kvp.Value.Count == 1)
                {
                    endpointNodes.Add(kvp.Key);
                }
            }
            
            // Assign frequencies to endpoints
            AssignEndpointFrequencies();
            
            analysisComplete = true;
        }
        
        private void AssignEndpointFrequencies()
        {
            endpointFrequencies.Clear();
            float[] pentatonic = { 261.63f, 293.66f, 329.63f, 392.00f, 440.00f, 523.25f, 587.33f, 659.25f };
            
            var sortedEndpoints = endpointNodes.OrderBy(e => cachedPositions[e].y).ToList();
            for (int i = 0; i < sortedEndpoints.Count; i++)
            {
                endpointFrequencies[sortedEndpoints[i]] = pentatonic[i % pentatonic.Length];
            }
        }
        
        private void SubscribeToInput()
        {
            if (inputMode == InputMode.PlayerController)
            {
                GraphPlayerController.OnEndpointPressed -= OnEndpointTouched;
                GraphPlayerController.OnEndpointPressed += OnEndpointTouched;
            }
        }
        
        private void OnEndpointTouched(LEDGraphManager source, int endpointIndex, Vector3 position)
        {
            if (OwnerGraphManager != null && source != OwnerGraphManager) return;
            pendingTouches.Enqueue(endpointIndex);
        }
        
        private void ProcessInput()
        {
            // Mouse input
            if (inputMode == InputMode.MouseClick && Input.GetMouseButtonDown(0))
            {
                int clicked = GetClickedEndpoint();
                if (clicked >= 0)
                {
                    HandleEndpointTouch(clicked);
                }
            }
            
            // Player controller input
            while (pendingTouches.Count > 0)
            {
                int endpoint = pendingTouches.Dequeue();
                HandleEndpointTouch(endpoint);
            }
        }
        
        private int GetClickedEndpoint()
        {
            if (cachedPositions == null || Camera.main == null) return -1;
            
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            float closestDist = float.MaxValue;
            int closest = -1;
            
            foreach (int endpoint in endpointNodes)
            {
                if (endpoint >= cachedPositions.Count) continue;
                Vector3 pos = cachedPositions[endpoint];
                float dist = Vector3.Cross(ray.direction, pos - ray.origin).magnitude;
                if (dist < 2f && dist < closestDist)
                {
                    closestDist = dist;
                    closest = endpoint;
                }
            }
            
            return closest;
        }
        
        private void HandleEndpointTouch(int endpointIndex)
        {
            float currentTime = Time.realtimeSinceStartup;
            
            // Find all settled balls at this endpoint (ordered by stack position - top first)
            var ballsHere = settledBalls.Where(b => b.endpointIndex == endpointIndex)
                                        .OrderByDescending(b => b.stackPosition)
                                        .ToList();
            
            if (ballsHere.Count > 0)
            {
                // Disperse all balls at this endpoint with stagger delay
                int dispatchIndex = 0;
                foreach (var ball in ballsHere)
                {
                    settledBalls.Remove(ball);
                    
                    // Pick a random OTHER endpoint
                    var otherEndpoints = endpointNodes.Where(e => e != endpointIndex).ToList();
                    if (otherEndpoints.Count > 0)
                    {
                        int target = otherEndpoints[Random.Range(0, otherEndpoints.Count)];
                        var path = FindPath(endpointIndex, target);
                        
                        if (path.Count >= 2)
                        {
                            // Add stagger delay based on dispatch order
                            float delay = dispatchIndex * dispatchStagger;
                            var traveling = new TravelingBall(path, currentTime, ball.hue, ball.ballIndex, delay);
                            
                            // Start the ball from where it was sitting in the stack
                            // Stack position 0 = at endpoint (progress 0)
                            // Higher stack positions = further along the path
                            float pathLength = path.Count - 1;
                            float startOffset = ball.stackPosition * pulseWidth / pathLength;
                            traveling.progress = Mathf.Clamp01(startOffset);
                            traveling.lastProgress = traveling.progress;
                            
                            // Calculate where this ball will land in the target's stack
                            int existingAtTarget = settledBalls.Count(b => b.endpointIndex == target);
                            int incomingToTarget = travelingBalls.Count(b => b.targetEndpoint == target);
                            traveling.targetStackPosition = existingAtTarget + incomingToTarget;
                            
                            travelingBalls.Add(traveling);
                        }
                    }
                    dispatchIndex++;
                }
                
                // Play dispersal sound
                if (enableAudio)
                {
                    PlayTone(endpointIndex, ballsHere.Count);
                }
                
                UpdateStackPositions();
            }
        }
        
        private void UpdateTravelingBalls(float deltaTime, float currentTime)
        {
            for (int i = travelingBalls.Count - 1; i >= 0; i--)
            {
                var ball = travelingBalls[i];
                
                // Check if still in delay period
                float timeSinceSpawn = currentTime - ball.spawnTime;
                if (timeSinceSpawn < ball.startDelay)
                {
                    continue; // Ball hasn't started moving yet
                }
                
                float pathLength = ball.path.Count - 1;
                float progressSpeed = ballSpeed / (pathLength * 10f);
                ball.progress += progressSpeed * deltaTime * speed;
                
                // Recalculate target stack position (other balls may have arrived)
                int currentStackAtTarget = settledBalls.Count(b => b.endpointIndex == ball.targetEndpoint);
                int incomingBeforeMe = travelingBalls.Count(b => 
                    b.targetEndpoint == ball.targetEndpoint && 
                    b != ball && 
                    b.progress > ball.progress);
                ball.targetStackPosition = currentStackAtTarget + incomingBeforeMe;
                
                // Calculate arrival threshold - ball stops at its stack position, not at endpoint
                // Stack position 0 = at endpoint (progress 1.0)
                // Stack position 1 = one pulseWidth back, etc.
                float stackOffset = ball.targetStackPosition * pulseWidth / pathLength;
                float arrivalThreshold = 1f - stackOffset;
                
                // Ball has arrived at its stack position
                if (ball.progress >= arrivalThreshold)
                {
                    ball.progress = arrivalThreshold; // Clamp to exact position
                    
                    // Settle at destination
                    var settled = new SettledBall(ball.targetEndpoint, ball.hue, ball.ballIndex, currentTime);
                    settledBalls.Add(settled);
                    travelingBalls.RemoveAt(i);
                    
                    UpdateStackPositions();
                    
                    // Soft arrival tone
                    if (enableAudio)
                    {
                        PlayTone(ball.targetEndpoint, 1, 0.5f);
                    }
                }
                else
                {
                    ball.lastProgress = ball.progress;
                }
            }
        }
        
        private void UpdateStackPositions()
        {
            // Group settled balls by endpoint and assign stack positions
            var byEndpoint = settledBalls.GroupBy(b => b.endpointIndex);
            
            foreach (var group in byEndpoint)
            {
                int pos = 0;
                foreach (var ball in group.OrderBy(b => b.settleTime))
                {
                    ball.stackPosition = pos++;
                }
            }
        }
        
        private List<int> FindPath(int start, int end)
        {
            if (!adjacency.ContainsKey(start) || !adjacency.ContainsKey(end))
                return new List<int>();
            
            var visited = new HashSet<int>();
            var queue = new Queue<List<int>>();
            queue.Enqueue(new List<int> { start });
            
            while (queue.Count > 0)
            {
                var path = queue.Dequeue();
                int current = path[path.Count - 1];
                
                if (current == end) return path;
                if (visited.Contains(current)) continue;
                visited.Add(current);
                
                if (adjacency.ContainsKey(current))
                {
                    foreach (int neighbor in adjacency[current])
                    {
                        if (!visited.Contains(neighbor))
                        {
                            var newPath = new List<int>(path) { neighbor };
                            queue.Enqueue(newPath);
                        }
                    }
                }
            }
            
            return new List<int>();
        }
        
        // ==================== RENDERING ====================
        
        private void RenderSettledBalls(Color[] colors, List<Vector3> nodePositions, float time)
        {
            if (settledBalls.Count == 0) return;
            
            // Group balls by endpoint for proper stacking
            var ballsByEndpoint = settledBalls.GroupBy(b => b.endpointIndex);
            
            foreach (var group in ballsByEndpoint)
            {
                int endpointIndex = group.Key;
                if (endpointIndex >= nodePositions.Count) continue;
                if (!adjacency.ContainsKey(endpointIndex)) continue;
                
                // Build path inward from this endpoint (following the graph structure)
                var pathFromEndpoint = new List<int> { endpointIndex };
                int current = endpointIndex;
                int prev = -1;
                
                // Traverse inward until we hit a junction or run out of nodes
                int maxDepth = (int)(totalBalls * pulseWidth * 2); // Enough depth for all stacked balls
                for (int step = 0; step < maxDepth && adjacency.ContainsKey(current); step++)
                {
                    var neighbors = adjacency[current].Where(n => n != prev).ToList();
                    if (neighbors.Count == 0) break;
                    
                    prev = current;
                    current = neighbors[0];
                    pathFromEndpoint.Add(current);
                    
                    // Stop if we hit a junction (more than 2 connections means it's a branch point)
                    if (adjacency.ContainsKey(current) && adjacency[current].Count > 2) break;
                }
                
                // Render each ball in the stack along this specific path
                int stackIndex = 0;
                foreach (var ball in group.OrderBy(b => b.settleTime))
                {
                    Color ballColor = useRainbowColors 
                        ? Color.HSVToRGB(ball.hue, rainbowSaturation, rainbowBrightness)
                        : primaryColor;
                    
                    // Gentle pulse
                    float pulse = 1f;
                    if (settledPulse)
                    {
                        pulse = 0.85f + 0.15f * Mathf.Sin((time - ball.settleTime + stackIndex * 0.3f) * settledPulseSpeed * Mathf.PI * 2f);
                    }
                    
                    // Each ball occupies pulseWidth nodes along the path
                    int bandStart = (int)(stackIndex * pulseWidth);
                    int bandEnd = (int)(bandStart + pulseWidth);
                    
                    // Render this ball's band along the path (only on connected nodes)
                    for (int pathIdx = bandStart; pathIdx <= bandEnd && pathIdx < pathFromEndpoint.Count; pathIdx++)
                    {
                        int nodeIndex = pathFromEndpoint[pathIdx];
                        if (nodeIndex >= colors.Length) continue;
                        
                        // Calculate intensity with falloff
                        float posInBand = (pathIdx - bandStart) / Mathf.Max(0.001f, pulseWidth);
                        float intensity = pulseShape.Evaluate(1f - Mathf.Abs(posInBand - 0.5f) * 2f);
                        intensity *= settledIntensity * pulse;
                        
                        colors[nodeIndex] = BlendMax(colors[nodeIndex], ballColor, intensity);
                    }
                    
                    stackIndex++;
                }
            }
        }
        
        private void RenderTravelingBalls(Color[] colors, List<Vector3> nodePositions)
        {
            float currentTime = Time.realtimeSinceStartup;
            
            foreach (var ball in travelingBalls)
            {
                if (ball.path == null || ball.path.Count < 2) continue;
                
                // Skip balls that haven't started moving yet
                if (currentTime - ball.spawnTime < ball.startDelay) continue;
                
                int pathLength = ball.path.Count;
                float pulsePosition = ball.progress * (pathLength - 1);
                
                Color ballColor = useRainbowColors 
                    ? Color.HSVToRGB(ball.hue, rainbowSaturation, rainbowBrightness)
                    : primaryColor;
                
                if (pulseStyle == PulseStyle.VelocityTrail)
                {
                    // Trail
                    float effectiveTrailLength = trailLength * 0.3f;
                    for (int i = 0; i < pathLength; i++)
                    {
                        float signedDist = i - pulsePosition;
                        if (signedDist < 0 && signedDist > -effectiveTrailLength)
                        {
                            int nodeIndex = ball.path[i];
                            if (nodeIndex >= colors.Length) continue;
                            
                            float trailPos = -signedDist / effectiveTrailLength;
                            float trailFade = (1f - trailPos) * trailIntensity;
                            
                            Color trailColor = useRainbowColors
                                ? Color.HSVToRGB(ball.hue, rainbowSaturation * (1f - trailPos * 0.5f), rainbowBrightness * (1f - trailPos * 0.7f))
                                : Color.Lerp(primaryColor, secondaryColor, trailPos) * (1f - trailPos * 0.5f);
                            
                            colors[nodeIndex] = BlendMax(colors[nodeIndex], trailColor, trailFade);
                        }
                    }
                    
                    // Compact head
                    float headWidth = pulseWidth * 0.5f;
                    for (int i = 0; i < pathLength; i++)
                    {
                        float distance = Mathf.Abs(i - pulsePosition);
                        if (distance <= headWidth)
                        {
                            int nodeIndex = ball.path[i];
                            if (nodeIndex >= colors.Length) continue;
                            
                            float intensity = pulseShape.Evaluate(1f - (distance / headWidth));
                            colors[nodeIndex] = BlendMax(colors[nodeIndex], ballColor, intensity);
                        }
                    }
                }
                else
                {
                    // Symmetric glow
                    for (int i = 0; i < pathLength; i++)
                    {
                        float distance = Mathf.Abs(i - pulsePosition);
                        if (distance <= pulseWidth)
                        {
                            int nodeIndex = ball.path[i];
                            if (nodeIndex >= colors.Length) continue;
                            
                            float intensity = pulseShape.Evaluate(1f - (distance / pulseWidth));
                            colors[nodeIndex] = BlendMax(colors[nodeIndex], ballColor, intensity);
                        }
                    }
                }
            }
        }
        
        // ==================== LED MODE RENDERING ====================
        
        private void RenderSettledBallsLED(Color[] colors, MappedGraph mappedGraph, float time)
        {
            if (settledBalls.Count == 0) return;
            
            // Group balls by endpoint for proper stacking
            var ballsByEndpoint = settledBalls.GroupBy(b => b.endpointIndex);
            
            foreach (var group in ballsByEndpoint)
            {
                int endpointIndex = group.Key;
                
                // Map node endpoint to LED endpoint
                int ledEndpoint;
                if (nodeToLEDEndpoint != null && nodeToLEDEndpoint.TryGetValue(endpointIndex, out ledEndpoint))
                {
                    // It was a node index, use the mapped LED
                }
                else if (endpointIndex < mappedGraph.TotalLEDCount)
                {
                    // It's already an LED index
                    ledEndpoint = endpointIndex;
                }
                else
                {
                    continue;
                }
                
                // Find strip containing this endpoint
                if (ledEndpoint < 0 || ledEndpoint >= mappedGraph.LEDToStripIndex.Count)
                    continue;
                    
                int stripIdx = mappedGraph.LEDToStripIndex[ledEndpoint];
                var strip = stripLEDPaths.ContainsKey(stripIdx) ? stripLEDPaths[stripIdx] : null;
                if (strip == null || strip.Count == 0)
                    continue;
                
                int endpointPosInStrip = strip.IndexOf(ledEndpoint);
                if (endpointPosInStrip < 0)
                    continue;
                
                // Determine direction inward (away from endpoint)
                int direction = (endpointPosInStrip == 0) ? 1 : -1;
                
                // Render each ball in the stack
                int stackIndex = 0;
                foreach (var ball in group.OrderBy(b => b.settleTime))
                {
                    Color ballColor = useRainbowColors 
                        ? Color.HSVToRGB(ball.hue, rainbowSaturation, rainbowBrightness)
                        : primaryColor;
                    
                    float pulse = 1f;
                    if (settledPulse)
                    {
                        pulse = 0.85f + 0.15f * Mathf.Sin((time - ball.settleTime + stackIndex * 0.3f) * settledPulseSpeed * Mathf.PI * 2f);
                    }
                    
                    // Each ball occupies pulseWidth LEDs, stacked from endpoint inward
                    float bandStart = stackIndex * pulseWidth;
                    float bandEnd = bandStart + pulseWidth;
                    
                    // Render this ball's band along the strip
                    for (int offset = 0; offset < strip.Count; offset++)
                    {
                        int ledIndex = endpointPosInStrip + (offset * direction);
                        if (ledIndex < 0 || ledIndex >= strip.Count) break;
                        
                        int led = strip[ledIndex];
                        if (led >= colors.Length) continue;
                        
                        float ledPos = offset; // Position from endpoint
                        
                        // Check if this LED falls within this ball's band
                        if (ledPos >= bandStart && ledPos < bandEnd)
                        {
                            // Calculate intensity with falloff at edges using pulse shape
                            float posInBand = (ledPos - bandStart) / pulseWidth;
                            float intensity = pulseShape.Evaluate(1f - Mathf.Abs(posInBand - 0.5f) * 2f);
                            intensity *= settledIntensity * pulse;
                            
                            colors[led] = BlendMax(colors[led], ballColor, intensity);
                        }
                    }
                    
                    stackIndex++;
                }
            }
        }
        
        private void RenderTravelingBallsLED(Color[] colors, MappedGraph mappedGraph)
        {
            float currentTime = Time.realtimeSinceStartup;
            
            foreach (var ball in travelingBalls)
            {
                if (ball.path == null || ball.path.Count < 2) continue;
                
                // Skip balls that haven't started moving yet
                if (currentTime - ball.spawnTime < ball.startDelay) continue;
                
                // Get target LED (might be node index or LED index depending on init path)
                int targetLED;
                if (nodeToLEDEndpoint.TryGetValue(ball.targetEndpoint, out targetLED))
                {
                    // It was a node index
                }
                else if (ball.targetEndpoint < mappedGraph.TotalLEDCount)
                {
                    // It's already an LED index
                    targetLED = ball.targetEndpoint;
                }
                else
                {
                    continue;
                }
                
                if (targetLED < 0 || targetLED >= mappedGraph.LEDToStripIndex.Count) continue;
                int stripIdx = mappedGraph.LEDToStripIndex[targetLED];
                var ledPath = stripLEDPaths.ContainsKey(stripIdx) ? stripLEDPaths[stripIdx] : null;
                if (ledPath == null || ledPath.Count == 0) continue;
                
                int ledCount = ledPath.Count;
                float ledPosition = ball.progress * (ledCount - 1);
                
                // Determine direction
                bool targetAtEnd = targetLED == ledPath[ledPath.Count - 1];
                int direction = 1;
                if (!targetAtEnd && targetLED == ledPath[0])
                {
                    ledPosition = (ledCount - 1) - ledPosition;
                    direction = -1;
                }
                
                Color ballColor = useRainbowColors 
                    ? Color.HSVToRGB(ball.hue, rainbowSaturation, rainbowBrightness)
                    : primaryColor;
                
                if (pulseStyle == PulseStyle.VelocityTrail)
                {
                    // Trail
                    float effectiveTrailLength = trailLength;
                    for (int i = 0; i < ledCount; i++)
                    {
                        float signedDist = (i - ledPosition) * direction;
                        if (signedDist < 0 && signedDist > -effectiveTrailLength)
                        {
                            int ledIdx = ledPath[i];
                            if (ledIdx >= colors.Length) continue;
                            
                            float trailPos = -signedDist / effectiveTrailLength;
                            float trailFade = (1f - trailPos) * trailIntensity;
                            
                            Color trailColor = useRainbowColors
                                ? Color.HSVToRGB(ball.hue, rainbowSaturation * (1f - trailPos * 0.5f), rainbowBrightness * (1f - trailPos * 0.7f))
                                : Color.Lerp(primaryColor, secondaryColor, trailPos) * (1f - trailPos * 0.5f);
                            
                            colors[ledIdx] = BlendMax(colors[ledIdx], trailColor, trailFade);
                        }
                    }
                    
                    // Head
                    float headWidth = pulseWidth * 0.5f;
                    for (int i = 0; i < ledCount; i++)
                    {
                        float distance = Mathf.Abs(i - ledPosition);
                        if (distance <= headWidth)
                        {
                            int ledIdx = ledPath[i];
                            if (ledIdx >= colors.Length) continue;
                            
                            float intensity = pulseShape.Evaluate(1f - (distance / headWidth));
                            colors[ledIdx] = BlendMax(colors[ledIdx], ballColor, intensity);
                        }
                    }
                }
                else
                {
                    // Symmetric
                    for (int i = 0; i < ledCount; i++)
                    {
                        float distance = Mathf.Abs(i - ledPosition);
                        if (distance <= pulseWidth)
                        {
                            int ledIdx = ledPath[i];
                            if (ledIdx >= colors.Length) continue;
                            
                            float intensity = pulseShape.Evaluate(1f - (distance / pulseWidth));
                            colors[ledIdx] = BlendMax(colors[ledIdx], ballColor, intensity);
                        }
                    }
                }
            }
        }
        
        private Color BlendMax(Color current, Color add, float intensity)
        {
            Color blended = add * intensity;
            return new Color(
                Mathf.Max(current.r, blended.r),
                Mathf.Max(current.g, blended.g),
                Mathf.Max(current.b, blended.b),
                1f
            );
        }
        
        // ==================== AUDIO ====================
        
        private void SetupAudio()
        {
            if (audioContainer == null)
            {
                audioContainer = new GameObject("BallSettleAudio");
                audioContainer.hideFlags = HideFlags.HideAndDontSave;
                if (Application.isPlaying)
                    Object.DontDestroyOnLoad(audioContainer);
            }
            
            if (toneSource == null)
            {
                toneSource = audioContainer.AddComponent<AudioSource>();
                toneSource.playOnAwake = false;
            }
            
            // Pre-generate tone clips
            foreach (var kvp in endpointFrequencies)
            {
                if (!toneClips.ContainsKey(kvp.Key))
                {
                    toneClips[kvp.Key] = GenerateToneClip(kvp.Value, 0.4f);
                }
            }
            
            audioInitialized = true;
        }
        
        private AudioClip GenerateToneClip(float frequency, float duration)
        {
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];
            
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = Mathf.Exp(-t * 4f); // Quick decay
                
                // Soft bell-like tone
                float sample = Mathf.Sin(2f * Mathf.PI * frequency * t) * 0.5f;
                sample += Mathf.Sin(4f * Mathf.PI * frequency * t) * 0.25f;
                sample += Mathf.Sin(6f * Mathf.PI * frequency * t) * 0.1f;
                
                data[i] = sample * envelope;
            }
            
            AudioClip clip = AudioClip.Create($"Tone_{frequency}", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
        
        private void PlayTone(int endpointIndex, int ballCount, float volumeMultiplier = 1f)
        {
            if (!audioInitialized || toneSource == null) return;
            
            if (toneClips.TryGetValue(endpointIndex, out AudioClip clip))
            {
                // Play multiple times for stacked balls (with slight pitch variation)
                for (int i = 0; i < Mathf.Min(ballCount, 3); i++)
                {
                    toneSource.pitch = 1f + i * 0.1f;
                    toneSource.PlayOneShot(clip, toneVolume * volumeMultiplier);
                }
                toneSource.pitch = 1f;
            }
        }
        
        // ==================== PUBLIC API ====================
        
        /// <summary>
        /// Number of balls currently settled
        /// </summary>
        public int SettledBallCount => settledBalls.Count;
        
        /// <summary>
        /// Number of balls currently traveling
        /// </summary>
        public int TravelingBallCount => travelingBalls.Count;
        
        /// <summary>
        /// Get endpoints that have settled balls
        /// </summary>
        public List<int> GetOccupiedEndpoints()
        {
            return settledBalls.Select(b => b.endpointIndex).Distinct().ToList();
        }
        
        /// <summary>
        /// Get count of balls at a specific endpoint
        /// </summary>
        public int GetBallCountAtEndpoint(int endpointIndex)
        {
            return settledBalls.Count(b => b.endpointIndex == endpointIndex);
        }
        
        /// <summary>
        /// Simulate touching an endpoint (for external triggering)
        /// </summary>
        public void SimulateTouchEndpoint(int endpointIndex)
        {
            if (endpointNodes.Contains(endpointIndex))
            {
                HandleEndpointTouch(endpointIndex);
            }
        }
        
        /// <summary>
        /// Add a new ball to the system
        /// </summary>
        public void AddBall(int atEndpoint = -1)
        {
            if (atEndpoint < 0 && endpointNodes.Count > 0)
            {
                atEndpoint = endpointNodes[Random.Range(0, endpointNodes.Count)];
            }
            
            if (atEndpoint >= 0)
            {
                float hue = (float)nextBallIndex / Mathf.Max(1, totalBalls);
                var ball = new SettledBall(atEndpoint, hue, nextBallIndex, Time.realtimeSinceStartup);
                settledBalls.Add(ball);
                nextBallIndex++;
                UpdateStackPositions();
            }
        }
        
        /// <summary>
        /// Reset to initial state
        /// </summary>
        [ContextMenu("Reset Animation")]
        public void ResetAnimation()
        {
            travelingBalls.Clear();
            settledBalls.Clear();
            nextBallIndex = 0;
            initialized = false;
        }
        
        /// <summary>
        /// Clean up audio and state when animation is disabled or changed
        /// </summary>
        public void CleanupAudio()
        {
            if (toneSource != null)
            {
                toneSource.Stop();
            }
            
            if (audioContainer != null)
            {
                Object.DestroyImmediate(audioContainer);
                audioContainer = null;
            }
            
            audioInitialized = false;
        }
        
        private void OnDisable()
        {
            GraphPlayerController.OnEndpointPressed -= OnEndpointTouched;
            CleanupAudio();
        }
    }
}
