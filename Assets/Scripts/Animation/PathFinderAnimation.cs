using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using BranchingLEDAnimator.Core;
using BranchingLEDAnimator.Player;

namespace BranchingLEDAnimator.Animation
{
    /// <summary>
    /// Interactive pathfinding animation where touching an endpoint starts a fill
    /// that animates along a path to another random endpoint. Touching the destination
    /// redirects the fill to a new target. Supports multiple simultaneous paths.
    /// </summary>
    [CreateAssetMenu(fileName = "PathFinderAnimation", menuName = "LED Animations/Interactive/Path Finder")]
    public class PathFinderAnimation : LEDAnimationType
    {
        public enum FillStyle
        {
            SolidFill,          // Solid color fill that stays lit
            GradientFill,       // Gradient from source to destination color
            ChasingFill,        // Fill with directional chasing particles
            PulseFill           // Fill with pulsing intensity wave
        }
        
        public enum ColorMode
        {
            GlobalGradient,     // Source/destination colors apply to all paths
            PerLegColors,       // Each endpoint has its own assigned color
            Monochrome          // Single trail color, multiple paths get spaced hues
        }
        
        public enum PathState
        {
            Expanding,          // Fill traveling from source to destination
            Waiting,            // Fill arrived, waiting for player
            Receding,           // Fill receding back into the triggered endpoint
            Complete            // Path finished, ready for removal
        }
        
        [Header("Path Settings")]
        [Tooltip("Time for the fill to travel from source to destination")]
        [Range(0.5f, 10f)]
        public float pathDuration = 3f;
        
        [Tooltip("Time for the path to recede after destination is touched")]
        [Range(0.1f, 3f)]
        public float recedeDuration = 0.8f;
        
        [Tooltip("Style of the fill animation")]
        public FillStyle fillStyle = FillStyle.GradientFill;
        
        [Tooltip("Width of the leading edge of the fill (in nodes)")]
        [Range(1f, 30f)]
        public float leadingEdgeWidth = 8f;
        
        [Tooltip("How quickly filled nodes fade after the leading edge passes")]
        [Range(0f, 1f)]
        public float trailPersistence = 0.8f;
        
        [Header("Multi-Path Settings")]
        [Tooltip("Maximum number of simultaneous paths")]
        [Range(1, 10)]
        public int maxSimultaneousPaths = 4;
        
        [Tooltip("If true, new paths avoid endpoints already in use")]
        public bool avoidActiveEndpoints = true;
        
        [Header("Color Mode")]
        public ColorMode colorMode = ColorMode.GlobalGradient;
        
        [Header("Global Gradient Colors (when ColorMode = GlobalGradient)")]
        public Color sourceColor = new Color(0.2f, 0.8f, 1f);  // Cyan
        public Color destinationColor = new Color(1f, 0.4f, 0.8f);  // Pink
        
        [Header("Monochrome Colors (when ColorMode = Monochrome)")]
        public Color monochromeTrailColor = new Color(0f, 0.8f, 1f);
        public Color monochromeBackground = new Color(0.02f, 0.02f, 0.05f);
        [Tooltip("Hue offset between multiple simultaneous paths")]
        [Range(0f, 0.5f)]
        public float multiPathHueSpacing = 0.25f;
        
        [Header("Per-Leg Colors (when ColorMode = PerLegColors)")]
        [Tooltip("Saturation for auto-generated leg colors")]
        [Range(0f, 1f)]
        public float legColorSaturation = 0.8f;
        [Tooltip("Brightness for auto-generated leg colors")]
        [Range(0f, 1f)]
        public float legColorBrightness = 1f;
        
        [Header("Common Colors")]
        public Color leadingEdgeColor = Color.white;
        public Color idleEndpointColor = new Color(0.1f, 0.2f, 0.3f);
        
        [Tooltip("Idle endpoint pulse speed")]
        public float idlePulseSpeed = 1f;
        [Tooltip("Idle endpoint pulse amount")]
        [Range(0f, 0.5f)]
        public float idlePulseAmount = 0.2f;
        
