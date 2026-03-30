using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using System.Linq; // Added for Select
using BranchingLEDAnimator.Core;

namespace BranchingLEDAnimator.Visualization
{
    /// <summary>
    /// Unified Game view visualizer with built-in nighttime environment
    /// Combines LED visualization and atmospheric setup in one component
    /// </summary>
    public class UnifiedGameVisualizer : MonoBehaviour
    {
        [Header("LED Visualization")]
        public GameObject ledPrefab;
        public float ledSize = 6f; // Larger for better visibility in dark environment
        public bool autoCreateOnStart = false; // DISABLED - causing issues
        public bool showConnections = true;
        public float connectionWidth = 0.2f;
        
        [Header("LED Materials & Glow")]
        public Material ledMaterial;
        [Tooltip("Higher values = brighter glow but may shift hue. Use 1-5 for accurate colors, 10-20 for dramatic glow.")]
        public float emissionIntensity = 5f; // Reduced for better color accuracy
        public bool useEmissiveMaterials = true;
        [Tooltip("Preserve original hue when applying emission (prevents color shifting)")]
        public bool preserveHueInEmission = true;
        
        [Header("Nighttime Environment")]
        public bool applyNightEnvironment = true;
        public Color skyboxTop = new Color(0.02f, 0.02f, 0.08f, 1f);
        public Color skyboxBottom = new Color(0.005f, 0.005f, 0.02f, 1f);
        public Color ambientColor = new Color(0.05f, 0.05f, 0.1f, 1f);
        public float ambientIntensity = 0.3f;
        
        [Header("Performance")]
        public int maxLEDs = 1000;
        public bool useMaterialPropertyBlocks = true;
        
        [Header("Debug")]
        public bool showDebugInfo = true;
        
        // Internal state
        private LEDGraphManager graphManager;
        private LEDAnimationSystem animationSystem;
        private List<GameObject> ledObjects = new List<GameObject>();
        private List<LineRenderer> connectionLines = new List<LineRenderer>();
        private List<Renderer> ledRenderers = new List<Renderer>();
        private List<MaterialPropertyBlock> materialBlocks = new List<MaterialPropertyBlock>();
        
        private GameObject ledContainer;
        private GameObject connectionContainer;
        private bool gameViewCreated = false;
        private bool environmentApplied = false;
        
        // Material property IDs
        private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
        private static readonly int MainTexID = Shader.PropertyToID("_MainTex");
        
        // Custom materials
        private Material customSkyboxMaterial;
        private Material defaultLEDMaterial;
        
        void Start()
        {
            // Get component references
            graphManager = GetComponent<LEDGraphManager>();
            animationSystem = GetComponent<LEDAnimationSystem>();
            
            if (graphManager == null)
            {
                Debug.LogError("UnifiedGameVisualizer requires LEDGraphManager on the same GameObject!");
                return;
            }
            
            if (animationSystem == null)
            {
                Debug.LogError("UnifiedGameVisualizer requires LEDAnimationSystem on the same GameObject!");
                return;
            }
            
            // Apply nighttime environment first
            if (applyNightEnvironment)
            {
                ApplyNightTimeEnvironment();
            }
            
            // Subscribe to events
            LEDVisualizationEvents.OnGeometryUpdated += OnGeometryUpdated;
            LEDVisualizationEvents.OnColorsUpdated += OnColorsUpdated;
            
            // Create initial visualization if data is already loaded
            if (graphManager.DataLoaded)
            {
                CreateGameVisualization();
            }
            
            Debug.Log("🎮 UnifiedGameVisualizer initialized successfully!");
        }
        
        void OnDestroy()
        {
            // Unsubscribe from events
            LEDVisualizationEvents.OnGeometryUpdated -= OnGeometryUpdated;
            LEDVisualizationEvents.OnColorsUpdated -= OnColorsUpdated;
            
            // Clean up materials
            if (customSkyboxMaterial != null)
                DestroyImmediate(customSkyboxMaterial);
            if (defaultLEDMaterial != null)
                DestroyImmediate(defaultLEDMaterial);
        }
        
        /// <summary>
        /// Handle geometry update event
        /// </summary>
        private void OnGeometryUpdated(LEDGraphManager source, List<Vector3> nodePositions, List<Vector2Int> edgeConnections, List<int> sourceNodes)
        {
            if (source != graphManager) return;
            CreateGameVisualization();
        }
        
