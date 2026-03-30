using UnityEngine;
using System.Collections.Generic;
using BranchingLEDAnimator.Core;

namespace BranchingLEDAnimator.Visualization
{
    /// <summary>
    /// Game view visualizer that creates 3D LED objects and connection lines
    /// Handles real-time color updates and glow effects
    /// </summary>
    public class LEDGameVisualizer : MonoBehaviour
    {
        [Header("LED Objects")]
        public GameObject ledPrefab;
        public float ledSize = 2f;
        public bool autoCreateLEDs = true;
        
        [Header("Connection Lines")]
        public bool showConnections = true;
        public Material connectionMaterial;
        public float connectionWidth = 0.2f;
        public Color connectionColor = Color.gray;
        
        [Header("LED Materials")]
        public Material ledMaterial;
        public bool useEmissiveMaterials = true;
        public float emissionIntensity = 2f;
        
        [Header("Performance")]
        public bool useMaterialPropertyBlocks = true;
        public int maxLEDs = 1000;
        
        [Header("Debug")]
        public bool showDebugInfo = false;
        
        // Internal state
        private LEDGraphManager graphManager;
        private List<GameObject> ledObjects = new List<GameObject>();
        private List<LineRenderer> connectionLines = new List<LineRenderer>();
        private List<MaterialPropertyBlock> materialBlocks = new List<MaterialPropertyBlock>();
        private List<Renderer> ledRenderers = new List<Renderer>();
        
        private GameObject ledContainer;
        private GameObject connectionContainer;
        private bool gameViewCreated = false;
        
        // Material property IDs for performance
        private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
        
        void Start()
        {
            // Get reference to graph manager on same GameObject
            graphManager = GetComponent<LEDGraphManager>();
            if (graphManager == null)
            {
                Debug.LogError("LEDGameVisualizer requires LEDGraphManager on the same GameObject!");
                return;
            }
            
            // Subscribe to events
            LEDVisualizationEvents.OnGeometryUpdated += OnGeometryUpdated;
            LEDVisualizationEvents.OnColorsUpdated += OnColorsUpdated;
            
            // Create initial Game view if data is already loaded
            if (graphManager.DataLoaded && autoCreateLEDs)
            {
                CreateGameView();
            }
        }
        
        void OnDestroy()
        {
            // Unsubscribe from events
            LEDVisualizationEvents.OnGeometryUpdated -= OnGeometryUpdated;
            LEDVisualizationEvents.OnColorsUpdated -= OnColorsUpdated;
        }
        
        /// <summary>
        /// Handle geometry update event
        /// </summary>
        private void OnGeometryUpdated(LEDGraphManager source, List<Vector3> nodePositions, List<Vector2Int> edgeConnections, List<int> sourceNodes)
        {
            if (source != graphManager) return;
            if (autoCreateLEDs)
            {
                CreateGameView();
            }
        }
        
        /// <summary>
        /// Handle color update event
        /// </summary>
        private void OnColorsUpdated(LEDGraphManager source, Color[] colors)
        {
            if (source != graphManager) return;
            UpdateLEDColors(colors);
        }
        
        /// <summary>
        /// Create 3D LED objects and connections for Game view
        /// </summary>
        [ContextMenu("Create Game View")]
        public void CreateGameView()
        {
            if (graphManager == null || !graphManager.DataLoaded)
            {
                if (showDebugInfo)
                {
                    Debug.LogWarning("No geometry data available for Game view creation!");
                }
                return;
            }
            
            // Clear existing objects
            ClearGameView();
            
            // Create containers
            CreateContainers();
            
            // Create LED objects
            CreateLEDObjects();
            
            // Create connection lines
            if (showConnections)
            {
                CreateConnectionLines();
            }
            
            gameViewCreated = true;
            
            // Automatically focus camera on the new geometry
            FocusCameraOnGeometry();
            
            if (showDebugInfo)
            {
                Debug.Log($"✓ Created Game view with {ledObjects.Count} LEDs and {connectionLines.Count} connections");
            }
        }
        
        /// <summary>
        /// Clear all Game view objects
        /// </summary>
        [ContextMenu("Clear Game View")]
        public void ClearGameView()
        {
            // Destroy LED objects
            foreach (GameObject led in ledObjects)
            {
                if (led != null)
                    DestroyImmediate(led);
            }
            ledObjects.Clear();
            ledRenderers.Clear();
            materialBlocks.Clear();
            
            // Destroy connection lines
            foreach (LineRenderer line in connectionLines)
            {
                if (line != null)
                    DestroyImmediate(line.gameObject);
            }
            connectionLines.Clear();
            
            // Destroy containers
            if (ledContainer != null)
                DestroyImmediate(ledContainer);
            if (connectionContainer != null)
                DestroyImmediate(connectionContainer);
                
            gameViewCreated = false;
            
            if (showDebugInfo)
            {
                Debug.Log("✓ Cleared Game view objects");
            }
        }
        
