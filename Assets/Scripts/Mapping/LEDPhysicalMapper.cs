using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using BranchingLEDAnimator.Core;

namespace BranchingLEDAnimator.Mapping
{
    /// <summary>
    /// Visual LED mapping tool that bridges graph-based animations to physical LED circuits
    /// Provides multiple strategies for mapping Unity nodes to physical LED positions
    /// </summary>
    public class LEDPhysicalMapper : MonoBehaviour
    {
        [Header("Mapping Configuration")]
        public MappingMode mappingMode = MappingMode.AutoDetectBranches;
        public bool showMappingVisualization = false;
        public bool enableManualMapping = false;
        
        [Header("Physical LED Configuration")]
        public int totalPhysicalLEDs = 99;
        public List<LEDStripConfig> stripConfigurations = new List<LEDStripConfig>();
        
        [Header("Visual Mapping Tools")]
        public Color unmappedNodeColor = Color.gray;
        public Color[] stripColors = { Color.red, Color.green, Color.blue, Color.yellow, Color.magenta };
        public float mappingGizmoSize = 0.2f;
        public bool showNodeNumbers = false;
        public bool showStripDirections = false;
        
        [Header("Data Export/Import")]
        public string mappingDataPath = "Assets/Data/led_mapping.json";
        public bool autoSaveMappingChanges = true;
        
        // Mapping data
        private Dictionary<int, LEDAddress> nodeToPhysicalMapping = new Dictionary<int, LEDAddress>();
        private Dictionary<LEDAddress, int> physicalToNodeMapping = new Dictionary<LEDAddress, int>();
        private List<BranchPath> detectedBranches = new List<BranchPath>();
        private LEDGraphManager graphManager;
        
        // Visual tools
        private bool mappingToolsActive = false;
        private int selectedNodeIndex = -1;
        private int targetStripIndex = 0;
        private int targetLEDPosition = 0;
        
        public enum MappingMode
        {
            AutoDetectBranches,      // Automatically detect branch structure
            ManualBranchDefinition,  // User defines branch start/end points
            LinearProgression,       // Map nodes 0-N to LEDs 0-N linearly
            GrasshopperMetadata,     // Use metadata from Grasshopper file
            CustomMappingFile        // Load from external mapping file
        }
        
        [System.Serializable]
        public class LEDStripConfig
        {
            public int stripIndex;
            public int ledCount;
            public int dataPin;
            public bool reverseDirection;
            public List<int> branchNodeIndices = new List<int>();
            public string stripName = "";
        }
        
        [System.Serializable]
        public class BranchPath
        {
            public int branchIndex;
            public List<int> nodeSequence = new List<int>();
            public int startNodeIndex;
            public int endNodeIndex;
            public bool reverseForLEDs;
            public string branchName = "";
        }
        
        [System.Serializable]
        public class LEDMappingData
        {
            public MappingMode mode;
            public List<LEDStripConfig> strips;
            public List<BranchPath> branches;
            public Dictionary<int, LEDAddress> nodeMapping;
            public string createdDate;
            public string geometryHash; // To detect if geometry changed
        }
        
        void Start()
        {
            graphManager = GetComponent<LEDGraphManager>();
            InitializeStripConfigurations();
            
            if (graphManager != null && graphManager.DataLoaded)
            {
                DetectMappingFromGeometry();
            }
        }
        
        void Update()
        {
            if (mappingToolsActive)
            {
                HandleMappingInput();
            }
        }
        
        /// <summary>
        /// Initialize default strip configurations
        /// </summary>
        void InitializeStripConfigurations()
        {
            if (stripConfigurations.Count == 0)
            {
                // Default configuration for 3 strips of 33 LEDs
                for (int i = 0; i < 3; i++)
                {
                    stripConfigurations.Add(new LEDStripConfig
                    {
                        stripIndex = i,
                        ledCount = 33,
                        dataPin = i + 2,
                        reverseDirection = false,
                        stripName = $"Strip {i + 1}"
                    });
                }
            }
        }
        
