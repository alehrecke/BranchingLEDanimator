using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using BranchingLEDAnimator.Core;

namespace BranchingLEDAnimator.Hardware
{
    /// <summary>
    /// Maps Unity LED graph nodes to physical LED strip addresses
    /// Handles branching geometry for single ESP32 with multiple strips
    /// </summary>
    public class LEDCircuitMapper : MonoBehaviour
    {
        [Header("Legacy Settings (for reference only)")]
        [SerializeField] private bool autoMapOnStart = false; // Disabled - use manual mapping workflow
        
        public enum MappingStrategy
        {
            Sample_Every_N_Nodes,    // Sample every N nodes from geometry
            Use_Branch_Endpoints,    // Use branch endpoints and key nodes
            Distribute_Evenly        // Distribute LEDs evenly across geometry
        }
        
        [Header("Strip Assignment")]
        [SerializeField] private List<LEDStripInfo> ledStrips = new List<LEDStripInfo>();
        
        [Header("Debug")]
        public bool showDebugInfo = true;
        
        // Mapping data
        private Dictionary<int, LEDAddress> nodeToLEDMapping = new Dictionary<int, LEDAddress>();
        private Dictionary<LEDAddress, int> ledToNodeMapping = new Dictionary<LEDAddress, int>();
        [SerializeField] private bool mappingComplete = false; // Serialize this so it persists in Edit mode
        
        // References
        private LEDGraphManager graphManager;
        
        void Start()
        {
            graphManager = GetComponent<LEDGraphManager>();
            
            if (autoMapOnStart && graphManager != null && graphManager.DataLoaded)
            {
                CreateLEDMapping();
            }
            
            // Subscribe to geometry updates
            LEDVisualizationEvents.OnGeometryUpdated += OnGeometryUpdated;
        }
        
        void OnDestroy()
        {
            LEDVisualizationEvents.OnGeometryUpdated -= OnGeometryUpdated;
        }
        
        private void OnGeometryUpdated(List<Vector3> nodePositions, List<Vector2Int> edgeConnections, List<int> sourceNodes)
        {
            if (autoMapOnStart)
            {
                CreateLEDMapping();
            }
        }
        
        /// <summary>
        /// SAFE: Only populate dictionary from existing strip configuration (preserves manual settings)
        /// </summary>
        [ContextMenu("Populate Dictionary Only")]
        public void PopulateDictionaryOnly()
        {
            if (showDebugInfo)
            {
                // Populating dictionary from existing strip configuration
                Debug.Log("📋 This will NOT clear your manual strip settings!");
            }
            
            // Only clear dictionaries, preserve strip configuration
            nodeToLEDMapping.Clear();
            ledToNodeMapping.Clear();
            
            // Populate dictionary from existing strips
            PopulateNodeToLEDMapping();
            
            mappingComplete = true;
            
            if (showDebugInfo)
            {
                Debug.Log($"✅ Dictionary populated: {nodeToLEDMapping.Count} mappings");
                Debug.Log("📋 Your manual strip settings were preserved!");
            }
        }

        /// <summary>
        /// Create LED strip mapping for continuous strip with 33-LED segments
        /// WARNING: This will clear all manual strip configurations!
        /// </summary>
        [ContextMenu("Create LED Mapping (CLEARS SETTINGS)")]
        public void CreateLEDMapping()
        {
            // Refresh component reference in case it was added later
            if (graphManager == null)
            {
                graphManager = GetComponent<LEDGraphManager>();
            }
            
            if (graphManager == null)
            {
                Debug.LogError("LEDCircuitMapper: LEDGraphManager component not found on same GameObject!");
                return;
            }
            
            if (!graphManager.DataLoaded)
            {
                Debug.LogError($"LEDCircuitMapper: No graph data loaded in LEDGraphManager! (Node count: {graphManager.NodeCount})");
                return;
            }
            
            if (showDebugInfo)
            {
                Debug.Log("🔌 CREATING GEOMETRY-TO-LED MAPPING...");
                Debug.Log($"📊 Geometry: {graphManager.NodeCount} nodes → {totalPhysicalLEDs} physical LEDs");
                Debug.Log($"📏 Configuration: {numLogicalStrips} strips × {ledsPerStrip} LEDs = {totalPhysicalLEDs} total");
                Debug.Log($"🎯 Strategy: {mappingStrategy}");
            }
            
            // Clear existing mapping
            ledStrips.Clear();
            nodeToLEDMapping.Clear();
            ledToNodeMapping.Clear();
            
            // Create mapping based on strategy
            CreateGeometryToLEDMapping();
            
            mappingComplete = true;
            
            // Output mapping summary
            LogMappingSummary();
        }
        
        /// <summary>
        /// Create mapping from Grasshopper geometry to exact physical LED count
        /// </summary>
        private void CreateGeometryToLEDMapping()
        {
            var nodePositions = graphManager.NodePositions;
            var edgeConnections = graphManager.EdgeConnections;
            var sourceNodes = graphManager.SourceNodes;
            
            Debug.Log($"🌿 Analyzing graph: {nodePositions.Count} nodes, {sourceNodes.Count} endpoints");
            
            // Get selected nodes based on strategy
            List<int> selectedNodes = SelectNodesForLEDs(nodePositions, edgeConnections, sourceNodes);
            
            Debug.Log($"📍 Selected {selectedNodes.Count} nodes for {totalPhysicalLEDs} LEDs using {mappingStrategy} strategy");
            Debug.Log($"🔢 Total available nodes from Grasshopper: {nodePositions.Count}");
            
            // Distribute selected nodes across strips
            DistributeNodesAcrossStrips(selectedNodes);
            
            // CRITICAL FIX: Ensure nodeToLEDMapping is populated after strip creation
            PopulateNodeToLEDMapping();
        }
        
