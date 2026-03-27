using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace BranchingLEDAnimator.Animation
{
    /// <summary>
    /// Network flow animation that simulates particles flowing through the graph structure
    /// </summary>
    [CreateAssetMenu(fileName = "NetworkFlowAnimation", menuName = "LED Animations/Network Flow Animation")]
    public class NetworkFlowAnimation : LEDAnimationType
    {
        [Header("Flow Configuration")]
        [SerializeField] private FlowType flowType = FlowType.SourceToSinks;
        [SerializeField] private float particleLifetime = 4f;
        [SerializeField] private int maxParticles = 20;
        
        [Header("Flow Appearance")]
        [SerializeField] private Color sourceColor = Color.yellow;
        [SerializeField] private Color sinkColor = Color.magenta;
        [SerializeField] private AnimationCurve flowTrail = AnimationCurve.EaseInOut(0, 1, 1, 0);
        
        [Header("Network Analysis")]
        [SerializeField] private bool showNetworkRoles = true;
        [SerializeField] private bool bidirectionalFlow = false;
        
        public enum FlowType
        {
            SourceToSinks,      // From high valence to low valence
            SinksToSource,      // From low valence to high valence  
            Circulation,        // Circular flow patterns
            RandomWalk          // Random movement through network
        }
        
        // Flow particle class
        [System.Serializable]
        private class FlowParticle
        {
            public int currentNode;
            public int targetNode;
            public float progress;          // 0-1 along current edge
            public float lifetime;
            public float totalLifetime;
            public List<int> path;          // Nodes this particle has visited
            public Color color;
            public float intensity;
            
            public FlowParticle(int startNode, Color col, float maxLifetime)
            {
                currentNode = startNode;
                targetNode = -1;
                progress = 0f;
                lifetime = 0f;
                totalLifetime = maxLifetime;
                path = new List<int> { startNode };
                color = col;
                intensity = 1f;
            }
        }
        
        // Network analysis cache
        private List<int> sourceNodes = new List<int>();
        private List<int> sinkNodes = new List<int>();
        private List<int> hubNodes = new List<int>();
        private Dictionary<int, List<int>> nodeConnections = new Dictionary<int, List<int>>();
        private Dictionary<int, int> nodeValences = new Dictionary<int, int>();
        private bool analysisComplete = false;
        
        // Flow state
        private List<FlowParticle> activeParticles = new List<FlowParticle>();
        private float lastSpawnTime = 0f;
        private float spawnInterval = 0.5f;
        private float lastAnimTime = 0f; // Track time resets for looping
        
        // Track previous values to detect meaningful changes
        private FlowType lastFlowType;
        private bool lastBidirectionalFlow;
        private bool lastShowNetworkRoles;
        
        // Frame tracking to prevent multiple updates per frame
        #pragma warning disable CS0414
        private int lastUpdateFrame = -1; // Used in builds, not editor
        #pragma warning restore CS0414
        private float lastRealTime = 0f;
        
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
            
            // Perform network analysis if needed
            if (!analysisComplete)
            {
                AnalyzeNetworkStructure(nodePositions, edgeConnections);
            }
            
            // Show network roles if enabled
            if (showNetworkRoles)
            {
                ApplyNetworkRoleColors(colors);
            }
            
            // Handle looping time
            float animTime = time * speed;
            if (loop && duration > 0)
            {
                animTime = animTime % duration;
                
                // Only reset spawn timing on significant loop reset (not minor fluctuations)
                // Don't clear particles - let them continue naturally for smoother looping
                if (lastAnimTime - animTime > duration * 0.5f)
                {
                    // Actual loop occurred - just reset spawn timing, keep particles
                    lastSpawnTime = animTime;
                }
            }
            lastAnimTime = animTime;
            
            // Prevent multiple updates per frame (Scene view + Game view + Inspector)
            float currentRealTime = Time.realtimeSinceStartup;
            float actualDeltaTime = 0f;
            
            #if UNITY_EDITOR
            // BUGFIX: Reset lastRealTime if it's invalid (stale from previous session or out of range)
            if (lastRealTime > currentRealTime || lastRealTime < 0f || (currentRealTime - lastRealTime) > 10f)
            {
                lastRealTime = currentRealTime - 0.033f; // Pretend last update was ~30fps ago
            }
            
            // In editor, use real time to calculate delta (more reliable)
            if (currentRealTime > lastRealTime)
            {
                actualDeltaTime = Mathf.Min(currentRealTime - lastRealTime, 0.1f); // Cap at 100ms
                lastRealTime = currentRealTime;
            }
            #else
            // In build, use frame tracking
            int currentFrame = Time.frameCount;
            if (currentFrame != lastUpdateFrame)
            {
                actualDeltaTime = Time.deltaTime;
                lastUpdateFrame = currentFrame;
            }
            #endif
            
            // BUGFIX: Ensure we always have some delta time to prevent stuck simulations
            if (actualDeltaTime <= 0f)
            {
                actualDeltaTime = 0.033f; // Fallback to ~30fps
            }
            
            UpdateFlowSimulation(animTime, actualDeltaTime);
            
            // Apply particle effects (always - just reads state)
            ApplyParticleEffects(colors);
            
            return colors;
        }
        
        void AnalyzeNetworkStructure(List<Vector3> nodePositions, List<Vector2Int> edgeConnections)
        {
            // Clear previous analysis
            sourceNodes.Clear();
            sinkNodes.Clear();
            hubNodes.Clear();
            nodeConnections.Clear();
            nodeValences.Clear();
            
            // Initialize connections
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
                    // Add connections
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
            
            // Categorize nodes by network role
            foreach (var kvp in nodeValences)
            {
                int nodeIndex = kvp.Key;
                int valence = kvp.Value;
                
                if (valence >= 3)
                {
                    hubNodes.Add(nodeIndex);        // Junction points
                    sourceNodes.Add(nodeIndex);     // For flow purposes, treat high valence as sources
                }
                else if (valence == 1)
                {
                    sinkNodes.Add(nodeIndex);       // Endpoints
                }
            }
            
            analysisComplete = true;
        }
        
        void ApplyNetworkRoleColors(Color[] colors)
        {
            foreach (int source in sourceNodes)
            {
                if (source < colors.Length)
                    colors[source] = Color.Lerp(inactiveColor, sourceColor, 0.2f);
            }
            
            foreach (int sink in sinkNodes)
            {
                if (sink < colors.Length)
                    colors[sink] = Color.Lerp(inactiveColor, sinkColor, 0.2f);
            }
        }
        
        void UpdateFlowSimulation(float animTime, float deltaTime)
        {
            // Spawn new particles
            if (animTime - lastSpawnTime >= spawnInterval && activeParticles.Count < maxParticles)
            {
                SpawnNewParticle(animTime);
                lastSpawnTime = animTime;
            }
            
            // Update existing particles
            for (int i = activeParticles.Count - 1; i >= 0; i--)
            {
                var particle = activeParticles[i];
                particle.lifetime += deltaTime;
                
                // Remove expired particles
                if (particle.lifetime >= particle.totalLifetime)
                {
                    activeParticles.RemoveAt(i);
                    continue;
                }
                
                // Update particle movement
                UpdateParticleMovement(particle, deltaTime);
                
                // Update intensity based on lifetime
                float lifeProgress = particle.lifetime / particle.totalLifetime;
                particle.intensity = flowTrail.Evaluate(1f - lifeProgress);
            }
        }
        
        void SpawnNewParticle(float animTime)
        {
            List<int> spawnNodes = GetSpawnNodes();
            if (spawnNodes.Count == 0) return;
            
            // Spawn particle from random source
            int spawnNode = spawnNodes[Random.Range(0, spawnNodes.Count)];
            Color particleColor = GetFlowColorForNode(spawnNode);
            var particle = new FlowParticle(spawnNode, particleColor, particleLifetime);
            
            // Set initial target
            SetParticleTarget(particle);
            
            activeParticles.Add(particle);
        }
        
        List<int> GetSpawnNodes()
        {
            switch (flowType)
            {
                case FlowType.SourceToSinks:
                case FlowType.Circulation:
                    return sourceNodes;
                    
                case FlowType.SinksToSource:
                    return sinkNodes;
                    
                case FlowType.RandomWalk:
                    // Can spawn from any node
                    return nodeConnections.Keys.ToList();
                    
                default:
                    return sourceNodes;
            }
        }
        
        Color GetFlowColorForNode(int nodeIndex)
        {
            if (sourceNodes.Contains(nodeIndex))
                return Color.Lerp(primaryColor, sourceColor, 0.5f);
            else if (sinkNodes.Contains(nodeIndex))
                return Color.Lerp(primaryColor, sinkColor, 0.5f);
            else
                return primaryColor;
        }
        
        void SetParticleTarget(FlowParticle particle)
        {
            if (!nodeConnections.ContainsKey(particle.currentNode)) return;
            
            var connections = nodeConnections[particle.currentNode];
            if (connections.Count == 0) return;
            
            switch (flowType)
            {
                case FlowType.SourceToSinks:
                    // Move towards sink nodes preferentially
                    var sinkConnections = connections.Where(n => sinkNodes.Contains(n)).ToList();
                    if (sinkConnections.Count > 0)
                    {
                        particle.targetNode = sinkConnections[Random.Range(0, sinkConnections.Count)];
                    }
                    else
                    {
                        // Move towards lower valence nodes
                        var sortedByValence = connections.OrderBy(n => nodeValences[n]).ToList();
                        particle.targetNode = sortedByValence[0];
                    }
                    break;
                    
                case FlowType.SinksToSource:
                    // Move towards source nodes
                    var sourceConnections = connections.Where(n => sourceNodes.Contains(n)).ToList();
                    if (sourceConnections.Count > 0)
                    {
                        particle.targetNode = sourceConnections[Random.Range(0, sourceConnections.Count)];
                    }
                    else
                    {
                        // Move towards higher valence nodes
                        var sortedByValence = connections.OrderByDescending(n => nodeValences[n]).ToList();
                        particle.targetNode = sortedByValence[0];
                    }
                    break;
                    
                case FlowType.RandomWalk:
                    // Random movement
                    particle.targetNode = connections[Random.Range(0, connections.Count)];
                    break;
                    
                case FlowType.Circulation:
                    // Try to avoid going back
                    var validTargets = connections.Where(n => !particle.path.Contains(n) || particle.path.Count < 3).ToList();
                    if (validTargets.Count > 0)
                    {
                        particle.targetNode = validTargets[Random.Range(0, validTargets.Count)];
                    }
                    else
                    {
                        particle.targetNode = connections[Random.Range(0, connections.Count)];
                    }
                    break;
                    
                default:
                    particle.targetNode = connections[Random.Range(0, connections.Count)];
                    break;
            }
        }
        
        void UpdateParticleMovement(FlowParticle particle, float deltaTime)
        {
            if (particle.targetNode < 0) return;
            
            // Update progress along current edge
            // Note: speed is already applied to animation timing, so use a base rate here
            float edgeSpeed = 2f; // Base flow rate (particles traverse an edge in ~0.5 seconds)
            particle.progress += edgeSpeed * speed * deltaTime;
            
            // Check if particle reached target node
            if (particle.progress >= 1f)
            {
                // Move to target node
                particle.currentNode = particle.targetNode;
                particle.path.Add(particle.currentNode);
                particle.progress = 0f;
                
                // Set new target
                SetParticleTarget(particle);
            }
        }
        
        void ApplyParticleEffects(Color[] colors)
        {
            foreach (var particle in activeParticles)
            {
                // Get CURRENT color from settings (not stored color) so editor changes apply immediately
                Color liveColor = GetFlowColorForNode(particle.currentNode);
                
                // Light up current node
                if (particle.currentNode < colors.Length)
                {
                    Color particleColor = Color.Lerp(inactiveColor, liveColor, particle.intensity);
                    colors[particle.currentNode] = Color.Lerp(colors[particle.currentNode], particleColor, 0.8f);
                }
                
                // If moving between nodes, interpolate the effect
                if (particle.targetNode >= 0 && particle.targetNode < colors.Length && particle.progress > 0f)
                {
                    float targetIntensity = particle.intensity * particle.progress;
                    Color targetColor = Color.Lerp(inactiveColor, liveColor, targetIntensity);
                    colors[particle.targetNode] = Color.Lerp(colors[particle.targetNode], targetColor, 0.5f);
                }
                
                // Show trail effect
                if (particle.path.Count > 1)
                {
                    for (int i = particle.path.Count - 2; i >= Mathf.Max(0, particle.path.Count - 4); i--)
                    {
                        int trailNode = particle.path[i];
                        if (trailNode < colors.Length)
                        {
                            float trailIntensity = particle.intensity * 0.3f * (i / (float)particle.path.Count);
                            Color trailColor = Color.Lerp(inactiveColor, liveColor, trailIntensity);
                            colors[trailNode] = Color.Lerp(colors[trailNode], trailColor, 0.3f);
                        }
                    }
                }
            }
        }
        
        // Smart reset - only clear what's necessary based on what changed
        void OnValidate()
        {
            // Only reset network analysis if network-related settings changed
            if (lastBidirectionalFlow != bidirectionalFlow || lastShowNetworkRoles != showNetworkRoles)
            {
                analysisComplete = false;
                lastBidirectionalFlow = bidirectionalFlow;
                lastShowNetworkRoles = showNetworkRoles;
            }
            
            // Only clear particles if flow behavior changed significantly
            if (lastFlowType != flowType)
            {
                activeParticles.Clear();
                lastFlowType = flowType;
                Debug.Log($"🔄 Network Flow: Flow type changed to {flowType} - restarting particles");
            }
            
            // Note: Changes to colors, particleLifetime, maxParticles, etc. 
            // don't require clearing particles - they'll apply naturally
        }
        
        /// <summary>
        /// Manual reset if needed - accessible from context menu
        /// </summary>
        [ContextMenu("Reset Animation")]
        public void ResetAnimation()
        {
            analysisComplete = false;
            activeParticles.Clear();
            lastSpawnTime = 0f;
            lastAnimTime = 0f;
            lastRealTime = 0f;
            lastUpdateFrame = -1;
            Debug.Log("🔄 Network Flow: Manual reset - all state cleared");
        }
        
        /// <summary>
        /// Force spawn particles for debugging
        /// </summary>
        [ContextMenu("Debug: Force Spawn Particles")]
        public void DebugForceSpawn()
        {
            Debug.Log($"🔍 Network Flow Debug:");
            Debug.Log($"   - Analysis Complete: {analysisComplete}");
            Debug.Log($"   - Source Nodes: {sourceNodes.Count}");
            Debug.Log($"   - Sink Nodes: {sinkNodes.Count}");
            Debug.Log($"   - Hub Nodes: {hubNodes.Count}");
            Debug.Log($"   - Active Particles: {activeParticles.Count}");
            Debug.Log($"   - Max Particles: {maxParticles}");
            Debug.Log($"   - Last Spawn Time: {lastSpawnTime}");
            Debug.Log($"   - Last Anim Time: {lastAnimTime}");
            Debug.Log($"   - Last Real Time: {lastRealTime}");
            
            if (sourceNodes.Count == 0)
            {
                Debug.LogWarning("⚠️ No source nodes found! Network analysis may have failed.");
            }
            
            // Force spawn a few particles
            for (int i = 0; i < 5 && activeParticles.Count < maxParticles; i++)
            {
                SpawnNewParticle(Time.realtimeSinceStartup);
            }
            Debug.Log($"   - Particles after spawn: {activeParticles.Count}");
        }
    }
}