        /// <summary>
        /// Detect mapping strategy based on geometry analysis
        /// </summary>
        [ContextMenu("Detect Mapping From Geometry")]
        public void DetectMappingFromGeometry()
        {
            if (graphManager == null || !graphManager.DataLoaded)
            {
                Debug.LogWarning("No geometry data available for mapping detection");
                return;
            }
            
            Debug.Log("🔍 ANALYZING GEOMETRY FOR LED MAPPING...");
            
            switch (mappingMode)
            {
                case MappingMode.AutoDetectBranches:
                    DetectBranchStructure();
                    break;
                case MappingMode.LinearProgression:
                    CreateLinearMapping();
                    break;
                case MappingMode.GrasshopperMetadata:
                    ExtractGrasshopperMetadata();
                    break;
                case MappingMode.CustomMappingFile:
                    LoadMappingFromFile();
                    break;
            }
            
            ApplyMappingToStrips();
            
            if (autoSaveMappingChanges)
            {
                SaveMappingToFile();
            }
        }
        
        /// <summary>
        /// Detect branch structure from graph connectivity
        /// </summary>
        void DetectBranchStructure()
        {
            var nodePositions = graphManager.NodePositions;
            var edgeConnections = graphManager.EdgeConnections;
            var sourceNodes = graphManager.SourceNodes;
            
            Debug.Log($"📊 Graph Analysis: {nodePositions.Count} nodes, {edgeConnections.Count} edges, {sourceNodes.Count} endpoints");
            
            // Find junction points (nodes with >2 connections)
            Dictionary<int, List<int>> nodeConnections = new Dictionary<int, List<int>>();
            
            foreach (var edge in edgeConnections)
            {
                if (!nodeConnections.ContainsKey(edge.x)) nodeConnections[edge.x] = new List<int>();
                if (!nodeConnections.ContainsKey(edge.y)) nodeConnections[edge.y] = new List<int>();
                
                nodeConnections[edge.x].Add(edge.y);
                nodeConnections[edge.y].Add(edge.x);
            }
            
            // Find branch starting points and endpoints
            List<int> junctionNodes = nodeConnections.Where(kvp => kvp.Value.Count > 2).Select(kvp => kvp.Key).ToList();
            List<int> endpointNodes = nodeConnections.Where(kvp => kvp.Value.Count == 1).Select(kvp => kvp.Key).ToList();
            
            Debug.Log($"🌿 Found {junctionNodes.Count} junction nodes, {endpointNodes.Count} endpoint nodes");
            
            detectedBranches.Clear();
            
            // Trace paths from each endpoint to junctions
            foreach (int endpoint in endpointNodes)
            {
                var branchPath = TracePath(endpoint, nodeConnections, junctionNodes);
                if (branchPath.nodeSequence.Count > 0)
                {
                    branchPath.branchIndex = detectedBranches.Count;
                    branchPath.branchName = $"Branch {detectedBranches.Count + 1}";
                    detectedBranches.Add(branchPath);
                }
            }
            
            Debug.Log($"✅ Detected {detectedBranches.Count} branches");
            
            foreach (var branch in detectedBranches)
            {
                Debug.Log($"  - {branch.branchName}: {branch.nodeSequence.Count} nodes (from {branch.startNodeIndex} to {branch.endNodeIndex})");
            }
        }
        
        /// <summary>
        /// Trace a path from start node following edges until reaching a junction or dead end
        /// </summary>
        BranchPath TracePath(int startNode, Dictionary<int, List<int>> nodeConnections, List<int> junctionNodes)
        {
            var path = new BranchPath
            {
                startNodeIndex = startNode,
                nodeSequence = new List<int> { startNode }
            };
            
            int currentNode = startNode;
            int previousNode = -1;
            
            while (true)
            {
                var connections = nodeConnections[currentNode];
                
                // Find next unvisited node
                int nextNode = -1;
                foreach (int connected in connections)
                {
                    if (connected != previousNode)
                    {
                        nextNode = connected;
                        break;
                    }
                }
                
                if (nextNode == -1) break; // Dead end
                
                path.nodeSequence.Add(nextNode);
                
                // Stop at junctions (except for the start)
                if (junctionNodes.Contains(nextNode) && nextNode != startNode)
                {
                    path.endNodeIndex = nextNode;
                    break;
                }
                
                previousNode = currentNode;
                currentNode = nextNode;
                
                // Safety check for infinite loops
                if (path.nodeSequence.Count > 1000)
                {
                    Debug.LogWarning($"Path tracing stopped - potential infinite loop detected starting from node {startNode}");
                    break;
                }
            }
            
            path.endNodeIndex = path.nodeSequence.Last();
            return path;
        }
        
