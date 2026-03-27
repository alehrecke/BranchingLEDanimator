using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace BranchingLEDAnimator.Animation
{
    /// <summary>
    /// Breadth-first search animation that grows from non-valence-2 nodes sequentially,
    /// with each search using complementary colors and consuming the previous search
    /// </summary>
    [CreateAssetMenu(fileName = "BreadthFirstSearchAnimation", menuName = "LED Animations/Breadth First Search Animation")]
    public class BreadthFirstSearchAnimation : LEDAnimationType
    {
        [Header("Search Configuration")]
        [SerializeField] private float searchSpeed = 1f;
        [SerializeField] private float pauseBetweenSearches = 3f;
        [SerializeField] private float stepInterval = 0.5f; // Time between BFS steps
        [SerializeField] private bool showProgressiveFill = true;
        
        [Header("Duration Auto-Calculation")]
        [Tooltip("When enabled, duration is automatically calculated based on graph structure")]
        [SerializeField] private bool autoCalculateDuration = true;
        [Tooltip("Extra buffer time added to calculated duration (seconds)")]
        [SerializeField] private float durationBuffer = 5f;
        
        [Header("Timing Debug Info (Read-Only)")]
        [SerializeField] private int _seedNodeCount = 0;
        [SerializeField] private int _estimatedMaxDepth = 10;
        [SerializeField] private float _estimatedRuntime = 0f;
        [SerializeField] private string _timingBreakdown = "Not yet calculated";
        
        // Public read-only access for Inspector display
        public int SeedNodeCount => _seedNodeCount;
        public float EstimatedRuntime => _estimatedRuntime;
        public string TimingBreakdown => _timingBreakdown;
        
        [Header("Color Progression")]
        [SerializeField] private bool useComplementaryColors = true;
        [SerializeField] private float colorProgression = 0.618f; // Golden ratio for pleasing color distribution
        [SerializeField] private float searchSaturation = 0.9f;
        [SerializeField] private float searchBrightness = 0.9f;
        
        [Header("Visual Effects")]
        [SerializeField] private bool persistentBackground = true;
        [SerializeField] private float backgroundFadeRate = 0.5f;
        [SerializeField] private bool highlightCurrentFrontier = true;
        [SerializeField] private float frontierIntensity = 1.2f;
        
        // Search state class
        [System.Serializable]
        private class BFSSearchState
        {
            public int seedNode;
            public Queue<int> searchQueue;
            public HashSet<int> visited;
            public Dictionary<int, int> nodeDepths;
            public Color searchColor;
            public float startTime;
            public bool isComplete;
            public int currentDepth;
            public int maxDepth;
            public HashSet<int> currentFrontier; // Nodes being explored this step
            public float lastStepTime;
            
            public BFSSearchState(int seed, Color color, float time)
            {
                seedNode = seed;
                searchQueue = new Queue<int>();
                visited = new HashSet<int>();
                nodeDepths = new Dictionary<int, int>();
                currentFrontier = new HashSet<int>();
                searchColor = color;
                startTime = time;
                lastStepTime = time;
                isComplete = false;
                currentDepth = 0;
                maxDepth = 0;
                
                // Initialize with seed node
                searchQueue.Enqueue(seed);
                visited.Add(seed);
                nodeDepths[seed] = 0;
                currentFrontier.Add(seed);
            }
        }
        
        // Graph structure cache
        private Dictionary<int, List<int>> nodeConnections = new Dictionary<int, List<int>>();
        private Dictionary<int, int> nodeValences = new Dictionary<int, int>();
        private List<int> seedNodes = new List<int>(); // Nodes with valence != 2
        private bool analysisComplete = false;
        
        // Search progression state
        private List<BFSSearchState> activeSearches = new List<BFSSearchState>();
        private Dictionary<int, Color> nodeBackgroundColors = new Dictionary<int, Color>();
        private int currentSeedIndex = 0;
        private float lastSearchStartTime = 0f;
        private float lastAnimTime = 0f; // Track time resets for looping
        
        protected override Color[] CalculateBaseNodeColors(
            List<Vector3> nodePositions,
            List<Vector2Int> edgeConnections,
            List<int> sourceNodes,
            float time,
            int frame)
        {
            Color[] colors = new Color[nodePositions.Count];
            
            // Initialize with inactive color
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = inactiveColor;
            }
            
            // Perform graph analysis if needed
            if (!analysisComplete)
            {
                AnalyzeGraphStructure(nodePositions, edgeConnections);
            }
            
            if (seedNodes.Count == 0)
            {
                // No seed nodes found, just return inactive
                return colors;
            }
            
            // Handle looping time
            float animTime = time * speed;
            if (loop && duration > 0)
            {
                animTime = animTime % duration;
                
                // Detect loop reset and restart the entire search sequence
                if (animTime < lastAnimTime)
                {
                    Debug.Log("🔄 BFS Animation: Loop reset detected - restarting search sequence");
                    ResetSearchSequence();
                }
            }
            lastAnimTime = animTime;
            
            // Apply persistent background colors
            ApplyBackgroundColors(colors);
            
            // Update search progression
            UpdateSearchProgression(animTime);
            
            // Apply active search effects
            ApplySearchEffects(colors, animTime);
            
            // Highlight seed nodes that haven't been searched yet
            HighlightUpcomingSeedNodes(colors);
            
            // SAFETY: Clamp all colors to valid range before returning
            for (int i = 0; i < colors.Length; i++)
            {
                Color c = colors[i];
                colors[i] = new Color(
                    Mathf.Clamp01(c.r),
                    Mathf.Clamp01(c.g), 
                    Mathf.Clamp01(c.b),
                    1f
                );
            }
            
            return colors;
        }
        
        void AnalyzeGraphStructure(List<Vector3> nodePositions, List<Vector2Int> edgeConnections)
        {
            // Clear previous analysis
            nodeConnections.Clear();
            nodeValences.Clear();
            seedNodes.Clear();
            
            // Initialize structures
            for (int i = 0; i < nodePositions.Count; i++)
            {
                nodeConnections[i] = new List<int>();
                nodeValences[i] = 0;
            }
            
            // Build connection graph
            foreach (var edge in edgeConnections)
            {
                int nodeA = edge.x;
                int nodeB = edge.y;
                
                if (nodeA < nodePositions.Count && nodeB < nodePositions.Count)
                {
                    // Add bidirectional connections
                    if (!nodeConnections[nodeA].Contains(nodeB))
                    {
                        nodeConnections[nodeA].Add(nodeB);
                        nodeValences[nodeA]++;
                    }
                    
                    if (!nodeConnections[nodeB].Contains(nodeA))
                    {
                        nodeConnections[nodeB].Add(nodeA);
                        nodeValences[nodeB]++;
                    }
                }
            }
            
            // Identify seed nodes (nodes with valence != 2)
            foreach (var kvp in nodeValences)
            {
                int nodeIndex = kvp.Key;
                int valence = kvp.Value;
                
                if (valence != 2)
                {
                    seedNodes.Add(nodeIndex);
                    Debug.Log($"🌱 Found seed node {nodeIndex} with valence {valence}");
                }
            }
            
            // Sort seed nodes for consistent order
            seedNodes.Sort();
            
            Debug.Log($"🌱 BFS Animation: Found {seedNodes.Count} seed nodes (valence != 2) out of {nodePositions.Count} total nodes");
            if (seedNodes.Count == 0)
            {
                Debug.LogWarning("⚠️ No seed nodes found! All nodes have valence = 2. BFS animation will not run.");
                Debug.Log("💡 Tip: BFS animation looks for nodes with valence != 2 (junctions, endpoints, etc.)");
            }
            
            // Calculate estimated duration
            CalculateEstimatedDuration(nodePositions.Count);
            
            analysisComplete = true;
        }
        
        /// <summary>
        /// Calculate and optionally set the duration based on graph structure
        /// </summary>
        void CalculateEstimatedDuration(int totalNodes)
        {
            _seedNodeCount = seedNodes.Count;
            
            if (_seedNodeCount == 0)
            {
                _estimatedRuntime = 0f;
                _timingBreakdown = "No seed nodes found";
                return;
            }
            
            // Estimate average depth per search
            // Assume each search covers roughly (totalNodes / seedNodeCount) nodes
            int nodesPerSearch = totalNodes / Mathf.Max(1, _seedNodeCount);
            
            // Estimate max depth based on nodes per search (BFS depth is roughly sqrt of covered nodes)
            _estimatedMaxDepth = Mathf.Max(3, Mathf.CeilToInt(Mathf.Sqrt(nodesPerSearch) * 1.5f));
            
            // Calculate time per search: steps × stepInterval
            float timePerSearch = _estimatedMaxDepth * stepInterval;
            
            // Total time: (searches × timePerSearch) + (pauses between searches)
            float totalSearchTime = _seedNodeCount * timePerSearch;
            float totalPauseTime = Mathf.Max(0, _seedNodeCount - 1) * pauseBetweenSearches;
            
            _estimatedRuntime = totalSearchTime + totalPauseTime + durationBuffer;
            
            // Build timing breakdown string
            _timingBreakdown = $"{_seedNodeCount} seeds × ({_estimatedMaxDepth} steps × {stepInterval}s + {pauseBetweenSearches}s pause) + {durationBuffer}s buffer";
            
            Debug.Log($"⏱️ BFS Duration Calculation:");
            Debug.Log($"   Seed nodes: {_seedNodeCount}");
            Debug.Log($"   Est. max depth: {_estimatedMaxDepth}");
            Debug.Log($"   Time per search: {timePerSearch:F1}s");
            Debug.Log($"   Total pause time: {totalPauseTime:F1}s");
            Debug.Log($"   Estimated runtime: {_estimatedRuntime:F1}s");
            
            // Auto-set duration if enabled
            if (autoCalculateDuration)
            {
                duration = _estimatedRuntime;
                Debug.Log($"✅ Duration auto-set to: {duration:F1}s");
            }
            else
            {
                Debug.Log($"ℹ️ Auto-calculate disabled. Current duration: {duration:F1}s (estimated: {_estimatedRuntime:F1}s)");
                if (duration < _estimatedRuntime)
                {
                    Debug.LogWarning($"⚠️ Duration ({duration:F1}s) is less than estimated runtime ({_estimatedRuntime:F1}s). Animation may loop before completing!");
                }
            }
        }
        
        void ResetSearchSequence()
        {
            activeSearches.Clear();
            nodeBackgroundColors.Clear();
            currentSeedIndex = 0;
            lastSearchStartTime = 0f;
        }
        
        void UpdateSearchProgression(float animTime)
        {
            // Check if we should start a new search
            bool shouldStartNewSearch = false;
            
            if (activeSearches.Count == 0)
            {
                // Start the first search
                shouldStartNewSearch = true;
            }
            else
            {
                // Check if current search is complete and enough time has passed
                var currentSearch = activeSearches.LastOrDefault();
                if (currentSearch != null && currentSearch.isComplete)
                {
                    float timeSinceCompletion = animTime - (currentSearch.startTime + GetSearchDuration(currentSearch));
                    if (timeSinceCompletion >= pauseBetweenSearches)
                    {
                        shouldStartNewSearch = true;
                    }
                }
            }
            
            // Start new search if needed
            if (shouldStartNewSearch && currentSeedIndex < seedNodes.Count)
            {
                StartNewSearch(animTime);
            }
            
            // Update active searches
            foreach (var search in activeSearches)
            {
                if (!search.isComplete)
                {
                    UpdateBFSSearch(search, animTime);
                }
            }
            
            // Check if all searches are complete (for looping)
            if (currentSeedIndex >= seedNodes.Count && activeSearches.All(s => s.isComplete))
            {
                // All searches complete - in a looping animation, this will restart
                if (loop)
                {
                    // Let the natural loop reset handle restart
                }
            }
        }
        
        void StartNewSearch(float animTime)
        {
            if (currentSeedIndex >= seedNodes.Count) return;
            
            int seedNode = seedNodes[currentSeedIndex];
            Color searchColor = GenerateSearchColor(currentSeedIndex);
            
            var newSearch = new BFSSearchState(seedNode, searchColor, animTime);
            activeSearches.Add(newSearch);
            
            Debug.Log($"🔍 BFS Animation: Starting search #{currentSeedIndex + 1} from node {seedNode} with color {searchColor}");
            
            currentSeedIndex++;
            lastSearchStartTime = animTime;
        }
        
        Color GenerateSearchColor(int searchIndex)
        {
            Color color;
            
            if (useComplementaryColors)
            {
                // Use golden ratio to generate pleasing color progression
                float hue = (searchIndex * colorProgression) % 1f;
                color = Color.HSVToRGB(hue, searchSaturation, searchBrightness);
            }
            else
            {
                // Alternate between primary and secondary colors with variations
                float variation = searchIndex * 0.1f;
                color = searchIndex % 2 == 0 ? 
                    Color.Lerp(primaryColor, Color.white, variation % 0.3f) :
                    Color.Lerp(secondaryColor, Color.white, variation % 0.3f);
            }
            
            // SAFETY: Clamp color values to valid range [0, 1]
            color.r = Mathf.Clamp01(color.r);
            color.g = Mathf.Clamp01(color.g);
            color.b = Mathf.Clamp01(color.b);
            color.a = 1f;
            
            return color;
        }
        
        void UpdateBFSSearch(BFSSearchState search, float animTime)
        {
            if (search.isComplete) return;
            
            // Check if it's time for the next BFS step
            float timeSinceLastStep = animTime - search.lastStepTime;
            if (timeSinceLastStep >= stepInterval)
            {
                // Perform the next BFS step
                if (search.searchQueue.Count > 0)
                {
                    PerformBFSStep(search);
                    search.lastStepTime = animTime;
                }
                else
                {
                    // Search is complete
                    search.isComplete = true;
                    
                    // Commit this search's colors to the background
                    foreach (var kvp in search.nodeDepths)
                    {
                        nodeBackgroundColors[kvp.Key] = search.searchColor;
                    }
                    
                    Debug.Log($"✅ BFS Animation: Search from node {search.seedNode} completed, visited {search.visited.Count} nodes");
                }
            }
        }
        
        void PerformBFSStep(BFSSearchState search)
        {
            if (search.searchQueue.Count == 0) return;
            
            // Clear the current frontier - we're starting a new step
            search.currentFrontier.Clear();
            
            // Process all nodes at the current depth level (breadth-first!)
            int nodesAtCurrentDepth = search.searchQueue.Count;
            
            for (int i = 0; i < nodesAtCurrentDepth; i++)
            {
                if (search.searchQueue.Count == 0) break;
                
                int currentNode = search.searchQueue.Dequeue();
                int currentDepth = search.nodeDepths[currentNode];
                
                // Update the maximum depth reached
                search.maxDepth = Mathf.Max(search.maxDepth, currentDepth);
                search.currentDepth = currentDepth;
                
                // Explore neighbors
                if (nodeConnections.ContainsKey(currentNode))
                {
                    foreach (int neighbor in nodeConnections[currentNode])
                    {
                        if (!search.visited.Contains(neighbor))
                        {
                            search.visited.Add(neighbor);
                            search.nodeDepths[neighbor] = currentDepth + 1;
                            search.searchQueue.Enqueue(neighbor);
                            search.currentFrontier.Add(neighbor); // Add to frontier for highlighting
                        }
                    }
                }
            }
            
            Debug.Log($"🔍 BFS Step: Depth {search.currentDepth}, added {search.currentFrontier.Count} new nodes to frontier");
        }
        
        float GetSearchDuration(BFSSearchState search)
        {
            // Estimate total search duration based on maximum depth
            return (search.maxDepth + 1) / (searchSpeed * 2f);
        }
        
        void ApplyBackgroundColors(Color[] colors)
        {
            if (!persistentBackground) return;
            
            foreach (var kvp in nodeBackgroundColors)
            {
                int nodeIndex = kvp.Key;
                Color bgColor = kvp.Value;
                
                if (nodeIndex < colors.Length)
                {
                    colors[nodeIndex] = Color.Lerp(inactiveColor, bgColor, backgroundFadeRate);
                }
            }
        }
        
        void ApplySearchEffects(Color[] colors, float animTime)
        {
            foreach (var search in activeSearches)
            {
                if (search.visited.Count == 0) continue;
                
                // Progressive fill: show all visited nodes in the search color
                foreach (var kvp in search.nodeDepths)
                {
                    int nodeIndex = kvp.Key;
                    
                    if (nodeIndex >= colors.Length) continue;
                    
                    float intensity = 0.8f; // Base intensity for visited nodes
                    
                    // Highlight current frontier if enabled
                    if (highlightCurrentFrontier && search.currentFrontier.Contains(nodeIndex))
                    {
                        intensity = frontierIntensity; // Brighter for frontier nodes
                    }
                    
                    Color searchEffect = Color.Lerp(inactiveColor, search.searchColor, intensity);
                    
                    // Use stronger blending for active searches
                    colors[nodeIndex] = Color.Lerp(colors[nodeIndex], searchEffect, 0.9f);
                }
                
                // Always highlight the seed node prominently
                if (search.seedNode < colors.Length)
                {
                    colors[search.seedNode] = search.searchColor;
                }
            }
        }
        
        void HighlightUpcomingSeedNodes(Color[] colors)
        {
            // Highlight seed nodes that haven't been searched yet
            for (int i = currentSeedIndex; i < seedNodes.Count; i++)
            {
                int seedNode = seedNodes[i];
                if (seedNode < colors.Length && !nodeBackgroundColors.ContainsKey(seedNode))
                {
                    // Subtle highlight for upcoming seed nodes
                    Color highlight = Color.Lerp(inactiveColor, Color.white, 0.1f);
                    colors[seedNode] = Color.Lerp(colors[seedNode], highlight, 0.3f);
                }
            }
        }
        
        // Reset analysis when parameters change
        void OnValidate()
        {
            analysisComplete = false;
            ResetSearchSequence();
            
            // Recalculate duration estimate if we have cached seed node count
            if (_seedNodeCount > 0)
            {
                RecalculateDurationFromCache();
            }
        }
        
        /// <summary>
        /// Recalculate duration from cached values (used when settings change in Inspector)
        /// </summary>
        void RecalculateDurationFromCache()
        {
            if (_seedNodeCount == 0) return;
            
            // Use cached values to recalculate
            float timePerSearch = _estimatedMaxDepth * stepInterval;
            float totalSearchTime = _seedNodeCount * timePerSearch;
            float totalPauseTime = Mathf.Max(0, _seedNodeCount - 1) * pauseBetweenSearches;
            
            _estimatedRuntime = totalSearchTime + totalPauseTime + durationBuffer;
            _timingBreakdown = $"{_seedNodeCount} seeds × ({_estimatedMaxDepth} steps × {stepInterval}s + {pauseBetweenSearches}s pause) + {durationBuffer}s buffer";
            
            if (autoCalculateDuration)
            {
                duration = _estimatedRuntime;
            }
        }
        
        /// <summary>
        /// Force recalculation of duration (can be called from context menu)
        /// </summary>
        [ContextMenu("Recalculate Duration")]
        public void ForceRecalculateDuration()
        {
            if (_seedNodeCount > 0)
            {
                RecalculateDurationFromCache();
                Debug.Log($"🔄 Duration recalculated: {_estimatedRuntime:F1}s");
            }
            else
            {
                Debug.Log("⚠️ No graph data cached. Play the animation once to analyze the graph.");
            }
        }
    }
}