        /// <summary>
        /// Handle color update event - this is the key method for animation updates
        /// </summary>
        private void OnColorsUpdated(LEDGraphManager source, Color[] colors)
        {
            if (source != graphManager) return;
            if (showDebugInfo && colors != null)
            {
                // Debug.Log($"🎨 Color update received: {colors.Length} colors");
            }
            
            UpdateLEDColors(colors);
        }
        
        /// <summary>
        /// Create complete game visualization with nighttime environment
        /// </summary>
        [ContextMenu("Create Game Visualization")]
        public void CreateGameVisualization()
        {
            // Ensure components are initialized
            if (graphManager == null)
            {
                graphManager = GetComponent<LEDGraphManager>();
            }
            
            if (animationSystem == null)
            {
                animationSystem = GetComponent<LEDAnimationSystem>();
            }
            
            if (graphManager == null)
            {
                Debug.LogError("LEDGraphManager not found! Make sure it's on the same GameObject.");
                return;
            }
            
            if (!graphManager.DataLoaded)
            {
                Debug.LogWarning("No geometry data loaded yet. Trying to import from Grasshopper...");
                
                // Try to import data first
                bool importSuccess = graphManager.ImportFromGrasshopper();
                if (!importSuccess)
                {
                    Debug.LogError("Failed to import geometry data. Check the Grasshopper file path in LEDGraphManager.");
                    return;
                }
                
                Debug.Log("✓ Geometry data imported successfully!");
            }
            else
            {
                Debug.Log($"✓ Geometry data already loaded: {graphManager.NodeCount} nodes");
            }
            
            Debug.Log("🚀 Creating unified game visualization...");
            
            // Apply nighttime environment if not already applied
            if (applyNightEnvironment && !environmentApplied)
            {
                ApplyNightTimeEnvironment();
            }
            
            // Clear existing visualization
            ClearGameVisualization();
            
            // Create LED visualization
            CreateLEDVisualization();
            
            // Focus camera
            FocusCameraOnGeometry();
            
            // Mark as created so color updates work
            gameViewCreated = true;
            
            // Force initial color update
            if (animationSystem != null)
            {
                // Manually trigger a color update to ensure LEDs show current animation state
                var currentColors = graphManager.CurrentNodeColors;
                if (currentColors != null && currentColors.Length > 0)
                {
                    Debug.Log($"🎨 Applying initial colors: {currentColors.Length} colors to {ledRenderers.Count} LEDs");
                    UpdateLEDColors(currentColors);
                }
                else
                {
                    Debug.LogWarning("⚠️ No current colors available from animation system");
                    
                    // Apply bright test colors to verify emission works
                    Color[] testColors = new Color[ledRenderers.Count];
                    for (int i = 0; i < testColors.Length; i++)
                    {
                        testColors[i] = Color.red; // Bright red for testing
                    }
                    UpdateLEDColors(testColors);
                    Debug.Log($"🔴 Applied red test colors to {testColors.Length} LEDs");
                }
            }
            
            Debug.Log($"✓ Game visualization created: {ledObjects.Count} LEDs, {connectionLines.Count} connections");
        }
        
        /// <summary>
        /// Clear all visualization objects
        /// </summary>
        [ContextMenu("Clear Game Visualization")]
        public void ClearGameVisualization()
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
            
            Debug.Log("✓ Game visualization cleared");
        }
        
        /// <summary>
        /// Apply nighttime environment settings
        /// </summary>
        private void ApplyNightTimeEnvironment()
        {
            if (environmentApplied) return;
            
            Debug.Log("🌙 Applying nighttime environment...");
            
            // Create custom skybox
            CreateCustomSkybox();
            
            // Setup lighting
            SetupNightLighting();
            
            // Setup camera
            SetupCamera();
            
            environmentApplied = true;
            Debug.Log("✓ Nighttime environment applied");
        }
        