        /// <summary>
        /// CRITICAL FIX: Populate nodeToLEDMapping dictionary from existing strip data
        /// This ensures GetLEDAddress() works correctly for SINGLE CONTINUOUS STRIP
        /// </summary>
        private void PopulateNodeToLEDMapping()
        {
            // Clear existing mappings to avoid conflicts
            nodeToLEDMapping.Clear();
            ledToNodeMapping.Clear();
            
            if (showDebugInfo)
            {
                // Populating node-to-LED mapping dictionary
            }
            
            int totalMappedNodes = 0;
            
            // Iterate through all LED strips and populate the mapping dictionary
            for (int stripIndex = 0; stripIndex < ledStrips.Count; stripIndex++)
            {
                var strip = ledStrips[stripIndex];
                if (strip.nodeIndices == null) continue;
                
                for (int ledIndex = 0; ledIndex < strip.nodeIndices.Count; ledIndex++)
                {
                    int nodeIndex = strip.nodeIndices[ledIndex];
                    if (nodeIndex >= 0) // Valid node (not -1)
                    {
                        // Calculate continuous LED address across all strips
                        int continuousLEDAddress = 0;
                        for (int prevStrip = 0; prevStrip < stripIndex; prevStrip++)
                        {
                            continuousLEDAddress += ledStrips[prevStrip].maxLEDsPerBranch;
                        }
                        continuousLEDAddress += ledIndex;
                        
                        // For SINGLE CONTINUOUS STRIP, all LEDs are on "strip 0" with continuous addressing
                        var ledAddress = new LEDAddress(0, continuousLEDAddress);
                        nodeToLEDMapping[nodeIndex] = ledAddress;
                        ledToNodeMapping[ledAddress] = nodeIndex;
                        totalMappedNodes++;
                    }
                }
            }
            
            if (showDebugInfo)
            {
                Debug.Log($"✅ Populated mapping dictionary: {totalMappedNodes} nodes mapped to LEDs");
                Debug.Log($"📊 Dictionary size: {nodeToLEDMapping.Count} node→LED, {ledToNodeMapping.Count} LED→node");
                
                // Debug first few mappings
                int debugCount = 0;
                foreach (var kvp in nodeToLEDMapping)
                {
                    if (debugCount < 5)
                    {
                        Debug.Log($"  Node {kvp.Key} → Strip {kvp.Value.stripIndex}, LED {kvp.Value.ledIndex}");
                        debugCount++;
                    }
                    else break;
                }
            }
        }
        
        /// <summary>
        /// Select which nodes from the geometry will control LEDs
        /// </summary>
        private List<int> SelectNodesForLEDs(List<Vector3> nodePositions, List<Vector2Int> edgeConnections, List<int> sourceNodes)
        {
            List<int> selectedNodes = new List<int>();
            
            switch (mappingStrategy)
            {
                case MappingStrategy.Sample_Every_N_Nodes:
                    // Sample every N nodes to get exactly totalPhysicalLEDs
                    float sampleStep = (float)nodePositions.Count / totalPhysicalLEDs;
                    for (int i = 0; i < totalPhysicalLEDs; i++)
                    {
                        int nodeIndex = Mathf.RoundToInt(i * sampleStep);
                        if (nodeIndex < nodePositions.Count)
                        {
                            selectedNodes.Add(nodeIndex);
                        }
                    }
                    break;
                    
                case MappingStrategy.Use_Branch_Endpoints:
                    // Find branches and use key nodes
                    var branches = FindBranches();
                    foreach (var branch in branches)
                    {
                        // Take nodes evenly distributed along each branch
                        int nodesPerBranch = totalPhysicalLEDs / branches.Count;
                        if (branch.Count > 0)
                        {
                            float branchStep = (float)branch.Count / nodesPerBranch;
                            for (int i = 0; i < nodesPerBranch && selectedNodes.Count < totalPhysicalLEDs; i++)
                            {
                                int nodeIndex = Mathf.RoundToInt(i * branchStep);
                                if (nodeIndex < branch.Count)
                                {
                                    selectedNodes.Add(branch[nodeIndex]);
                                }
                            }
                        }
                    }
                    break;
                    
                case MappingStrategy.Distribute_Evenly:
                default:
                    // Distribute LEDs evenly across all nodes
                    if (nodePositions.Count > 0)
                    {
                        float distributionStep = (float)nodePositions.Count / totalPhysicalLEDs;
                        for (int i = 0; i < totalPhysicalLEDs; i++)
                        {
                            int nodeIndex = Mathf.RoundToInt(i * distributionStep);
                            if (nodeIndex < nodePositions.Count)
                            {
                                selectedNodes.Add(nodeIndex);
                            }
                        }
                    }
                    break;
            }
            
            // Ensure we have exactly the right number of nodes
            while (selectedNodes.Count < totalPhysicalLEDs && selectedNodes.Count < nodePositions.Count)
            {
                // Add missing nodes
                for (int i = 0; i < nodePositions.Count && selectedNodes.Count < totalPhysicalLEDs; i++)
                {
                    if (!selectedNodes.Contains(i))
                    {
                        selectedNodes.Add(i);
                    }
                }
            }
            
            // Trim if we have too many
            if (selectedNodes.Count > totalPhysicalLEDs)
            {
                selectedNodes = selectedNodes.Take(totalPhysicalLEDs).ToList();
            }
            
            return selectedNodes;
        }
        