        /// <summary>
        /// Update LED colors in Game view
        /// </summary>
        private void UpdateLEDColors(Color[] colors)
        {
            if (!gameViewCreated || colors == null || ledRenderers.Count != colors.Length)
                return;
            
            for (int i = 0; i < Mathf.Min(colors.Length, ledRenderers.Count); i++)
            {
                UpdateLEDColor(i, colors[i]);
            }
        }
        
        /// <summary>
        /// Update single LED color
        /// </summary>
        private void UpdateLEDColor(int index, Color color)
        {
            if (index >= ledRenderers.Count || ledRenderers[index] == null)
                return;
            
            if (useMaterialPropertyBlocks && index < materialBlocks.Count)
            {
                // Use MaterialPropertyBlock for better performance
                MaterialPropertyBlock block = materialBlocks[index];
                block.SetColor(BaseColorID, color);
                
                if (useEmissiveMaterials)
                {
                    Color emissionColor = color * emissionIntensity;
                    block.SetColor(EmissionColorID, emissionColor);
                    
                    // Ensure emission is enabled for nighttime visibility
                    block.SetFloat("_EmissionEnabled", 1f);
                }
                
                ledRenderers[index].SetPropertyBlock(block);
            }
            else
            {
                // Direct material modification (creates instances)
                Material mat = ledRenderers[index].material;
                mat.color = color;
                
                if (useEmissiveMaterials && mat.HasProperty(EmissionColorID))
                {
                    Color emissionColor = color * emissionIntensity;
                    mat.SetColor(EmissionColorID, emissionColor);
                    
                    // Enable emission keyword for proper rendering
                    mat.EnableKeyword("_EMISSION");
                }
            }
        }
        
        /// <summary>
        /// Create container GameObjects
        /// </summary>
        private void CreateContainers()
        {
            ledContainer = new GameObject("LED Objects");
            ledContainer.transform.SetParent(transform);
            
            connectionContainer = new GameObject("LED Connections");
            connectionContainer.transform.SetParent(transform);
        }
        
        /// <summary>
        /// Create 3D LED objects at node positions
        /// </summary>
        private void CreateLEDObjects()
        {
            var nodePositions = graphManager.NodePositions;
            
            if (nodePositions.Count > maxLEDs)
            {
                if (showDebugInfo)
                {
                    Debug.LogWarning($"Too many LEDs ({nodePositions.Count}). Limited to {maxLEDs}");
                }
            }
            
            int ledCount = Mathf.Min(nodePositions.Count, maxLEDs);
            
            for (int i = 0; i < ledCount; i++)
            {
                GameObject ledObj = CreateSingleLED(nodePositions[i], i);
                ledObjects.Add(ledObj);
                
                Renderer renderer = ledObj.GetComponent<Renderer>();
                if (renderer != null)
                {
                    ledRenderers.Add(renderer);
                    
                    if (useMaterialPropertyBlocks)
                    {
                        MaterialPropertyBlock block = new MaterialPropertyBlock();
                        materialBlocks.Add(block);
                    }
                }
            }
        }
        
        /// <summary>
        /// Create single LED object
        /// </summary>
        private GameObject CreateSingleLED(Vector3 position, int index)
        {
            GameObject ledObj;
            
            if (ledPrefab != null)
            {
                ledObj = Instantiate(ledPrefab, position, Quaternion.identity, ledContainer.transform);
            }
            else
            {
                // Create default sphere LED
                ledObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                ledObj.transform.position = position;
                ledObj.transform.SetParent(ledContainer.transform);
            }
            
            ledObj.name = $"LED_{index}";
            ledObj.transform.localScale = Vector3.one * ledSize;
            
            // Apply LED material
            Renderer renderer = ledObj.GetComponent<Renderer>();
            if (renderer != null && ledMaterial != null)
            {
                renderer.sharedMaterial = ledMaterial;
            }
            
            return ledObj;
        }
        