        [Header("Chasing Effect (when FillStyle = ChasingFill)")]
        [Range(0, 10)]
        public int chaseParticleCount = 3;
        [Range(1f, 3f)]
        public float chaseSpeed = 1.5f;
        [Range(0.1f, 0.5f)]
        public float chaseSpacing = 0.2f;
        
        [Header("Audio")]
        [Tooltip("Enable audio blending between source and destination")]
        public bool enableAudio = true;
        public float baseFrequency = 220f;
        [Range(0f, 1f)]
        public float audioVolume = 0.4f;
        public float audioAttack = 0.3f;
        public float audioRelease = 1f;
        
        [Header("Gameplay")]
        [Tooltip("Time window to touch destination after fill arrives (0 = unlimited)")]
        [Range(0f, 10f)]
        public float destinationWindow = 5f;
        
        [Tooltip("If true, any endpoint can start a new path")]
        public bool anyEndpointCanStart = true;
        
        [Tooltip("Visual indication of the target endpoint")]
        public bool highlightDestination = true;
        public float destinationPulseSpeed = 3f;
        
        [Header("Debug")]
        public bool showPathDebug = false;
        
        // Active path class
        private class ActivePath
        {
            public int id;
            public int sourceEndpoint;
            public int destinationEndpoint;
            public List<int> pathNodes = new List<int>();
            public Dictionary<int, int> nodePathIndex = new Dictionary<int, int>();
            public float startTime;
            public float stateChangeTime;
            public PathState state = PathState.Expanding;
            public Color pathColor;
            public float hueOffset;  // For monochrome mode
            public bool wasDestinationTriggered;  // True if player touched destination, false if timeout
            
            // Audio
            public AudioSource sourceAudio;
            public AudioSource destAudio;
            public float currentSourceVol;
            public float currentDestVol;
            public float targetSourceVol;
            public float targetDestVol;
        }
        
        // State
        private List<int> endpointNodes = new List<int>();
        private Dictionary<int, List<int>> adjacency = new Dictionary<int, List<int>>();
        private Dictionary<int, float> endpointFrequencies = new Dictionary<int, float>();
        private Dictionary<int, Color> endpointColors = new Dictionary<int, Color>();
        private bool analysisComplete = false;
        private bool subscribedToEvents = false;
        private List<Vector3> cachedPositions;
        
        // Active paths
        private List<ActivePath> activePaths = new List<ActivePath>();
        private int nextPathId = 0;
        private GameObject audioContainer;
        
        public override bool SupportsLEDMode => false;
        
        protected override Color[] CalculateBaseNodeColors(
            List<Vector3> nodePositions,
            List<Vector2Int> edgeConnections,
            List<int> sourceNodes,
            float time,
            int frame)
        {
            Color[] colors = new Color[nodePositions.Count];
            
            // Initialize with inactive/background
            Color bgColor = colorMode == ColorMode.Monochrome ? monochromeBackground : inactiveColor;
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = bgColor;
            }
            
            // First-time analysis
            if (!analysisComplete)
            {
                AnalyzeGraph(nodePositions, edgeConnections);
                SetupAudioContainer();
                SubscribeToPlayerEvents();
            }
            
            float deltaTime = Time.deltaTime;
            
            // Update all active paths
            UpdateActivePaths(time, deltaTime);
            
            // Render idle endpoints (not part of any active path)
            RenderIdleEndpoints(time, colors);
            
            // Render all active paths (oldest first so newest paints over)
            foreach (var path in activePaths)
            {
                RenderPath(path, time, colors);
            }
            
            // Highlight destinations
            foreach (var path in activePaths.Where(p => p.state == PathState.Expanding || p.state == PathState.Waiting))
            {
                if (highlightDestination && path.destinationEndpoint >= 0)
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin(time * destinationPulseSpeed * Mathf.PI * 2f);
                    float intensity = path.state == PathState.Waiting ? 1f : 0.5f + 0.5f * pulse;
                    Color destColor = GetDestinationColor(path);
                    colors[path.destinationEndpoint] = Color.Lerp(colors[path.destinationEndpoint], destColor, intensity);
                }
            }
            