        /// <summary>
        /// Distribute selected nodes across the LED strips
        /// </summary>
        private void DistributeNodesAcrossStrips(List<int> selectedNodes)
        {
            for (int stripIndex = 0; stripIndex < numLogicalStrips; stripIndex++)
            {
                var stripInfo = new LEDStripInfo
                {
                    stripIndex = stripIndex,
                    dataPin = 19, // GPIO 19 - continuous strip on single pin
                    ledCount = ledsPerStrip,
                    nodeIndices = new List<int>()
                };
                stripInfo.GenerateRandomColor();
                
                // Assign nodes to this strip
                int startIndex = stripIndex * ledsPerStrip;
                int endIndex = Mathf.Min(startIndex + ledsPerStrip, selectedNodes.Count);
                
                for (int i = startIndex; i < endIndex; i++)
                {
                    stripInfo.nodeIndices.Add(selectedNodes[i]);
                }
                
                // Fill remaining slots with -1 (unmapped)
                while (stripInfo.nodeIndices.Count < ledsPerStrip)
                {
                    stripInfo.nodeIndices.Add(-1);
                }
                
                ledStrips.Add(stripInfo);
                
                // Create node → LED mapping
                for (int ledIndex = 0; ledIndex < stripInfo.nodeIndices.Count; ledIndex++)
                {
                    int nodeIndex = stripInfo.nodeIndices[ledIndex];
                    if (nodeIndex >= 0) // Valid node
                    {
                        var ledAddress = new LEDAddress(stripIndex, ledIndex);
                        nodeToLEDMapping[nodeIndex] = ledAddress;
                        ledToNodeMapping[ledAddress] = nodeIndex;
                    }
                }
                
                if (showDebugInfo)
                {
                    int validNodes = stripInfo.nodeIndices.Count(n => n >= 0);
                    int ledStartIndex = stripIndex * ledsPerStrip;
                    int ledEndIndex = ledStartIndex + ledsPerStrip - 1;
                    Debug.Log($"✓ Logical Strip {stripIndex}: GPIO {stripInfo.dataPin}, LEDs {ledStartIndex}-{ledEndIndex}, {validNodes}/{ledsPerStrip} nodes mapped");
                }
            }
        }
        
        /// <summary>
        /// Create mapping for logical strips (matches your ESP32 code)
        /// </summary>
        private void CreateContinuousStripMapping()
        {
            var nodePositions = graphManager.NodePositions;
            var edgeConnections = graphManager.EdgeConnections;
            var sourceNodes = graphManager.SourceNodes;
            
            Debug.Log($"🌿 Analyzing graph: {nodePositions.Count} nodes, {sourceNodes.Count} endpoints");
            Debug.Log($"📦 Creating {numLogicalStrips} logical strips of {ledsPerStrip} LEDs each (total: {totalPhysicalLEDs})");
            
            // Find the branch structure for logical strip assignment
            var branches = FindBranches();
            
            if (branches.Count > numLogicalStrips)
            {
                Debug.LogWarning($"⚠️ Graph has {branches.Count} branches but ESP32 only supports {numLogicalStrips} logical strips!");
            }
            
            // Create logical strips (matches your ESP32 expectation)
            for (int stripIndex = 0; stripIndex < numLogicalStrips; stripIndex++)
            {
                var stripInfo = new LEDStripInfo
                {
                    stripIndex = stripIndex,
                    dataPin = 19, // Your GPIO pin (same for all logical strips)
                    ledCount = ledsPerStrip,
                    nodeIndices = new List<int>()
                };
                stripInfo.GenerateRandomColor();
                
                // Map branch to logical strip
                if (stripIndex < branches.Count)
                {
                    var branch = branches[stripIndex];
                    Debug.Log($"🔗 Mapping Branch {stripIndex}: {branch.Count} nodes → Logical Strip {stripIndex}");
                    
                    // Map nodes to logical strip positions
                    for (int nodeIdx = 0; nodeIdx < branch.Count && nodeIdx < ledsPerStrip; nodeIdx++)
                    {
                        int unityNodeIndex = branch[nodeIdx];
                        
                        var ledAddress = new LEDAddress(stripIndex, nodeIdx);
                        
                        nodeToLEDMapping[unityNodeIndex] = ledAddress;
                        ledToNodeMapping[ledAddress] = unityNodeIndex;
                        stripInfo.nodeIndices.Add(unityNodeIndex);
                    }
                    
                    // Pad with black LEDs if branch is shorter
                    if (branch.Count < ledsPerStrip)
                    {
                        Debug.Log($"  - Branch {stripIndex} has only {branch.Count} nodes, padding logical strip with {ledsPerStrip - branch.Count} black LEDs");
                    }
                }
                else
                {
                    Debug.Log($"🔗 Logical Strip {stripIndex}: No branch assigned (will be black)");
                }
                
                ledStrips.Add(stripInfo);
            }
            
            Debug.Log($"✅ Logical strip mapping complete:");
            Debug.Log($"  - Total Unity nodes mapped: {nodeToLEDMapping.Count}");
            Debug.Log($"  - Logical strips created: {ledStrips.Count}");
            Debug.Log($"  - GPIO Pin: 19 (shared)");
            Debug.Log($"  - Physical mapping: Strip 0 = LEDs 0-19, Strip 1 = LEDs 20-39, Strip 2 = LEDs 40-59");
        }
        
