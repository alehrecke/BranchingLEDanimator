using UnityEngine;
using BranchingLEDAnimator.Core;
using BranchingLEDAnimator.Visualization;

namespace BranchingLEDAnimator.UI
{
    /// <summary>
    /// Simple, clean UI control panel for the LED animation system
    /// All components are on the same GameObject - no searching required
    /// </summary>
    public class LEDControlPanel : MonoBehaviour
    {
        [Header("UI Settings")]
        public bool showUI = true;
        public bool isCollapsed = false; // Collapsed state - shows minimal bar
        public float uiWidth = 300f;
        public float uiHeight = 400f;
        public float collapsedWidth = 140f;
        public float collapsedHeight = 30f;
        
        [Header("Debug")]
        public bool showDebugInfo = false;
        
        // Component references (all on same GameObject)
        private LEDGraphManager graphManager;
        private LEDAnimationSystem animationSystem;
        private LEDSceneVisualizer sceneVisualizer;
        private LEDGameVisualizer gameVisualizer;
        
        // UI state
        private bool showAdvancedControls = false;
        private bool showAnimationList = false; // Expandable animation selector
        private Vector2 animationScrollPos = Vector2.zero; // Scroll position for animation list
        private Rect animationDropdownRect; // Position for floating dropdown
        
        void Start()
        {
            // Get all components from same GameObject
            graphManager = GetComponent<LEDGraphManager>();
            animationSystem = GetComponent<LEDAnimationSystem>();
            sceneVisualizer = GetComponent<LEDSceneVisualizer>();
            gameVisualizer = GetComponent<LEDGameVisualizer>();
            
            // Validate required components
            if (graphManager == null)
            {
                Debug.LogError("LEDControlPanel requires LEDGraphManager on the same GameObject!");
            }
            if (animationSystem == null)
            {
                Debug.LogError("LEDControlPanel requires LEDAnimationSystem on the same GameObject!");
            }
        }
        
        void OnGUI()
        {
            if (!showUI) return;
            
            // Handle click-outside-to-close for dropdown
            if (showAnimationList && Event.current.type == EventType.MouseDown)
            {
                // Check if click is outside dropdown area
                Vector2 mousePos = Event.current.mousePosition;
                float panelX = 10f;
                float dropdownX = panelX + animationDropdownRect.x;
                float dropdownY = 10f + animationDropdownRect.y + animationDropdownRect.height + 2f;
                
                string[] animationNames = animationSystem != null ? animationSystem.GetAnimationNames() : new string[0];
                int maxVisibleItems = 8;
                float itemHeight = 24f;
                float listHeight = Mathf.Min(animationNames.Length, maxVisibleItems) * itemHeight + 10f;
                
                Rect dropdownArea = new Rect(dropdownX, dropdownY, animationDropdownRect.width, listHeight);
                Rect buttonArea = new Rect(panelX + animationDropdownRect.x, 10f + animationDropdownRect.y, 
                                           animationDropdownRect.width, animationDropdownRect.height);
                
                if (!dropdownArea.Contains(mousePos) && !buttonArea.Contains(mousePos))
                {
                    showAnimationList = false;
                }
            }
            
            // Show collapsed or expanded UI
            if (isCollapsed)
            {
                DrawCollapsedUI();
            }
            else
            {
                DrawExpandedUI();
            }
        }
        