            // Clean up completed paths
            activePaths.RemoveAll(p => p.state == PathState.Complete);
            
            return colors;
        }
        
        void AnalyzeGraph(List<Vector3> nodePositions, List<Vector2Int> edgeConnections)
        {
            cachedPositions = nodePositions;
            endpointNodes.Clear();
            adjacency.Clear();
            endpointFrequencies.Clear();
            endpointColors.Clear();
            
            // Build adjacency
            for (int i = 0; i < nodePositions.Count; i++)
            {
                adjacency[i] = new List<int>();
            }
            
            foreach (var edge in edgeConnections)
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
            foreach (var kvp in adjacency)
            {
                if (kvp.Value.Count == 1)
                {
                    endpointNodes.Add(kvp.Key);
                }
            }
            
            // Sort by height for consistent assignment
            endpointNodes = endpointNodes.OrderBy(e => nodePositions[e].y).ToList();
            
            // Assign frequencies and colors per endpoint
            int[] pentatonic = { 0, 2, 4, 7, 9 };
            for (int i = 0; i < endpointNodes.Count; i++)
            {
                int endpoint = endpointNodes[i];
                
                // Frequency
                int scaleIndex = i % pentatonic.Length;
                int octave = i / pentatonic.Length;
                int semitones = pentatonic[scaleIndex] + (octave * 12);
                float frequency = baseFrequency * Mathf.Pow(2f, semitones / 12f);
                endpointFrequencies[endpoint] = frequency;
                
                // Color (for per-leg mode)
                float hue = (float)i / endpointNodes.Count;
                endpointColors[endpoint] = Color.HSVToRGB(hue, legColorSaturation, legColorBrightness);
            }
            
            Debug.Log($"🔍 PathFinder: Found {endpointNodes.Count} endpoints");
            analysisComplete = true;
        }
        
        void SetupAudioContainer()
        {
            if (!enableAudio) return;
            
            if (audioContainer == null)
            {
                audioContainer = new GameObject("PathFinderAudio");
                audioContainer.hideFlags = HideFlags.HideAndDontSave;
            }
        }
        
        AudioSource CreateAudioSource(string name, float frequency)
        {
            // Ensure audio container exists
            if (audioContainer == null)
            {
                audioContainer = new GameObject("PathFinderAudio");
                audioContainer.hideFlags = HideFlags.HideAndDontSave;
            }
            
            var go = new GameObject(name);
            go.transform.SetParent(audioContainer.transform);
            go.hideFlags = HideFlags.HideAndDontSave;
            
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.volume = 0f;
            source.clip = GenerateToneClip(frequency);
            
            return source;
        }
        
        AudioClip GenerateToneClip(float frequency)
        {
            int sampleRate = 44100;
            float wavelengthSamples = sampleRate / frequency;
            int numCycles = Mathf.Max(10, Mathf.RoundToInt(frequency / 10f));
            int sampleCount = Mathf.RoundToInt(wavelengthSamples * numCycles);
            
            float[] samples = new float[sampleCount];
            
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float sample = Mathf.Sin(2f * Mathf.PI * frequency * t);
                sample += 0.3f * Mathf.Sin(2f * Mathf.PI * frequency * 2f * t);
                sample += 0.15f * Mathf.Sin(2f * Mathf.PI * frequency * 3f * t);
                sample *= 0.4f;
                samples[i] = sample;
            }
            
            AudioClip clip = AudioClip.Create($"Tone_{frequency:F0}Hz", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
        
        void SubscribeToPlayerEvents()
        {
            if (subscribedToEvents) return;
            
            GraphPlayerController.OnEndpointPressed += OnEndpointPressed;
            subscribedToEvents = true;
        }
        
        void OnEndpointPressed(LEDGraphManager source, int endpointIndex, Vector3 position)
        {
            if (OwnerGraphManager != null && source != OwnerGraphManager) return;
            if (!endpointNodes.Contains(endpointIndex))
            {
                if (showPathDebug) Debug.Log($"❌ Endpoint {endpointIndex} not in endpoint list");
                return;
            }
            
            if (showPathDebug)
            {
                Debug.Log($"🎯 Endpoint {endpointIndex} pressed. Active paths: {activePaths.Count}, Max: {maxSimultaneousPaths}");
            }
            
            // Check if this endpoint is a destination of any waiting path
            var waitingPath = activePaths.FirstOrDefault(p => 
                p.state == PathState.Waiting && p.destinationEndpoint == endpointIndex);
            
            if (waitingPath != null)
            {
                // Trigger recede and then start new path from this endpoint
                if (showPathDebug) Debug.Log($"✅ Endpoint {endpointIndex} is waiting destination - starting recede");
                StartRecede(waitingPath, endpointIndex);
                return;
            }
            
            // Check if this endpoint is already in use
            if (avoidActiveEndpoints && IsEndpointInUse(endpointIndex))
            {
                if (showPathDebug) Debug.Log($"⚠️ Endpoint {endpointIndex} already in use by another path");
                return;
            }
            
            // Check max paths
            if (activePaths.Count >= maxSimultaneousPaths)
            {
                if (showPathDebug) Debug.Log($"⚠️ Max paths ({maxSimultaneousPaths}) reached, current: {activePaths.Count}");
                return;
            }
            
            // Check if we can start from any endpoint, or only when no paths active
            if (!anyEndpointCanStart && activePaths.Count > 0)
            {
                if (showPathDebug) Debug.Log($"⚠️ anyEndpointCanStart is false and {activePaths.Count} paths already active");
                return;
            }
            
            if (showPathDebug) Debug.Log($"✅ Starting new path from endpoint {endpointIndex}");
            StartNewPath(endpointIndex);
        }
        
        bool IsEndpointInUse(int endpoint)
        {
            return activePaths.Any(p => 
                p.sourceEndpoint == endpoint || 
                p.destinationEndpoint == endpoint);
        }
        
        HashSet<int> GetActiveEndpoints()
        {
            var active = new HashSet<int>();
            foreach (var path in activePaths)
            {
                active.Add(path.sourceEndpoint);
                active.Add(path.destinationEndpoint);
            }
            return active;
        }
        
        void StartNewPath(int fromEndpoint)
        {
            // Pick a random destination, avoiding active endpoints
            var activeEndpoints = GetActiveEndpoints();
            var availableDestinations = endpointNodes
                .Where(e => e != fromEndpoint && (!avoidActiveEndpoints || !activeEndpoints.Contains(e)))
                .ToList();
            
            if (availableDestinations.Count == 0)
            {
                // Fallback: allow any endpoint except source
                availableDestinations = endpointNodes.Where(e => e != fromEndpoint).ToList();
            }
            
            if (availableDestinations.Count == 0) return;
            
            int destinationEndpoint = availableDestinations[Random.Range(0, availableDestinations.Count)];
            
            // Find path using BFS
            var pathNodes = FindPath(fromEndpoint, destinationEndpoint);
            if (pathNodes == null || pathNodes.Count == 0)
            {
                Debug.LogWarning($"❌ No path found from {fromEndpoint} to {destinationEndpoint}");
                return;
            }
            
            // Create new path
            var newPath = new ActivePath
            {
                id = nextPathId++,
                sourceEndpoint = fromEndpoint,
                destinationEndpoint = destinationEndpoint,
                pathNodes = pathNodes,
                startTime = Time.time,
                stateChangeTime = Time.time,
                state = PathState.Expanding
            };
            
            // Build node-to-index lookup
            for (int i = 0; i < pathNodes.Count; i++)
            {
                newPath.nodePathIndex[pathNodes[i]] = i;
            }
            
            // Assign color based on mode
            AssignPathColor(newPath);
            
            // Setup audio - CreateAudioSource will create container if needed
            if (enableAudio)
            {
                float srcFreq = endpointFrequencies.ContainsKey(fromEndpoint) ? endpointFrequencies[fromEndpoint] : baseFrequency;
                float dstFreq = endpointFrequencies.ContainsKey(destinationEndpoint) ? endpointFrequencies[destinationEndpoint] : baseFrequency * 1.5f;
                
                newPath.sourceAudio = CreateAudioSource($"Path{newPath.id}_Src", srcFreq);
                newPath.destAudio = CreateAudioSource($"Path{newPath.id}_Dst", dstFreq);
                
                newPath.sourceAudio.Play();
                newPath.destAudio.Play();
                
                newPath.targetSourceVol = 1f;
                newPath.targetDestVol = 0f;
            }
            
            activePaths.Add(newPath);
            
            if (showPathDebug)
            {
                Debug.Log($"🚀 Path {newPath.id} started: {fromEndpoint} → {destinationEndpoint} ({pathNodes.Count} nodes). Total active: {activePaths.Count}");
            }
        }
        
        void AssignPathColor(ActivePath path)
        {
            switch (colorMode)
            {
                case ColorMode.GlobalGradient:
                    path.pathColor = sourceColor;
                    break;
                    
                case ColorMode.PerLegColors:
                    // Use the source endpoint's color
                    path.pathColor = endpointColors.ContainsKey(path.sourceEndpoint) 
                        ? endpointColors[path.sourceEndpoint] 
                        : sourceColor;
                    break;
                    
                case ColorMode.Monochrome:
                    // Space out hues based on number of active paths
                    int pathIndex = activePaths.Count;
                    path.hueOffset = (pathIndex * multiPathHueSpacing) % 1f;
                    Color.RGBToHSV(monochromeTrailColor, out float h, out float s, out float v);
                    path.pathColor = Color.HSVToRGB((h + path.hueOffset) % 1f, s, v);
                    break;
            }
        }
        
        Color GetDestinationColor(ActivePath path)
        {
            switch (colorMode)
            {
                case ColorMode.PerLegColors:
                    return endpointColors.ContainsKey(path.destinationEndpoint) 
                        ? endpointColors[path.destinationEndpoint] 
                        : destinationColor;
                default:
                    return destinationColor;
            }
        }
        
        void StartRecede(ActivePath path, int triggeredEndpoint, bool wasTriggered = true)
        {
            path.state = PathState.Receding;
            path.stateChangeTime = Time.time;
            path.wasDestinationTriggered = wasTriggered;
            
            // Fade out audio
            path.targetSourceVol = 0f;
            path.targetDestVol = 0f;
            
            if (showPathDebug)
            {
                Debug.Log($"🔙 Path {path.id} receding (triggered={wasTriggered})");
            }
            
            // Store the endpoint for potential new path start
            path.destinationEndpoint = triggeredEndpoint;
        }
        
        List<int> FindPath(int start, int end)
        {
            Queue<int> queue = new Queue<int>();
            Dictionary<int, int> parent = new Dictionary<int, int>();
            HashSet<int> visited = new HashSet<int>();
            
            queue.Enqueue(start);
            visited.Add(start);
            parent[start] = -1;
            
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                
                if (current == end)
                {
                    List<int> path = new List<int>();
                    int node = end;
                    while (node != -1)
                    {
                        path.Add(node);
                        node = parent[node];
                    }
                    path.Reverse();
                    return path;
                }
                
                if (adjacency.ContainsKey(current))
                {
                    var neighbors = adjacency[current].OrderBy(x => Random.value).ToList();
                    foreach (int neighbor in neighbors)
                    {
                        if (!visited.Contains(neighbor))
                        {
                            visited.Add(neighbor);
                            parent[neighbor] = current;
                            queue.Enqueue(neighbor);
                        }
                    }
                }
            }
            
            return null;
        }
        
        void UpdateActivePaths(float time, float deltaTime)
        {
            foreach (var path in activePaths.ToList())
            {
                // Update audio
                UpdatePathAudio(path, time, deltaTime);
                
                switch (path.state)
                {
                    case PathState.Expanding:
                        float expandProgress = (time - path.startTime) / pathDuration;
                        if (expandProgress >= 1f)
                        {
                            path.state = PathState.Waiting;
                            path.stateChangeTime = time;
                            if (showPathDebug)
                            {
                                Debug.Log($"🎯 Path {path.id} arrived at destination {path.destinationEndpoint}");
                            }
                        }
                        break;
                        
                    case PathState.Waiting:
                        if (destinationWindow > 0f && time - path.stateChangeTime > destinationWindow)
                        {
                            // Timeout - start receding (NOT triggered, so won't auto-start new path)
                            StartRecede(path, path.destinationEndpoint, wasTriggered: false);
                            if (showPathDebug)
                            {
                                Debug.Log($"⏰ Path {path.id} timeout - receding (will not auto-continue)");
                            }
                        }
                        break;
                        
                    case PathState.Receding:
                        float recedeProgress = (time - path.stateChangeTime) / recedeDuration;
                        if (recedeProgress >= 1f)
                        {
                            // Check if we should start a new path from where this one ended
                            int nextSource = path.destinationEndpoint;
                            bool wasTriggered = path.wasDestinationTriggered;
                            
                            // Mark this path as complete
                            path.state = PathState.Complete;
                            
                            // Clean up audio
                            if (path.sourceAudio != null)
                            {
                                Object.DestroyImmediate(path.sourceAudio.gameObject);
                            }
                            if (path.destAudio != null)
                            {
                                Object.DestroyImmediate(path.destAudio.gameObject);
                            }
                            
                            if (showPathDebug)
                            {
                                Debug.Log($"✅ Path {path.id} complete (triggered={wasTriggered})");
                            }
                            
                            // Only auto-start new path if destination was manually triggered (not timeout)
                            // AND we're below the max path limit (count -1 because this path is about to be removed)
                            if (wasTriggered && nextSource >= 0 && endpointNodes.Contains(nextSource))
                            {
                                int currentCount = activePaths.Count(p => p.state != PathState.Complete);
                                if (currentCount < maxSimultaneousPaths)
                                {
                                    StartNewPath(nextSource);
                                }
                                else if (showPathDebug)
                                {
                                    Debug.Log($"⚠️ Not auto-starting: at max paths ({currentCount}/{maxSimultaneousPaths})");
                                }
                            }
                        }
                        break;
                }
            }
        }
        
        void UpdatePathAudio(ActivePath path, float time, float deltaTime)
        {
            if (!enableAudio || path.sourceAudio == null) return;
            
            float attackSpeed = 1f / Mathf.Max(0.01f, audioAttack);
            float releaseSpeed = 1f / Mathf.Max(0.01f, audioRelease);
            
            // Update source volume
            if (path.currentSourceVol < path.targetSourceVol)
                path.currentSourceVol = Mathf.Min(path.currentSourceVol + attackSpeed * deltaTime, path.targetSourceVol);
            else
                path.currentSourceVol = Mathf.Max(path.currentSourceVol - releaseSpeed * deltaTime, path.targetSourceVol);
            
            // Update dest volume
            if (path.currentDestVol < path.targetDestVol)
                path.currentDestVol = Mathf.Min(path.currentDestVol + attackSpeed * deltaTime, path.targetDestVol);
            else
                path.currentDestVol = Mathf.Max(path.currentDestVol - releaseSpeed * deltaTime, path.targetDestVol);
            
            // Blend based on progress during expansion
            if (path.state == PathState.Expanding)
            {
                float progress = Mathf.Clamp01((time - path.startTime) / pathDuration);
                path.targetSourceVol = 1f - progress * 0.7f;
                path.targetDestVol = progress;
            }
            
            path.sourceAudio.volume = path.currentSourceVol * audioVolume;
            path.destAudio.volume = path.currentDestVol * audioVolume;
        }
        
        void RenderIdleEndpoints(float time, Color[] colors)
        {
            var activeEndpoints = GetActiveEndpoints();
            
            foreach (int endpoint in endpointNodes)
            {
                if (activeEndpoints.Contains(endpoint)) continue;
                
                float pulse = 1f + idlePulseAmount * Mathf.Sin(time * idlePulseSpeed * Mathf.PI * 2f + endpoint * 0.5f);
                colors[endpoint] = idleEndpointColor * pulse;
            }
        }
        
        void RenderPath(ActivePath path, float time, Color[] colors)
        {
            if (path.pathNodes.Count == 0) return;
            
            float fillPosition;
            int pathLength = path.pathNodes.Count;
            
            switch (path.state)
            {
                case PathState.Expanding:
                    float expandProgress = Mathf.Clamp01((time - path.startTime) / pathDuration);
                    fillPosition = expandProgress * (pathLength - 1);
                    RenderExpandingFill(path, fillPosition, time, colors);
                    break;
                    
                case PathState.Waiting:
                    // Full fill, possibly pulsing
                    fillPosition = pathLength - 1;
                    RenderExpandingFill(path, fillPosition, time, colors);
                    break;
                    
                case PathState.Receding:
                    float recedeProgress = Mathf.Clamp01((time - path.stateChangeTime) / recedeDuration);
                    // Recede from source towards destination
                    float recedePosition = recedeProgress * (pathLength - 1);
                    RenderRecedingFill(path, recedePosition, time, colors);
                    break;
            }
            
            // Always highlight source during expansion/waiting
            if (path.state != PathState.Receding)
            {
                colors[path.sourceEndpoint] = path.pathColor;
            }
        }
        
        void RenderExpandingFill(ActivePath path, float fillPosition, float time, Color[] colors)
        {
            int pathLength = path.pathNodes.Count;
            
            for (int i = 0; i < pathLength; i++)
            {
                int node = path.pathNodes[i];
                float nodePos = i;
                float normalizedPos = (float)i / Mathf.Max(1, pathLength - 1);
                
                Color nodeColor = GetNodeColor(path, normalizedPos);
                
                if (nodePos <= fillPosition)
                {
                    // Filled
                    float fillIntensity = 1f;
                    
                    if (trailPersistence < 1f)
                    {
                        float distFromEdge = fillPosition - nodePos;
                        fillIntensity = Mathf.Lerp(1f, trailPersistence, distFromEdge / pathLength);
                    }
                    
                    // Apply fill style effects
                    if (fillStyle == FillStyle.PulseFill)
                    {
                        float pulse = 0.7f + 0.3f * Mathf.Sin(time * 5f - normalizedPos * 10f);
                        fillIntensity *= pulse;
                    }
                    
                    colors[node] = nodeColor * fillIntensity;
                }
                else if (nodePos < fillPosition + leadingEdgeWidth)
                {
                    // Leading edge - use destination color for PerLegColors mode
                    Color edgeColor = GetLeadingEdgeColor(path);
                    float edgeProgress = (nodePos - fillPosition) / leadingEdgeWidth;
                    colors[node] = Color.Lerp(edgeColor, colors[node], edgeProgress);
                }
            }
            
            // Chasing particles
            if (fillStyle == FillStyle.ChasingFill)
            {
                RenderChaseParticles(path, fillPosition, time, colors);
            }
        }
        
        void RenderRecedingFill(ActivePath path, float recedePosition, float time, Color[] colors)
        {
            int pathLength = path.pathNodes.Count;
            
            for (int i = 0; i < pathLength; i++)
            {
                int node = path.pathNodes[i];
                float nodePos = i;
                float normalizedPos = (float)i / Mathf.Max(1, pathLength - 1);
                
                // Only render nodes that haven't been receded yet
                if (nodePos >= recedePosition)
                {
                    Color nodeColor = GetNodeColor(path, normalizedPos);
                    
                    // Fade near the receding edge
                    if (nodePos < recedePosition + leadingEdgeWidth)
                    {
                        float edgeProgress = (nodePos - recedePosition) / leadingEdgeWidth;
                        colors[node] = Color.Lerp(colors[node], nodeColor, edgeProgress);
                    }
                    else
                    {
                        colors[node] = nodeColor * trailPersistence;
                    }
                }
            }
        }
        
        Color GetNodeColor(ActivePath path, float normalizedPosition)
        {
            switch (colorMode)
            {
                case ColorMode.GlobalGradient:
                    return Color.Lerp(sourceColor, destinationColor, normalizedPosition);
                    
                case ColorMode.PerLegColors:
                    Color srcColor = endpointColors.ContainsKey(path.sourceEndpoint) 
                        ? endpointColors[path.sourceEndpoint] 
                        : sourceColor;
                    Color dstColor = endpointColors.ContainsKey(path.destinationEndpoint) 
                        ? endpointColors[path.destinationEndpoint] 
                        : destinationColor;
                    return Color.Lerp(srcColor, dstColor, normalizedPosition);
                    
                case ColorMode.Monochrome:
                    return path.pathColor;
                    
                default:
                    return path.pathColor;
            }
        }
        
        Color GetLeadingEdgeColor(ActivePath path)
        {
            switch (colorMode)
            {
                case ColorMode.PerLegColors:
                    // Use destination endpoint color as leading edge
                    return endpointColors.ContainsKey(path.destinationEndpoint) 
                        ? endpointColors[path.destinationEndpoint] 
                        : leadingEdgeColor;
                    
                default:
                    return leadingEdgeColor;
            }
        }
        
        void RenderChaseParticles(ActivePath path, float fillPosition, float time, Color[] colors)
        {
            int pathLength = path.pathNodes.Count;
            
            for (int p = 0; p < chaseParticleCount; p++)
            {
                float particleOffset = p * chaseSpacing * pathLength;
                float particlePos = (fillPosition * chaseSpeed - particleOffset);
                
                if (particlePos < 0 || particlePos >= pathLength) continue;
                
                for (int i = 0; i < pathLength; i++)
                {
                    float dist = Mathf.Abs(i - particlePos);
                    if (dist < leadingEdgeWidth / 2f)
                    {
                        float intensity = 1f - (dist / (leadingEdgeWidth / 2f));
                        int node = path.pathNodes[i];
                        colors[node] = Color.Lerp(colors[node], leadingEdgeColor, intensity * 0.8f);
                    }
                }
            }
        }
        
        /// <summary>
        /// Clean up audio and state when animation is disabled or changed
        /// </summary>
        public void CleanupAudio()
        {
            // Clean up all path audio
            foreach (var path in activePaths)
            {
                if (path.sourceAudio != null)
                {
                    path.sourceAudio.Stop();
                    Object.DestroyImmediate(path.sourceAudio.gameObject);
                }
                if (path.destAudio != null)
                {
                    path.destAudio.Stop();
                    Object.DestroyImmediate(path.destAudio.gameObject);
                }
            }
            activePaths.Clear();
            
            if (audioContainer != null)
            {
                Object.DestroyImmediate(audioContainer);
                audioContainer = null;
            }
        }
        
        private void OnDisable()
        {
            if (subscribedToEvents)
            {
                GraphPlayerController.OnEndpointPressed -= OnEndpointPressed;
                subscribedToEvents = false;
            }
            
            CleanupAudio();
            analysisComplete = false;
        }
        
        // Public API for simulator
        public void SimulateTouchEndpoint(int endpointIndex)
        {
            if (endpointNodes.Contains(endpointIndex))
            {
                Vector3 pos = cachedPositions != null && endpointIndex < cachedPositions.Count 
                    ? cachedPositions[endpointIndex] 
                    : Vector3.zero;
                var gm = Object.FindFirstObjectByType<LEDGraphManager>();
                if (gm == null)
                {
                    Debug.LogWarning("PathFinderAnimation.SimulateTouchEndpoint: no LEDGraphManager found in scene.");
                    return;
                }
                GraphPlayerController.SimulatePress(gm, endpointIndex, pos);
            }
        }
        
        public List<int> GetEndpointNodes() => new List<int>(endpointNodes);
        public int GetActivePathCount() => activePaths.Count;
        public bool IsEndpointActive(int endpoint) => IsEndpointInUse(endpoint);
        
        public List<int> GetWaitingDestinations()
        {
            return activePaths
                .Where(p => p.state == PathState.Waiting)
                .Select(p => p.destinationEndpoint)
                .ToList();
        }
    }
}