        /// <summary>
        /// Create custom gradient skybox
        /// </summary>
        private void CreateCustomSkybox()
        {
            // Try gradient skybox first
            Shader skyboxShader = Shader.Find("Skybox/Gradient");
            if (skyboxShader == null)
            {
                skyboxShader = Shader.Find("Skybox/Procedural");
            }
            
            if (skyboxShader != null)
            {
                customSkyboxMaterial = new Material(skyboxShader);
                
                if (skyboxShader.name.Contains("Procedural"))
                {
                    customSkyboxMaterial.SetColor("_SkyTint", skyboxTop);
                    customSkyboxMaterial.SetColor("_GroundColor", skyboxBottom);
                    customSkyboxMaterial.SetFloat("_Exposure", 0.4f);
                    customSkyboxMaterial.SetFloat("_AtmosphereThickness", 0.3f);
                }
                
                RenderSettings.skybox = customSkyboxMaterial;
                DynamicGI.UpdateEnvironment();
            }
            else
            {
                // Fallback to solid color
                RenderSettings.skybox = null;
                Camera mainCamera = Camera.main;
                if (mainCamera != null)
                {
                    mainCamera.clearFlags = CameraClearFlags.SolidColor;
                    mainCamera.backgroundColor = skyboxBottom;
                }
            }
        }
        
        /// <summary>
        /// Setup nighttime lighting
        /// </summary>
        private void SetupNightLighting()
        {
            // Ambient lighting
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = ambientColor;
            RenderSettings.ambientEquatorColor = ambientColor * 0.7f;
            RenderSettings.ambientGroundColor = ambientColor * 0.3f;
            RenderSettings.ambientIntensity = ambientIntensity;
            
            // Main light
            Light mainLight = FindFirstObjectByType<Light>();
            if (mainLight == null)
            {
                GameObject lightObj = new GameObject("Main Light");
                lightObj.transform.SetParent(transform);
                mainLight = lightObj.AddComponent<Light>();
                mainLight.type = LightType.Directional;
            }
            
            if (mainLight != null)
            {
                mainLight.color = new Color(0.7f, 0.8f, 1f, 1f);
                mainLight.intensity = 0.4f;
                mainLight.shadows = LightShadows.Soft;
                mainLight.transform.rotation = Quaternion.Euler(30f, -30f, 0f);
            }
        }
        
        /// <summary>
        /// Setup camera for nighttime
        /// </summary>
        private void SetupCamera()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                mainCamera.backgroundColor = skyboxBottom;
                mainCamera.allowHDR = true;
                
                // Optimize for emission visibility in URP
                mainCamera.clearFlags = CameraClearFlags.SolidColor;
                
                // Ensure camera can see emission properly
                if (mainCamera.renderingPath == RenderingPath.UsePlayerSettings)
                {
                    // Let URP handle the rendering
                }
                