        /// <summary>
        /// Find all branches in the graph (paths from branch point to endpoints)
        /// </summary>
        private List<List<int>> FindBranches()
        {
            var nodePositions = graphManager.NodePositions;
            var edgeConnections = graphManager.EdgeConnections;
            var sourceNodes = graphManager.SourceNodes;
            
            // Build adjacency list
            var adjacency = new Dictionary<int, List<int>>();
            for (int i = 0; i < nodePositions.Count; i++)
            {
                adjacency[i] = new List<int>();
            }
            
            foreach (var edge in edgeConnections)
            {
                adjacency[edge.x].Add(edge.y);
                adjacency[edge.y].Add(edge.x);
            }
            
            // Find branch point (node with 3+ connections)
            int branchPoint = -1;
            foreach (var kvp in adjacency)
            {
                if (kvp.Value.Count > 2)
                {
                    branchPoint = kvp.Key;
                    break;
                }
            }
            
            if (branchPoint == -1)
            {
                Debug.LogWarning("No branch point found - treating as single linear strip");
                // Single path from first to last source node
                var singleBranch = FindPath(sourceNodes[0], sourceNodes[sourceNodes.Count - 1], adjacency);
                return new List<List<int>> { singleBranch };
            }
            
            // Find paths from branch point to each endpoint
            var branches = new List<List<int>>();
            
            foreach (int endpoint in sourceNodes)
            {
                var path = FindPath(branchPoint, endpoint, adjacency);
                if (path.Count > 0)
                {
                    branches.Add(path);
                }
            }
            
            if (showDebugInfo)
            {
                Debug.Log($"✓ Found {branches.Count} branches from node {branchPoint}");
                for (int i = 0; i < branches.Count; i++)
                {
                    Debug.Log($"  Branch {i}: {branches[i].Count} LEDs (nodes {branches[i][0]} → {branches[i][branches[i].Count - 1]})");
                }
            }
            
            return branches;
        }
        
        /// <summary>
        /// Find path between two nodes using BFS
        /// </summary>
        private List<int> FindPath(int start, int end, Dictionary<int, List<int>> adjacency)
        {
            var visited = new HashSet<int>();
            var queue = new Queue<List<int>>();
            queue.Enqueue(new List<int> { start });
            
            while (queue.Count > 0)
            {
                var path = queue.Dequeue();
                var current = path[path.Count - 1];
                
                if (current == end)
                {
                    return path;
                }
                
                if (visited.Contains(current)) continue;
                visited.Add(current);
                
                foreach (int neighbor in adjacency[current])
                {
                    if (!visited.Contains(neighbor))
                    {
                        var newPath = new List<int>(path);
                        newPath.Add(neighbor);
                        queue.Enqueue(newPath);
                    }
                }
            }
            
            return new List<int>(); // No path found
        }
        
        /// <summary>
        /// Create LED strip mapping for a branch
        /// </summary>
        private void CreateLEDStripForBranch(int stripIndex, List<int> branchNodes)
        {
            if (branchNodes.Count > ledsPerStrip)
            {
                Debug.LogWarning($"Branch {stripIndex} has {branchNodes.Count} nodes, exceeding max {ledsPerStrip} per strip!");
            }
            
            var stripInfo = new LEDStripInfo
            {
                stripIndex = stripIndex,
                dataPin = stripIndex + 2, // ESP32 pins 2, 4, 5, etc.
                ledCount = branchNodes.Count,
                nodeIndices = new List<int>(branchNodes)
            };
            stripInfo.GenerateRandomColor();
            
            ledStrips.Add(stripInfo);
            
            // Create node → LED address mapping
            for (int i = 0; i < branchNodes.Count; i++)
            {
                int nodeIndex = branchNodes[i];
                var ledAddress = new LEDAddress(stripIndex, i);
                
                nodeToLEDMapping[nodeIndex] = ledAddress;
                ledToNodeMapping[ledAddress] = nodeIndex;
            }
            
            if (showDebugInfo)
            {
                Debug.Log($"✓ Created LED Strip {stripIndex}: Pin {stripInfo.dataPin}, {stripInfo.ledCount} LEDs");
            }
        }
        
        /// <summary>
        /// Get LED address for a Unity node index
        /// </summary>
        public LEDAddress GetLEDAddress(int nodeIndex)
        {
            if (nodeToLEDMapping.TryGetValue(nodeIndex, out LEDAddress address))
            {
                return address;
            }
            return new LEDAddress(-1, -1); // Invalid address
        }
        
        /// <summary>
        /// Get Unity node index for an LED address
        /// </summary>
        public int GetNodeIndex(LEDAddress ledAddress)
        {
            if (ledToNodeMapping.TryGetValue(ledAddress, out int nodeIndex))
            {
                return nodeIndex;
            }
            return -1; // Invalid node
        }
        