        /// <summary>
        /// Create simple linear mapping (node index = LED index)
        /// </summary>
        void CreateLinearMapping()
        {
            Debug.Log("📏 Creating linear node-to-LED mapping");
            
            nodeToPhysicalMapping.Clear();
            physicalToNodeMapping.Clear();
            
            int totalNodes = graphManager.NodeCount;
            float step = (float)totalNodes / totalPhysicalLEDs;
            
            for (int ledIndex = 0; ledIndex < totalPhysicalLEDs; ledIndex++)
            {
                int nodeIndex = Mathf.RoundToInt(ledIndex * step);
                if (nodeIndex < totalNodes)
                {
                    int stripIndex = ledIndex / 33; // Assuming 33 LEDs per strip
                    int ledPosition = ledIndex % 33;
                    
                    var ledAddress = new LEDAddress(stripIndex, ledPosition);
                    nodeToPhysicalMapping[nodeIndex] = ledAddress;
                    physicalToNodeMapping[ledAddress] = nodeIndex;
                }
            }
            
            Debug.Log($"✅ Linear mapping complete: {nodeToPhysicalMapping.Count} nodes mapped");
        }
        
        /// <summary>
        /// Extract metadata from Grasshopper file if available
        /// </summary>
        void ExtractGrasshopperMetadata()
        {
            Debug.Log("📄 Attempting to extract Grasshopper metadata...");
            
            // This would read additional data from the Grasshopper export
            // For now, fall back to auto-detection
            Debug.LogWarning("Grasshopper metadata extraction not yet implemented - using auto-detection");
            DetectBranchStructure();
        }
        
        /// <summary>
        /// Apply detected mapping to strip configurations
        /// </summary>
        void ApplyMappingToStrips()
        {
            // Clear existing strip assignments
            foreach (var strip in stripConfigurations)
            {
                strip.branchNodeIndices.Clear();
            }
            
            // Assign branches to strips
            for (int i = 0; i < detectedBranches.Count && i < stripConfigurations.Count; i++)
            {
                var branch = detectedBranches[i];
                var strip = stripConfigurations[i];
                
                // Determine how many nodes to take from this branch
                int nodesToTake = Mathf.Min(branch.nodeSequence.Count, strip.ledCount);
                
                // Distribute nodes evenly if branch has more nodes than LEDs
                if (branch.nodeSequence.Count > strip.ledCount)
                {
                    float step = (float)branch.nodeSequence.Count / strip.ledCount;
                    for (int j = 0; j < strip.ledCount; j++)
                    {
                        int nodeIndex = Mathf.RoundToInt(j * step);
                        if (nodeIndex < branch.nodeSequence.Count)
                        {
                            strip.branchNodeIndices.Add(branch.nodeSequence[nodeIndex]);
                        }
                    }
                }
                else
                {
                    // Use all nodes from branch
                    strip.branchNodeIndices.AddRange(branch.nodeSequence.Take(nodesToTake));
                }
                
                Debug.Log($"📍 Strip {i}: {strip.branchNodeIndices.Count} nodes assigned from {branch.branchName}");
            }
        }
        
        /// <summary>
        /// Handle manual mapping input in Scene view
        /// </summary>
        void HandleMappingInput()
        {
            // This would handle mouse input for manual node-to-LED assignment
            // Implementation would depend on Unity editor tools
        }
        
        /// <summary>
        /// Save mapping configuration to file
        /// </summary>
        [ContextMenu("Save Mapping To File")]
        public void SaveMappingToFile()
        {
            var mappingData = new LEDMappingData
            {
                mode = mappingMode,
                strips = new List<LEDStripConfig>(stripConfigurations),
                branches = new List<BranchPath>(detectedBranches),
                nodeMapping = new Dictionary<int, LEDAddress>(nodeToPhysicalMapping),
                createdDate = System.DateTime.Now.ToString(),
                geometryHash = GetGeometryHash()
            };
            
            string json = JsonUtility.ToJson(mappingData, true);
            System.IO.File.WriteAllText(mappingDataPath, json);
            
            Debug.Log($"💾 Mapping saved to {mappingDataPath}");
        }
        
        /// <summary>
        /// Load mapping configuration from file
        /// </summary>
        [ContextMenu("Load Mapping From File")]
        public void LoadMappingFromFile()
        {
            if (System.IO.File.Exists(mappingDataPath))
            {
                string json = System.IO.File.ReadAllText(mappingDataPath);
                var mappingData = JsonUtility.FromJson<LEDMappingData>(json);
                
                mappingMode = mappingData.mode;
                stripConfigurations = mappingData.strips;
                detectedBranches = mappingData.branches;
                nodeToPhysicalMapping = mappingData.nodeMapping;
                
                // Rebuild reverse mapping
                physicalToNodeMapping.Clear();
                foreach (var kvp in nodeToPhysicalMapping)
                {
                    physicalToNodeMapping[kvp.Value] = kvp.Key;
                }
                
                Debug.Log($"📂 Mapping loaded from {mappingDataPath}");
            }
            else
            {
                Debug.LogWarning($"Mapping file not found: {mappingDataPath}");
            }
        }
        