                Debug.Log("✓ Camera optimized for emission visibility");
            }
        }
        
        /// <summary>
        /// Create LED visualization objects
        /// </summary>
        private void CreateLEDVisualization()
        {
            // First, try to find existing LED objects from other visualizers
            FindExistingLEDObjects();
            
            if (ledObjects.Count == 0)
            {
                Debug.Log("No existing LED objects found, creating new ones...");
                
                // Create containers — worldPositionStays: false so they inherit the sculpture's transform
                ledContainer = new GameObject("LED Objects (UnifiedGameVisualizer)");
                ledContainer.transform.SetParent(transform, false);
                
                connectionContainer = new GameObject("LED Connections");
                connectionContainer.transform.SetParent(transform, false);
                
                // Create default LED material if none provided
                if (ledMaterial == null)
                {
                    CreateDefaultLEDMaterial();
                }
                
                // Create LED objects
                CreateLEDObjects();
            }
            else
            {
                Debug.Log($"✓ Found {ledObjects.Count} existing LED objects, using them for Game View");
                
                // Setup existing objects with proper materials and emission
                SetupExistingLEDObjects();
            }
            
            // Create connections if enabled and not already present
            if (showConnections && connectionLines.Count == 0)
            {
                if (connectionContainer == null)
                {
                    connectionContainer = new GameObject("LED Connections");
                    connectionContainer.transform.SetParent(transform, false);
                }
                CreateConnectionLines();
            }
        }
        
        /// <summary>
        /// Find existing LED objects in the scene
        /// </summary>
        private void FindExistingLEDObjects()
        {
            ledObjects.Clear();
            ledRenderers.Clear();
            materialBlocks.Clear();
            
            // Only search under this sculpture's own transform to avoid
            // picking up LED objects belonging to other sculptures
            foreach (Transform child in transform)
            {
                // Check direct children that look like LED containers
                if (child.name.Contains("LED") || child.name.Contains("Node"))
                {
                    // Check the container's children for renderers
                    foreach (Transform grandchild in child)
                    {
                        Renderer renderer = grandchild.GetComponent<Renderer>();
                        if (renderer != null && !ledObjects.Contains(grandchild.gameObject))
                        {
                            ledObjects.Add(grandchild.gameObject);
                            ledRenderers.Add(renderer);
                            materialBlocks.Add(new MaterialPropertyBlock());
                        }
                    }
                    
                    // Also check the container itself
                    Renderer containerRenderer = child.GetComponent<Renderer>();
                    if (containerRenderer != null && !ledObjects.Contains(child.gameObject))
                    {
                        ledObjects.Add(child.gameObject);
                        ledRenderers.Add(containerRenderer);
                        materialBlocks.Add(new MaterialPropertyBlock());
                    }
                }
            }
            
            Debug.Log($"🔍 Found {ledObjects.Count} existing LED objects under {gameObject.name}");
        }
        
        /// <summary>
        /// Setup existing LED objects with proper emission using MaterialPropertyBlocks
        /// </summary>
        private void SetupExistingLEDObjects()
        {
            // Don't modify the shared materials - use MaterialPropertyBlocks instead
            // This ensures Scene View animations continue to work normally
            
            // Just ensure we have MaterialPropertyBlocks ready for each renderer
            for (int i = 0; i < ledRenderers.Count; i++)
            {
                if (i >= materialBlocks.Count)
                {
                    materialBlocks.Add(new MaterialPropertyBlock());
                }
            }
            
            Debug.Log($"✓ Prepared emission blocks for {ledRenderers.Count} existing LED objects (materials unchanged)");
        }
        
        /// <summary>
        /// Create default emissive LED material
        /// </summary>
        private void CreateDefaultLEDMaterial()
        {
            // Try URP Lit shader first
            Shader ledShader = Shader.Find("Universal Render Pipeline/Lit");
            if (ledShader == null)
            {
                ledShader = Shader.Find("Standard");
            }
            if (ledShader == null)
            {
                ledShader = Shader.Find("Unlit/Color");
            }
            
            if (ledShader != null)
            {
                defaultLEDMaterial = new Material(ledShader);
                defaultLEDMaterial.name = "Default LED Material";
                
                // Setup for URP emission
                if (defaultLEDMaterial.HasProperty("_EmissionColor"))
                {
                    defaultLEDMaterial.EnableKeyword("_EMISSION");
                    defaultLEDMaterial.SetColor("_EmissionColor", Color.white);
                    defaultLEDMaterial.SetFloat("_EmissionEnabled", 1f); // Enable emission in URP
                    
                    // Enable URP emission map if available
                    if (defaultLEDMaterial.HasProperty("_UseEmissiveIntensity"))
                        defaultLEDMaterial.SetFloat("_UseEmissiveIntensity", 1f);
                }
                
                ledMaterial = defaultLEDMaterial;
                
                Debug.Log($"✓ Created default LED material with shader: {ledShader.name}");
            }
        }
        
        /// <summary>
        /// Create LED objects at node positions
        /// </summary>
        private void CreateLEDObjects()
        {
            var nodePositions = graphManager.NodePositions;
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
            
            Debug.Log($"✓ Created {ledCount} LED objects");
        }
        
        /// <summary>
        /// Create single LED object
        /// </summary>
        private GameObject CreateSingleLED(Vector3 position, int index)
        {
            GameObject ledObj;
            
            if (ledPrefab != null)
            {
                ledObj = Instantiate(ledPrefab, ledContainer.transform);
                ledObj.transform.localPosition = position;
            }
            else
            {
                ledObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                ledObj.transform.SetParent(ledContainer.transform, false);
                ledObj.transform.localPosition = position;
            }
            
            ledObj.name = $"LED_{index}";
            ledObj.transform.localScale = Vector3.one * ledSize;
            
            // Apply material
            Renderer renderer = ledObj.GetComponent<Renderer>();
            if (renderer != null && ledMaterial != null)
            {
                renderer.sharedMaterial = ledMaterial;
            }
            
            return ledObj;
        }
        
        /// <summary>
        /// Create connection lines
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
                    lineObj.transform.SetParent(connectionContainer.transform, false);
                    
                    LineRenderer line = lineObj.AddComponent<LineRenderer>();
                    line.material = CreateDefaultLineMaterial();
                    line.startWidth = connectionWidth;
                    line.endWidth = connectionWidth;
                    line.positionCount = 2;
                    line.useWorldSpace = false;
                    
                    line.SetPosition(0, nodePositions[edge.x]);
                    line.SetPosition(1, nodePositions[edge.y]);
                    
                    connectionLines.Add(line);
                }
            }
            
            Debug.Log($"✓ Created {connectionLines.Count} connection lines");
        }
        
        /// <summary>
        /// Create default line material
        /// </summary>
        private Material CreateDefaultLineMaterial()
        {
            Shader lineShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (lineShader == null)
            {
                lineShader = Shader.Find("Unlit/Color");
            }
            
            Material mat = new Material(lineShader);
            mat.color = new Color(0.3f, 0.3f, 0.4f, 1f); // Dim blue-gray for connections
            return mat;
        }
        
        /// <summary>
        /// Update LED colors - the core animation method
        /// </summary>
        private void UpdateLEDColors(Color[] colors)
        {
            if (!gameViewCreated || colors == null || ledRenderers.Count == 0)
            {
                if (showDebugInfo)
                {
                    Debug.LogWarning($"Cannot update colors: gameViewCreated={gameViewCreated}, colors={colors?.Length}, renderers={ledRenderers.Count}");
                }
                return;
            }
            
            int updateCount = Mathf.Min(colors.Length, ledRenderers.Count);
            
            for (int i = 0; i < updateCount; i++)
            {
                UpdateSingleLED(i, colors[i]);
            }
            
            if (showDebugInfo)
            {
                // Debug.Log($"🎨 Updated {updateCount} LED colors");
            }
        }
        
        /// <summary>
        /// Update single LED color with proper emission
        /// </summary>
        private void UpdateSingleLED(int index, Color color)
        {
            if (index >= ledRenderers.Count || ledRenderers[index] == null)
                return;
            
            Color emissionColor = CalculateEmissionColor(color);
            
            if (useMaterialPropertyBlocks && index < materialBlocks.Count)
            {
                MaterialPropertyBlock block = materialBlocks[index];
                block.SetColor(BaseColorID, color);
                
                if (useEmissiveMaterials)
                {
                    // URP emission setup
                    block.SetColor(EmissionColorID, emissionColor);
                    block.SetFloat("_EmissionEnabled", 1f); // Critical for URP
                    
                    // Also set URP-specific emission color if it exists
                    block.SetColor("_EmissionColor", emissionColor);
                }
                
                ledRenderers[index].SetPropertyBlock(block);
            }
            else
            {
                Material mat = ledRenderers[index].material;
                mat.color = color;
                
                if (useEmissiveMaterials && mat.HasProperty(EmissionColorID))
                {
                    mat.SetColor(EmissionColorID, emissionColor);
                    mat.SetColor("_EmissionColor", emissionColor);
                    mat.SetFloat("_EmissionEnabled", 1f); // Critical for URP
                    
                    // Enable emission keywords for URP
                    mat.EnableKeyword("_EMISSION");
                }
            }
        }
        
        /// <summary>
        /// Calculate emission color while optionally preserving hue
        /// </summary>
        private Color CalculateEmissionColor(Color baseColor)
        {
            if (!preserveHueInEmission)
            {
                // Original behavior - simple multiplication (can shift hue)
                return baseColor * emissionIntensity;
            }
            
            // Preserve hue by working in HSV space
            Color.RGBToHSV(baseColor, out float h, out float s, out float v);
            
            // Skip nearly black colors
            if (v < 0.01f) return Color.black;
            
            // Apply intensity to value only, preserving hue and saturation
            // Use sqrt for more natural-feeling brightness scaling
            float boostedValue = v * Mathf.Sqrt(emissionIntensity);
            
            // Convert back to RGB
            Color emissionColor = Color.HSVToRGB(h, s, 1f); // Use full value for emission base
            
            // Scale by the boosted intensity
            return emissionColor * boostedValue;
        }
        
        /// <summary>
        /// Focus camera on geometry
        /// </summary>
        [ContextMenu("Focus Camera")]
        public void FocusCameraOnGeometry()
        {
            if (graphManager == null || !graphManager.DataLoaded)
                return;
            
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
                return;
            
            Bounds bounds = graphManager.GetGeometryBounds();
            Vector3 center = bounds.center;
            Vector3 size = bounds.size;
            
            float maxSize = Mathf.Max(size.x, size.y, size.z);
            float distance = maxSize * 2f;
            
            // Position camera at an angle
            Vector3 offset = new Vector3(distance * 0.5f, distance * 0.3f, -distance * 0.8f);
            mainCamera.transform.position = center + offset;
            mainCamera.transform.LookAt(center);
            
            Debug.Log($"✓ Camera focused on geometry (distance: {distance:F1})");
        }
        
        /// <summary>
        /// Test method to verify color updates work
        /// </summary>
        [ContextMenu("Test Random Colors")]
        public void TestRandomColors()
        {
            // Ensure components are initialized
            if (graphManager == null)
            {
                graphManager = GetComponent<LEDGraphManager>();
            }
            
            if (!gameViewCreated)
            {
                Debug.LogWarning("Create visualization first!");
                return;
            }
            
            Color[] testColors = new Color[ledRenderers.Count];
            for (int i = 0; i < testColors.Length; i++)
            {
                testColors[i] = new Color(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value, 1f);
            }
            
            UpdateLEDColors(testColors);
            Debug.Log($"🎲 Applied random colors to {testColors.Length} LEDs");
        }
        
        /// <summary>
        /// Reset to Unity defaults
        /// </summary>
        [ContextMenu("Reset Environment")]
        public void ResetEnvironment()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1f;
            
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                mainCamera.backgroundColor = new Color(0.19f, 0.3f, 0.47f, 0f);
            }
            
            environmentApplied = false;
            Debug.Log("✓ Environment reset to defaults");
        }
        
        /// <summary>
        /// Diagnostic method to check system status
        /// </summary>
        [ContextMenu("Check System Status")]
        public void CheckSystemStatus()
        {
            Debug.Log("🔍 System Status Check:");
            
            // Ensure components are initialized
            if (graphManager == null)
            {
                graphManager = GetComponent<LEDGraphManager>();
            }
            
            if (animationSystem == null)
            {
                animationSystem = GetComponent<LEDAnimationSystem>();
            }
            
            // Check components
            if (graphManager == null)
            {
                Debug.LogError("❌ LEDGraphManager: MISSING - Component not found on GameObject!");
                Debug.LogError($"   - GameObject: {gameObject.name}");
                Debug.LogError($"   - All Components: {string.Join(", ", GetComponents<Component>().Select(c => c.GetType().Name))}");
            }
            else
            {
                Debug.Log($"✅ LEDGraphManager: Found");
                try
                {
                    Debug.Log($"   - Data Loaded: {graphManager.DataLoaded}");
                    Debug.Log($"   - Node Count: {graphManager.NodeCount}");
                    Debug.Log($"   - Edge Count: {graphManager.EdgeCount}");
                    Debug.Log($"   - File Path: {graphManager.grasshopperDataPath}");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"   - Error accessing LEDGraphManager properties: {e.Message}");
                }
            }
            
            if (animationSystem == null)
            {
                Debug.LogError("❌ LEDAnimationSystem: MISSING");
            }
            else
            {
                Debug.Log($"✅ LEDAnimationSystem: Found");
                Debug.Log($"   - Is Playing: {animationSystem.isPlaying}");
                Debug.Log($"   - Animation Count: {animationSystem.AnimationCount}");
                Debug.Log($"   - Current Animation: {animationSystem.CurrentAnimationName}");
            }
            
            // Check visualization status
            Debug.Log($"🎮 Game View Status:");
            Debug.Log($"   - Created: {gameViewCreated}");
            Debug.Log($"   - LED Objects: {ledObjects.Count}");
            Debug.Log($"   - LED Renderers: {ledRenderers.Count}");
            Debug.Log($"   - Environment Applied: {environmentApplied}");
            
            // Check file existence
            if (graphManager != null)
            {
                string filePath = graphManager.grasshopperDataPath;
                bool fileExists = System.IO.File.Exists(filePath);
                Debug.Log($"📁 Grasshopper File: {(fileExists ? "EXISTS" : "MISSING")} at {filePath}");
            }
        }
    }
}