        /// <summary>
        /// Convert Unity colors to LED strip data format with direction and offset customizations
        /// Updated for new polyline-based mapping system with interpolation for smooth animations
        /// </summary>
        public List<LEDStripData> ConvertColorsToLEDData(Color[] nodeColors)
        {
            if (!mappingComplete || nodeColors == null)
            {
                Debug.LogWarning("🚫 ConvertColorsToLEDData: Mapping not complete or no colors");
                return new List<LEDStripData>();
            }
            
            var stripDataList = new List<LEDStripData>();
            
            // Get graph manager for node positions
            if (graphManager == null)
                graphManager = GetComponent<LEDGraphManager>();
            
            if (graphManager == null || graphManager.NodePositions.Count == 0)
            {
                Debug.LogError("LEDCircuitMapper: No graph data available for color conversion!");
                return stripDataList;
            }
            
            // Sort strips by wiring order (same as compilation process)
            var sortedStrips = ledStrips
                .Where(s => s.enabled)
                .OrderBy(s => s.wiringOrder)
                .ToList();
            
            foreach (var strip in sortedStrips)
            {
                var stripData = new LEDStripData
                {
                    stripIndex = strip.stripIndex,
                    dataPin = strip.dataPin,
                    ledColors = new Color[strip.maxLEDsPerBranch]
                };
                
                // Get the node indices for this strip's polyline
                if (strip.nodeIndices.Count < 2)
                {
                    // Not enough nodes, fill with black
                    for (int i = 0; i < stripData.ledColors.Length; i++)
                        stripData.ledColors[i] = Color.black;
                    stripDataList.Add(stripData);
                    continue;
                }
                
                // Use interpolated mapping for smooth animations
                int ledCount = strip.maxLEDsPerBranch;
                int nodeCount = strip.nodeIndices.Count;
                
                for (int ledIndex = 0; ledIndex < ledCount; ledIndex++)
                {
                    // Calculate where this LED falls along the polyline (0 to 1)
                    float t = ledCount > 1 ? (float)ledIndex / (ledCount - 1) : 0f;
                    
                    // Map t to node space - find which two nodes this LED is between
                    float nodePosition = t * (nodeCount - 1);
                    int nodeA = Mathf.FloorToInt(nodePosition);
                    int nodeB = Mathf.CeilToInt(nodePosition);
                    
                    // Clamp to valid range
                    nodeA = Mathf.Clamp(nodeA, 0, nodeCount - 1);
                    nodeB = Mathf.Clamp(nodeB, 0, nodeCount - 1);
                    
                    // Get the actual node indices from the strip's polyline
                    int actualNodeA = strip.nodeIndices[nodeA];
                    int actualNodeB = strip.nodeIndices[nodeB];
                    
                    // Calculate interpolation factor between the two nodes
                    float lerpFactor = nodePosition - nodeA;
                    
                    // Get colors from nodes (with bounds checking)
                    Color colorA = (actualNodeA >= 0 && actualNodeA < nodeColors.Length) 
                        ? nodeColors[actualNodeA] : Color.black;
                    Color colorB = (actualNodeB >= 0 && actualNodeB < nodeColors.Length) 
                        ? nodeColors[actualNodeB] : Color.black;
                    
                    // Interpolate between the two node colors for smooth transitions
                    stripData.ledColors[ledIndex] = Color.Lerp(colorA, colorB, lerpFactor);
                }
                
                // Apply direction reversal if enabled
                if (strip.reverseDirection)
                {
                    Color[] reversedColors = new Color[stripData.ledColors.Length];
                    for (int i = 0; i < stripData.ledColors.Length; i++)
                    {
                        reversedColors[i] = stripData.ledColors[stripData.ledColors.Length - 1 - i];
                    }
                    stripData.ledColors = reversedColors;
                }
                
                stripDataList.Add(stripData);
            }
            
            return stripDataList;
        }
        
        /// <summary>
        /// Calculate LED positions along polyline (same logic as editor visualization)
        /// </summary>
        private List<Vector3> CalculateLEDPositionsForMapping(LEDStripInfo stripInfo, List<Vector3> nodePositions)
        {
            var ledPositions = new List<Vector3>();
            
            if (stripInfo.nodeIndices.Count < 2)
                return ledPositions;
                
            // Get polyline points
            var polylinePoints = new List<Vector3>();
            foreach (int nodeIndex in stripInfo.nodeIndices)
            {
                if (nodeIndex >= 0 && nodeIndex < nodePositions.Count)
                {
                    polylinePoints.Add(nodePositions[nodeIndex]);
                }
            }
            
            if (polylinePoints.Count < 2)
                return ledPositions;
            
            // Distribute LEDs evenly along the polyline
            int ledCount = stripInfo.maxLEDsPerBranch;
            for (int i = 0; i < ledCount; i++)
            {
                float t = ledCount > 1 ? (float)i / (ledCount - 1) : 0f;
                Vector3 ledPos = GetPointAlongPolylineForMapping(polylinePoints, t);
                ledPositions.Add(ledPos);
            }
            
            return ledPositions;
        }
        