        /// <summary>
        /// Draw minimal collapsed UI bar
        /// </summary>
        private void DrawCollapsedUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, collapsedWidth + 60, collapsedHeight));
            GUILayout.BeginHorizontal("box");
            
            if (GUILayout.Button("▶ LED Controls", GUILayout.Height(24)))
            {
                isCollapsed = false;
            }
            
            // Show keyboard shortcut hint
            GUIStyle hintStyle = new GUIStyle(GUI.skin.label);
            hintStyle.fontSize = 10;
            hintStyle.normal.textColor = Color.gray;
            GUILayout.Label("(` key)", hintStyle, GUILayout.Height(24));
            
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
        
        /// <summary>
        /// Draw full expanded UI panel
        /// </summary>
        private void DrawExpandedUI()
        {
            // Create UI area
            GUILayout.BeginArea(new Rect(10, 10, uiWidth, uiHeight));
            GUILayout.BeginVertical("box");
            
            // Store panel offset for dropdown positioning
            float panelX = 10f;
            
            // Header with collapse button
            GUILayout.BeginHorizontal();
            GUILayout.Label("🎬 LED Animation System", GUI.skin.box, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("▼", GUILayout.Width(24), GUILayout.Height(20)))
            {
                isCollapsed = true;
            }
            GUILayout.EndHorizontal();
            
            // System status
            DrawSystemStatus();
            
            GUILayout.Space(10);
            
            // Import controls
            DrawImportControls();
            
            GUILayout.Space(10);
            
            // Animation controls (only if data is loaded)
            if (graphManager != null && graphManager.DataLoaded)
            {
                DrawAnimationControls();
                
                GUILayout.Space(10);
                
                // View controls
                DrawViewControls();
                
                GUILayout.Space(10);
                
                // Advanced controls (collapsible)
                DrawAdvancedControls();
            }
            
            GUILayout.EndVertical();
            GUILayout.EndArea();
            
            // Draw animation dropdown as floating overlay (outside main panel to avoid clipping)
            if (showAnimationList && animationSystem != null)
            {
                DrawAnimationDropdownOverlay(panelX);
            }
        }
        
        /// <summary>
        /// Draw the animation dropdown as a floating overlay
        /// </summary>
        private void DrawAnimationDropdownOverlay(float panelX)
        {
            string[] animationNames = animationSystem.GetAnimationNames();
            if (animationNames.Length == 0) return;
            
            // Position dropdown below the button (using stored rect)
            float dropdownX = panelX + animationDropdownRect.x;
            float dropdownY = 10f + animationDropdownRect.y + animationDropdownRect.height + 2f;
            float dropdownWidth = animationDropdownRect.width;
            
            // Calculate height based on items
            int maxVisibleItems = 8;
            float itemHeight = 24f;
            float listHeight = Mathf.Min(animationNames.Length, maxVisibleItems) * itemHeight + 10f;
            
            // Draw semi-transparent background for better visibility
            GUI.Box(new Rect(dropdownX - 2, dropdownY - 2, dropdownWidth + 4, listHeight + 4), "");
            
            GUILayout.BeginArea(new Rect(dropdownX, dropdownY, dropdownWidth, listHeight));
            
            // Scrollable list if many animations
            if (animationNames.Length > maxVisibleItems)
            {
                animationScrollPos = GUILayout.BeginScrollView(animationScrollPos, GUILayout.Height(listHeight - 6));
            }
            
            for (int i = 0; i < animationNames.Length; i++)
            {
                bool isSelected = (animationSystem.currentAnimationIndex == i);
                
                // Style for selected item
                GUIStyle itemStyle = new GUIStyle(isSelected ? GUI.skin.box : GUI.skin.button);
                string prefix = isSelected ? "● " : "  ";
                
                if (GUILayout.Button(prefix + animationNames[i], itemStyle, GUILayout.Height(itemHeight - 2)))
                {
                    animationSystem.SetAnimation(i);
                    showAnimationList = false; // Close dropdown after selection
                }
            }
            
            if (animationNames.Length > maxVisibleItems)
            {
                GUILayout.EndScrollView();
            }
            
            GUILayout.EndArea();
        }
        
        /// <summary>
        /// Draw system status indicators
        /// </summary>
        private void DrawSystemStatus()
        {
            GUILayout.Label("System Status:", GUI.skin.box);
            
            // Component status
            GUILayout.Label($"Graph Manager: {GetStatusIcon(graphManager != null)}");
            GUILayout.Label($"Animation System: {GetStatusIcon(animationSystem != null)}");
            GUILayout.Label($"Scene Visualizer: {GetStatusIcon(sceneVisualizer != null)}");
            GUILayout.Label($"Game Visualizer: {GetStatusIcon(gameVisualizer != null)}");
            
            // Data status
            if (graphManager != null)
            {
                GUILayout.Label($"Data Loaded: {GetStatusIcon(graphManager.DataLoaded)}");
                if (graphManager.DataLoaded)
                {
                    GUILayout.Label($"LEDs: {graphManager.NodeCount}, Connections: {graphManager.EdgeCount}");
                    GUILayout.Label($"Sources: {graphManager.SourceNodes.Count}");
                }
            }
        }
        
        /// <summary>
        /// Draw import controls
        /// </summary>
        private void DrawImportControls()
        {
            GUILayout.Label("Import Controls:", GUI.skin.box);
            
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Import Grasshopper Data"))
            {
                if (graphManager != null)
                {
                    bool success = graphManager.ImportFromGrasshopper();
                    if (success)
                    {
                        if (showDebugInfo)
                        {
                            Debug.Log("✓ Grasshopper data imported successfully");
                        }
                        
                        // Auto-focus Scene view
                        if (sceneVisualizer != null)
                        {
                            sceneVisualizer.FocusSceneView();
                        }
                        
                        // Auto-focus Game view camera if Game view exists
                        if (gameVisualizer != null)
                        {
                            gameVisualizer.FocusCameraOnGeometry();
                        }
                    }
                }
            }
            
            if (GUILayout.Button("Clear Data"))
            {
                if (graphManager != null)
                {
                    graphManager.ClearData();
                    if (gameVisualizer != null)
                    {
                        gameVisualizer.ClearGameView();
                    }
                }
            }
            GUILayout.EndHorizontal();
            
            // File path display
            if (graphManager != null)
            {
                GUILayout.Label($"File: {System.IO.Path.GetFileName(graphManager.grasshopperDataPath)}");
            }
        }
        
        /// <summary>
        /// Draw animation controls
        /// </summary>
        private void DrawAnimationControls()
        {
            GUILayout.Label("Animation Controls:", GUI.skin.box);
            
            if (animationSystem == null) return;
            
            // Animation selection - dropdown style
            string[] animationNames = animationSystem.GetAnimationNames();
            if (animationNames.Length > 0)
            {
                // Compact dropdown-style selector
                GUILayout.BeginHorizontal();
                GUILayout.Label("Animation:", GUILayout.Width(70));
                
                // Current selection button (acts as dropdown toggle)
                string currentName = animationSystem.CurrentAnimationName;
                string dropdownLabel = showAnimationList ? $"▲ {currentName}" : $"▼ {currentName}";
                if (GUILayout.Button(dropdownLabel))
                {
                    showAnimationList = !showAnimationList;
                }
                
                // Capture button rect for dropdown positioning (after the button is drawn)
                if (Event.current.type == EventType.Repaint)
                {
                    animationDropdownRect = GUILayoutUtility.GetLastRect();
                }
                
                GUILayout.EndHorizontal();
                
                // Show animation count info
                GUIStyle countStyle = new GUIStyle(GUI.skin.label);
                countStyle.fontSize = 10;
                countStyle.normal.textColor = Color.gray;
                GUILayout.Label($"({animationNames.Length} available)" + (showAnimationList ? " - click to select" : ""), countStyle);
            }
            else
            {
                GUILayout.Label("No animations available!");
                if (GUILayout.Button("Find Animation Assets"))
                {
                    animationSystem.FindAllAnimationAssets();
                }
                GUILayout.Label("Create assets via: Assets → Create → LED Animation");
            }
            
            GUILayout.Space(5);
            
            // Playback controls
            GUILayout.BeginHorizontal();
            
            string playButtonText = animationSystem.isPlaying ? "⏸ Pause" : "▶ Play";
            if (GUILayout.Button(playButtonText))
            {
                animationSystem.TogglePlayPause();
            }
            
            if (GUILayout.Button("⏹ Stop"))
            {
                animationSystem.Stop();
            }
            
            GUILayout.EndHorizontal();
            
            // Speed control
            GUILayout.BeginHorizontal();
            GUILayout.Label("Speed:", GUILayout.Width(50));
            animationSystem.globalSpeed = GUILayout.HorizontalSlider(animationSystem.globalSpeed, 0.1f, 3f);
            GUILayout.Label($"{animationSystem.globalSpeed:F1}x", GUILayout.Width(30));
            GUILayout.EndHorizontal();
            
            // Status
            string status = animationSystem.isPlaying ? "Playing" : "Stopped";
            GUILayout.Label($"Status: {status}");
        }
        
        /// <summary>
        /// Draw view controls
        /// </summary>
        private void DrawViewControls()
        {
            GUILayout.Label("View Controls:", GUI.skin.box);
            
            // Scene view controls
            if (sceneVisualizer != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Scene View:");
                
                sceneVisualizer.showInSceneView = GUILayout.Toggle(sceneVisualizer.showInSceneView, "Show");
                sceneVisualizer.showAnimatedColors = GUILayout.Toggle(sceneVisualizer.showAnimatedColors, "Animate");
                GUILayout.EndHorizontal();
                
                if (GUILayout.Button("Focus Scene View"))
                {
                    sceneVisualizer.FocusSceneView();
                }
            }
            
            GUILayout.Space(5);
            
            // Game view controls
            if (gameVisualizer != null)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Create Game View"))
                {
                    gameVisualizer.CreateGameView();
                    // Camera auto-focus is now built into CreateGameView()
                }
                
                if (GUILayout.Button("Clear Game View"))
                {
                    gameVisualizer.ClearGameView();
                }
                GUILayout.EndHorizontal();
                
                if (GUILayout.Button("Focus Camera"))
                {
                    gameVisualizer.FocusCameraOnGeometry();
                }
            }
        }
        
        /// <summary>
        /// Draw advanced controls (collapsible)
        /// </summary>
        private void DrawAdvancedControls()
        {
            string advancedLabel = showAdvancedControls ? "▼ Advanced Controls" : "▶ Advanced Controls";
            if (GUILayout.Button(advancedLabel))
            {
                showAdvancedControls = !showAdvancedControls;
            }
            
            if (showAdvancedControls)
            {
                GUILayout.BeginVertical("box");
                
                // Animation system settings
                if (animationSystem != null)
                {
                    GUILayout.Label("Animation Settings:");
                    animationSystem.useFixedTimeStep = GUILayout.Toggle(animationSystem.useFixedTimeStep, "Use Fixed Time Step");
                    
                    if (animationSystem.useFixedTimeStep)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Time Step:", GUILayout.Width(70));
                        animationSystem.fixedTimeStep = GUILayout.HorizontalSlider(animationSystem.fixedTimeStep, 0.01f, 0.5f);
                        GUILayout.Label($"{animationSystem.fixedTimeStep:F2}s", GUILayout.Width(40));
                        GUILayout.EndHorizontal();
                    }
                }
                
                GUILayout.Space(5);
                
                // Visualization settings
                if (sceneVisualizer != null)
                {
                    GUILayout.Label("Scene View Settings:");
                    sceneVisualizer.showConnections = GUILayout.Toggle(sceneVisualizer.showConnections, "Show Connections");
                    sceneVisualizer.showNodeLabels = GUILayout.Toggle(sceneVisualizer.showNodeLabels, "Show Node Labels");
                    sceneVisualizer.useWireframeSpheres = GUILayout.Toggle(sceneVisualizer.useWireframeSpheres, "Wireframe Spheres");
                }
                
                if (gameVisualizer != null)
                {
                    GUILayout.Label("Game View Settings:");
                    gameVisualizer.showConnections = GUILayout.Toggle(gameVisualizer.showConnections, "Show Connections");
                    gameVisualizer.useEmissiveMaterials = GUILayout.Toggle(gameVisualizer.useEmissiveMaterials, "Emissive Materials");
                    
                    if (gameVisualizer.useEmissiveMaterials)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Emission:", GUILayout.Width(60));
                        gameVisualizer.emissionIntensity = GUILayout.HorizontalSlider(gameVisualizer.emissionIntensity, 0.1f, 5f);
                        GUILayout.Label($"{gameVisualizer.emissionIntensity:F1}", GUILayout.Width(30));
                        GUILayout.EndHorizontal();
                    }
                }
                
                GUILayout.Space(5);
                
                // Debug controls
                GUILayout.Label("Debug:");
                showDebugInfo = GUILayout.Toggle(showDebugInfo, "Show Debug Info");
                
                if (graphManager != null)
                {
                    graphManager.showDebugInfo = GUILayout.Toggle(graphManager.showDebugInfo, "Graph Debug");
                }
                if (animationSystem != null)
                {
                    animationSystem.showDebugInfo = GUILayout.Toggle(animationSystem.showDebugInfo, "Animation Debug");
                }
                
                GUILayout.EndVertical();
            }
        }
        
        /// <summary>
        /// Get status icon for boolean values
        /// </summary>
        private string GetStatusIcon(bool status)
        {
            return status ? "✓" : "✗";
        }
        
        /// <summary>
        /// Toggle UI visibility
        /// </summary>
        public void ToggleUI()
        {
            showUI = !showUI;
        }
        
        /// <summary>
        /// Toggle collapsed/expanded state
        /// </summary>
        public void ToggleCollapse()
        {
            isCollapsed = !isCollapsed;
        }
        
        // Keyboard shortcuts
        void Update()
        {
            // Tab - Hide UI completely
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                ToggleUI();
            }
            
            // Backtick/Tilde - Collapse/Expand UI
            if (Input.GetKeyDown(KeyCode.BackQuote))
            {
                ToggleCollapse();
            }
            
            if (Input.GetKeyDown(KeyCode.Space) && animationSystem != null)
            {
                animationSystem.TogglePlayPause();
            }
            
            if (Input.GetKeyDown(KeyCode.R) && animationSystem != null)
            {
                animationSystem.ResetAnimation();
            }
        }
    }
}