        /// <summary>
        /// Get hash of current geometry for change detection
        /// </summary>
        string GetGeometryHash()
        {
            if (graphManager == null) return "";
            
            string geometryString = $"{graphManager.NodeCount}_{graphManager.EdgeCount}";
            return geometryString.GetHashCode().ToString();
        }
        
        /// <summary>
        /// Visualization in Scene view
        /// </summary>
        void OnDrawGizmos()
        {
            if (!showMappingVisualization || graphManager == null || !graphManager.DataLoaded)
                return;
            
            var nodePositions = graphManager.NodePositions;
            
            // Draw nodes colored by strip assignment
            for (int i = 0; i < nodePositions.Count; i++)
            {
                Vector3 pos = nodePositions[i];
                
                if (nodeToPhysicalMapping.ContainsKey(i))
                {
                    var ledAddress = nodeToPhysicalMapping[i];
                    int stripIndex = ledAddress.stripIndex;
                    
                    // Color by strip
                    if (stripIndex < stripColors.Length)
                    {
                        Gizmos.color = stripColors[stripIndex];
                    }
                    else
                    {
                        Gizmos.color = Color.white;
                    }
                }
                else
                {
                    Gizmos.color = unmappedNodeColor;
                }
                
                Gizmos.DrawSphere(pos, mappingGizmoSize);
                
                // Draw node numbers if enabled
                if (showNodeNumbers)
                {
                    #if UNITY_EDITOR
                    UnityEditor.Handles.Label(pos + Vector3.up * 0.3f, i.ToString());
                    #endif
                }
            }
            
            // Draw strip direction indicators
            if (showStripDirections)
            {
                DrawStripDirections();
            }
        }
        
        /// <summary>
        /// Draw arrows showing LED strip directions
        /// </summary>
        void DrawStripDirections()
        {
            foreach (var strip in stripConfigurations)
            {
                if (strip.branchNodeIndices.Count < 2) continue;
                
                var nodePositions = graphManager.NodePositions;
                
                for (int i = 0; i < strip.branchNodeIndices.Count - 1; i++)
                {
                    int nodeA = strip.branchNodeIndices[i];
                    int nodeB = strip.branchNodeIndices[i + 1];
                    
                    if (nodeA < nodePositions.Count && nodeB < nodePositions.Count)
                    {
                        Vector3 posA = nodePositions[nodeA];
                        Vector3 posB = nodePositions[nodeB];
                        
                        if (strip.stripIndex < stripColors.Length)
                        {
                            Gizmos.color = stripColors[strip.stripIndex];
                        }
                        
                        // Draw direction arrow
                        Gizmos.DrawLine(posA, posB);
                        
                        // Draw arrowhead
                        Vector3 direction = (posB - posA).normalized;
                        Vector3 arrowPos = Vector3.Lerp(posA, posB, 0.8f);
                        Vector3 perpendicular = Vector3.Cross(direction, Vector3.up).normalized * 0.1f;
                        
                        Gizmos.DrawLine(arrowPos, arrowPos - direction * 0.1f + perpendicular);
                        Gizmos.DrawLine(arrowPos, arrowPos - direction * 0.1f - perpendicular);
                    }
                }
            }
        }
        
        // Public interface for other components
        public Dictionary<int, LEDAddress> GetNodeToLEDMapping() => new Dictionary<int, LEDAddress>(nodeToPhysicalMapping);
        public List<LEDStripConfig> GetStripConfigurations() => new List<LEDStripConfig>(stripConfigurations);
        public List<BranchPath> GetDetectedBranches() => new List<BranchPath>(detectedBranches);
    }
    
    /// <summary>
    /// LED address structure for physical mapping
    /// </summary>
    [System.Serializable]
    public struct LEDAddress
    {
        public int stripIndex;
        public int ledPosition;
        
        public LEDAddress(int strip, int position)
        {
            stripIndex = strip;
            ledPosition = position;
        }
        
        public override string ToString() => $"Strip{stripIndex}:LED{ledPosition}";
    }
}