        /// <summary>
        /// Get point along polyline at parameter t (0 to 1)
        /// </summary>
        private Vector3 GetPointAlongPolylineForMapping(List<Vector3> points, float t)
        {
            if (points.Count == 0) return Vector3.zero;
            if (points.Count == 1) return points[0];
            
            t = Mathf.Clamp01(t);
            
            // Calculate total length
            float totalLength = 0f;
            for (int i = 0; i < points.Count - 1; i++)
            {
                totalLength += Vector3.Distance(points[i], points[i + 1]);
            }
            
            // Find target distance
            float targetDistance = t * totalLength;
            float currentDistance = 0f;
            
            // Find segment containing target point
            for (int i = 0; i < points.Count - 1; i++)
            {
                float segmentLength = Vector3.Distance(points[i], points[i + 1]);
                
                if (currentDistance + segmentLength >= targetDistance)
                {
                    // Target is in this segment
                    float segmentT = (targetDistance - currentDistance) / segmentLength;
                    return Vector3.Lerp(points[i], points[i + 1], segmentT);
                }
                
                currentDistance += segmentLength;
            }
            
            // Fallback to last point
            return points[points.Count - 1];
        }
        
        /// <summary>
        /// Find closest node to a given position
        /// </summary>
        private int FindClosestNodeForMapping(Vector3 position, List<Vector3> nodePositions)
        {
            int closestIndex = 0;
            float closestDistance = Vector3.Distance(position, nodePositions[0]);
            
            for (int i = 1; i < nodePositions.Count; i++)
            {
                float distance = Vector3.Distance(position, nodePositions[i]);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestIndex = i;
                }
            }
            
            return closestIndex;
        }
        
        private void LogMappingSummary()
        {
            Debug.Log("🎯 LED MAPPING COMPLETE!");
            Debug.Log($"  - Total LED Strips: {ledStrips.Count}");
            Debug.Log($"  - Total LEDs Mapped: {nodeToLEDMapping.Count}");
            Debug.Log($"  - ESP32 Data Pins Used: {string.Join(", ", ledStrips.Select(s => s.dataPin))}");
            
            foreach (var strip in ledStrips)
            {
                Debug.Log($"  - Strip {strip.stripIndex}: Pin {strip.dataPin}, {strip.ledCount} LEDs");
            }
        }
        
        // Direction and offset customization
        private Dictionary<int, bool> stripDirections = new Dictionary<int, bool>();
        private Dictionary<int, int> stripOffsets = new Dictionary<int, int>();
        
        // Public properties
        public bool MappingComplete => mappingComplete;
        public List<LEDStripInfo> LEDStrips => ledStrips;
        public int NodeToLEDMappingCount => nodeToLEDMapping.Count;
        public int TotalLEDCount => ledStrips.Where(s => s.enabled).Sum(s => s.maxLEDsPerBranch);
        public int StripCount => ledStrips.Count(s => s.enabled);
        
        // Legacy properties for backwards compatibility (calculated from new system)
        public int totalPhysicalLEDs => TotalLEDCount; // Now calculated from enabled strips
        public int ledsPerStrip => ledStrips.Count > 0 ? (int)ledStrips.Average(s => s.maxLEDsPerBranch) : 33; // Average LED count per strip
        public int numLogicalStrips => StripCount; // Number of enabled strips
        public MappingStrategy mappingStrategy => MappingStrategy.Distribute_Evenly; // Always use this for new system
        
        // Methods for modifying LED strips
        public void AddLEDStrip(LEDStripInfo stripInfo)
        {
            ledStrips.Add(stripInfo);
        }
        
        public void ClearLEDStrips()
        {
            ledStrips.Clear();
        }
        
        public void RemoveLEDStripAt(int index)
        {
            if (index >= 0 && index < ledStrips.Count)
            {
                ledStrips.RemoveAt(index);
            }
        }
        
        /// <summary>
        /// Clear existing LED mapping
        /// </summary>
        public void ClearLEDMapping()
        {
            nodeToLEDMapping.Clear();
            ledToNodeMapping.Clear();
            mappingComplete = false;
        }
        
        /// <summary>
        /// Add LED mapping between graph node and physical LED address
        /// </summary>
        public void AddLEDMapping(int nodeIndex, int ledAddress)
        {
            var ledAddr = new LEDAddress(0, ledAddress); // All on strip 0 for continuous wiring
            nodeToLEDMapping[nodeIndex] = ledAddr;
            ledToNodeMapping[ledAddr] = nodeIndex;
        }
        
        /// <summary>
        /// Set mapping completion status
        /// </summary>
        public void SetMappingComplete(bool complete)
        {
            mappingComplete = complete;
        }
        
        /// <summary>
        /// Set total LED count (legacy - now calculated automatically from enabled strips)
        /// </summary>
        public void SetTotalLEDCount(int count)
        {
            // Note: This is now calculated automatically from enabled strips
            // but kept for compatibility with existing code
            // totalPhysicalLEDs = count;
        }
        
        /// <summary>
        /// Set direction for a specific strip (true = normal, false = reversed)
        /// </summary>
        public void SetStripDirection(int stripIndex, bool normal)
        {
            if (stripIndex >= 0 && stripIndex < numLogicalStrips)
            {
                stripDirections[stripIndex] = normal;
            }
        }
        
        /// <summary>
        /// Set start offset for a specific strip
        /// </summary>
        public void SetStripOffset(int stripIndex, int offset)
        {
            if (stripIndex >= 0 && stripIndex < numLogicalStrips)
            {
                stripOffsets[stripIndex] = Mathf.Clamp(offset, 0, ledsPerStrip - 1);
            }
        }
        