        /// <summary>
        /// Create connection lines between LEDs
        /// </summary>
        private void CreateConnectionLines()
        {
            var nodePositions = graphManager.NodePositions;
            var edgeConnections = graphManager.EdgeConnections;
            
            foreach (var edge in edgeConnections)
            {
                if (edge.x >= 0 && edge.x < nodePositions.Count &&
                    edge.y >= 0 && edge.y < nodePositions.Count)
                {
                    GameObject lineObj = new GameObject($"Connection_{edge.x}_{edge.y}");
                    lineObj.transform.SetParent(connectionContainer.transform);
                    
                    LineRenderer line = lineObj.AddComponent<LineRenderer>();
                    line.material = connectionMaterial != null ? connectionMaterial : CreateDefaultLineMaterial();
                    line.startWidth = connectionWidth;
                    line.endWidth = connectionWidth;
                    line.positionCount = 2;
                    line.useWorldSpace = true;
                    
                    line.SetPosition(0, nodePositions[edge.x]);
                    line.SetPosition(1, nodePositions[edge.y]);
                    
                    connectionLines.Add(line);
                }
            }
        }
        
        /// <summary>
        /// Create default line material
        /// </summary>
        private Material CreateDefaultLineMaterial()
        {
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.color = connectionColor;
            return mat;
        }
        
        /// <summary>
        /// Toggle connection visibility
        /// </summary>
        public void ToggleConnections()
        {
            showConnections = !showConnections;
            
            if (connectionContainer != null)
            {
                connectionContainer.SetActive(showConnections);
            }
        }
        
        /// <summary>
        /// Debug method to test color updates
        /// </summary>
        [ContextMenu("Test Color Updates")]
        public void TestColorUpdates()
        {
            if (!gameViewCreated)
            {
                if (showDebugInfo)
                {
                    Debug.LogWarning("Game view not created yet. Create it first!");
                }
                return;
            }
            
            // Test with random colors
            Color[] testColors = new Color[ledRenderers.Count];
            for (int i = 0; i < testColors.Length; i++)
            {
                testColors[i] = new Color(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value, 1f);
            }
            
            UpdateLEDColors(testColors);
            if (showDebugInfo)
            {
                Debug.Log($"Applied random colors to {testColors.Length} LEDs for testing");
            }
        }
        
        /// <summary>
        /// Focus camera on LED geometry with smart positioning
        /// </summary>
        [ContextMenu("Focus Camera On Geometry")]
        public void FocusCameraOnGeometry()
        {
            if (graphManager == null || !graphManager.DataLoaded)
            {
                if (showDebugInfo)
                {
                    Debug.LogWarning("No geometry data to focus camera on!");
                }
                return;
            }
            
            Bounds bounds = graphManager.GetGeometryBounds();
            
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                if (showDebugInfo)
                {
                    Debug.LogWarning("No main camera found!");
                }
                return;
            }
            
            // Calculate smart camera position
            Vector3 center = bounds.center;
            Vector3 size = bounds.size;
            
            // Calculate distance based on geometry size and camera settings
            float maxSize = Mathf.Max(size.x, size.y, size.z);
            float distance;
            
            if (mainCamera.orthographic)
            {
                // For orthographic camera, set orthographic size
                mainCamera.orthographicSize = maxSize * 0.6f;
                distance = maxSize * 1.5f;
            }
            else
            {
                // For perspective camera, calculate distance based on FOV
                float halfFOV = mainCamera.fieldOfView * 0.5f * Mathf.Deg2Rad;
                distance = (maxSize * 0.6f) / Mathf.Tan(halfFOV);
                distance = Mathf.Max(distance, maxSize * 1.5f); // Minimum distance
            }
            
            // Position camera at an angle for better 3D view
            Vector3 offset = new Vector3(distance * 0.5f, distance * 0.3f, -distance * 0.8f);
            mainCamera.transform.position = center + offset;
            mainCamera.transform.LookAt(center);
            
            if (showDebugInfo)
            {
                Debug.Log($"✓ Focused camera on geometry:");
                Debug.Log($"  - Bounds: {bounds}");
                Debug.Log($"  - Center: {center}");
                Debug.Log($"  - Size: {size}");
                Debug.Log($"  - Camera Position: {mainCamera.transform.position}");
                Debug.Log($"  - Distance: {distance:F1}");
                Debug.Log($"  - Camera Mode: {(mainCamera.orthographic ? "Orthographic" : "Perspective")}");
                if (mainCamera.orthographic)
                {
                    Debug.Log($"  - Ortho Size: {mainCamera.orthographicSize:F1}");
                }
            }
            
            // Force a repaint of the Game view
            #if UNITY_EDITOR
            UnityEditor.SceneView.RepaintAll();
            #endif
        }
    }
}