        /// <summary>
        /// Get node indices for a specific strip
        /// </summary>
        public List<int> GetNodeIndicesForStrip(int stripIndex)
        {
            if (stripIndex >= 0 && stripIndex < ledStrips.Count)
            {
                return ledStrips[stripIndex].nodeIndices;
            }
            return null;
        }
        
        /// <summary>
        /// Check if there's a valid node at the given LED position
        /// </summary>
        public bool HasNodeAtLED(int stripIndex, int ledIndex)
        {
            if (stripIndex >= 0 && stripIndex < ledStrips.Count && 
                ledIndex >= 0 && ledIndex < ledStrips[stripIndex].nodeIndices.Count)
            {
                return ledStrips[stripIndex].nodeIndices[ledIndex] >= 0;
            }
            return false;
        }
        
        /// <summary>
        /// Get the node index at the given LED position
        /// </summary>
        public int GetNodeAtLED(int stripIndex, int ledIndex)
        {
            if (HasNodeAtLED(stripIndex, ledIndex))
            {
                return ledStrips[stripIndex].nodeIndices[ledIndex];
            }
            return -1;
        }
        
        #region MappedGraph Generation
        
        // Cached mapped graph for LED-based animations
        private MappedGraph cachedMappedGraph;
        private bool mappedGraphValid = false;
        
        /// <summary>
        /// Get the MappedGraph representing actual LED positions and topology.
        /// This is generated on-demand and cached.
        /// </summary>
        public MappedGraph GetMappedGraph()
        {
            if (!mappedGraphValid || cachedMappedGraph == null)
            {
                GenerateMappedGraph();
            }
            return cachedMappedGraph;
        }
        
        /// <summary>
        /// Check if a valid MappedGraph is available.
        /// </summary>
        public bool HasValidMappedGraph => mappedGraphValid && cachedMappedGraph != null && cachedMappedGraph.IsValid;
        
        /// <summary>
        /// Force regeneration of the MappedGraph.
        /// Call this after changing strip configuration.
        /// </summary>
        [ContextMenu("Regenerate Mapped Graph")]
        public void InvalidateMappedGraph()
        {
            mappedGraphValid = false;
            if (showDebugInfo)
            {
                Debug.Log("🔄 MappedGraph invalidated - will regenerate on next access");
            }
        }
        
        /// <summary>
        /// Generate the MappedGraph from current strip configuration.
        /// Creates LED positions and topology that animations can use directly.
        /// </summary>
        private void GenerateMappedGraph()
        {
            if (cachedMappedGraph == null)
            {
                cachedMappedGraph = new MappedGraph();
            }
            cachedMappedGraph.Clear();
            
            if (graphManager == null)
            {
                graphManager = GetComponent<LEDGraphManager>();
                if (graphManager == null)
                {
                    graphManager = FindFirstObjectByType<LEDGraphManager>();
                }
            }
            
            if (graphManager == null)
            {
                Debug.LogWarning("⚠️ Cannot generate MappedGraph: graphManager is null");
                mappedGraphValid = false;
                return;
            }
            
            if (!graphManager.DataLoaded)
            {
                Debug.LogWarning($"⚠️ Cannot generate MappedGraph: graph data not loaded");
                mappedGraphValid = false;
                return;
            }
            
            var nodePositions = graphManager.NodePositions;
            
            // Sort strips by wiring order for consistent global indexing
            var sortedStrips = ledStrips
                .Where(s => s.enabled)
                .OrderBy(s => s.wiringOrder)
                .ToList();
            
            if (sortedStrips.Count == 0)
            {
                Debug.LogWarning("⚠️ Cannot generate MappedGraph: No enabled strips");
                mappedGraphValid = false;
                return;
            }
            
            int globalLEDIndex = 0;
            
            // Track which strips are at graph endpoints (for LEDEndpoints)
            var graphEndpointNodes = new HashSet<int>(graphManager.SourceNodes);
            
            foreach (var strip in sortedStrips)
            {
                int stripStartIndex = globalLEDIndex;
                cachedMappedGraph.StripStartIndices.Add(stripStartIndex);
                cachedMappedGraph.StripLEDCounts.Add(strip.maxLEDsPerBranch);
                
                // Calculate LED positions for this strip
                var ledPositions = CalculateLEDPositionsForMapping(strip, nodePositions);
                
                // If position calculation failed, generate placeholder positions
                if (ledPositions.Count == 0)
                {
                    Debug.LogWarning($"⚠️ Strip {strip.stripIndex} ({strip.branchName}): No LED positions calculated");
                    for (int i = 0; i < strip.maxLEDsPerBranch; i++)
                    {
                        ledPositions.Add(Vector3.zero);
                    }
                }
                
                // Add LED data
                for (int i = 0; i < ledPositions.Count; i++)
                {
                    cachedMappedGraph.LEDPositions.Add(ledPositions[i]);
                    cachedMappedGraph.LEDToStripIndex.Add(strip.stripIndex);
                    cachedMappedGraph.LEDToStripPosition.Add(i);
                    
                    float progress = ledPositions.Count > 1 ? (float)i / (ledPositions.Count - 1) : 0f;
                    cachedMappedGraph.LEDProgress.Add(progress);
                    
                    // Create edge to previous LED in same strip
                    if (i > 0)
                    {
                        cachedMappedGraph.LEDEdges.Add(new Vector2Int(globalLEDIndex - 1, globalLEDIndex));
                    }
                    
                    globalLEDIndex++;
                }
                
                // Check if this strip's endpoints are graph endpoints
                if (strip.nodeIndices.Count > 0)
                {
                    int firstNode = strip.nodeIndices[0];
                    int lastNode = strip.nodeIndices[strip.nodeIndices.Count - 1];
                    
                    if (graphEndpointNodes.Contains(firstNode))
                    {
                        cachedMappedGraph.LEDEndpoints.Add(stripStartIndex);
                    }
                    if (graphEndpointNodes.Contains(lastNode))
                    {
                        cachedMappedGraph.LEDEndpoints.Add(stripStartIndex + ledPositions.Count - 1);
                    }
                }
            }
            
            // TODO: Add cross-strip edges where strips connect at junction nodes
            // This requires analyzing which strips share endpoint nodes
            AddCrossStripEdges(sortedStrips);
            
            mappedGraphValid = true;
            
            if (showDebugInfo)
            {
                Debug.Log($"✅ MappedGraph generated: {cachedMappedGraph.GetSummary()}");
            }
        }
        
        /// <summary>
        /// Add edges connecting LEDs across different strips where they share nodes.
        /// This creates the full graph topology.
        /// </summary>
        private void AddCrossStripEdges(List<LEDStripInfo> sortedStrips)
        {
            // Build a map of graph node -> (strip, LED index at that end)
            var nodeToLED = new Dictionary<int, List<(int stripIdx, int ledIdx, bool isStart)>>();
            
            int globalOffset = 0;
            foreach (var strip in sortedStrips)
            {
                if (strip.nodeIndices.Count > 0)
                {
                    int firstNode = strip.nodeIndices[0];
                    int lastNode = strip.nodeIndices[strip.nodeIndices.Count - 1];
                    
                    if (!nodeToLED.ContainsKey(firstNode))
                        nodeToLED[firstNode] = new List<(int, int, bool)>();
                    nodeToLED[firstNode].Add((strip.stripIndex, globalOffset, true));
                    
                    if (!nodeToLED.ContainsKey(lastNode))
                        nodeToLED[lastNode] = new List<(int, int, bool)>();
                    nodeToLED[lastNode].Add((strip.stripIndex, globalOffset + strip.maxLEDsPerBranch - 1, false));
                }
                globalOffset += strip.maxLEDsPerBranch;
            }
            
            // For each node that has multiple strips meeting, create edges between those LEDs
            foreach (var kvp in nodeToLED)
            {
                var connections = kvp.Value;
                if (connections.Count > 1)
                {
                    // Connect all LEDs at this junction
                    for (int i = 0; i < connections.Count; i++)
                    {
                        for (int j = i + 1; j < connections.Count; j++)
                        {
                            int ledA = connections[i].ledIdx;
                            int ledB = connections[j].ledIdx;
                            
                            // Avoid duplicate edges
                            var edge = new Vector2Int(Mathf.Min(ledA, ledB), Mathf.Max(ledA, ledB));
                            if (!cachedMappedGraph.LEDEdges.Contains(edge))
                            {
                                cachedMappedGraph.LEDEdges.Add(edge);
                            }
                        }
                    }
                }
            }
        }
        
        #endregion
    }
    
    /// <summary>
    /// Information about a physical LED strip
    /// </summary>
    [System.Serializable]
    public class LEDStripInfo
    {
        public int stripIndex;
        public int dataPin;
        public int ledCount;
        public List<int> nodeIndices = new List<int>();
        
        [Header("Branch Control")]
        public bool reverseDirection = false;
        public int startOffset = 0;
        public string branchName = "";
        public Color visualColor = Color.white;
        
        [Header("Wiring Configuration")]
        public int wiringOrder = 0; // Physical order in the LED chain (0 = first, 1 = second, etc.)
        
        [Header("Advanced Settings")]
        public int maxLEDsPerBranch = 33; // Override global setting per branch
        public bool enabled = true;
        
        // Constructor
        public LEDStripInfo()
        {
            nodeIndices = new List<int>();
            visualColor = Color.white; // Default color, can be changed later
        }
        
        public LEDStripInfo(int index, int pin, int count)
        {
            stripIndex = index;
            dataPin = pin;
            ledCount = count;
            nodeIndices = new List<int>();
            branchName = $"Branch {index}";
            visualColor = Color.white; // Default color, can be changed later
            maxLEDsPerBranch = count;
        }
        
        /// <summary>
        /// Generate a random color for this strip (call after construction, not during serialization)
        /// </summary>
        public void GenerateRandomColor()
        {
            visualColor = UnityEngine.Random.ColorHSV(0f, 1f, 0.7f, 1f, 0.8f, 1f);
        }
    }
    
    /// <summary>
    /// Address of an LED on a specific strip
    /// </summary>
    [System.Serializable]
    public struct LEDAddress
    {
        public int stripIndex;
        public int ledIndex;
        
        public LEDAddress(int stripIndex, int ledIndex)
        {
            this.stripIndex = stripIndex;
            this.ledIndex = ledIndex;
        }
        
        public bool IsValid => stripIndex >= 0 && ledIndex >= 0;
    }
    
    /// <summary>
    /// Color data for an entire LED strip
    /// </summary>
    [System.Serializable]
    public class LEDStripData
    {
        public int stripIndex;
        public int dataPin;
        public Color[] ledColors;
    }
}
