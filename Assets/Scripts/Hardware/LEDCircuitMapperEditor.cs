#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System;
using BranchingLEDAnimator.Core;

namespace BranchingLEDAnimator.Hardware
{
    /// <summary>
    /// Visual editor for LED circuit mapping with direction control and preview
    /// </summary>
    [CustomEditor(typeof(LEDCircuitMapper))]
    public class LEDCircuitMapperEditor : Editor
    {
        private LEDCircuitMapper mapper;
        private LEDGraphManager graphManager;
        
        // Visual settings
        private bool showMappingPreview = true;
        private bool showDirectionArrows = true;
        private bool showLEDNumbers = false;
        private bool showNodeNumbers = false;
        private bool showBranchOverlay = false;
        private Vector2 previewScrollPosition;
        
        // Visual customization
        private float arrowSize = 0.8f;       // Arrow size for direction indicators
        private float ledPointSize = 0.15f;   // LED sphere size
        
        // Mapping customization
        private bool showAdvancedSettings = false;
        private bool showBranchConfiguration = true;
        
        // Branch analysis
        private List<List<int>> detectedBranches = new List<List<int>>();
        private bool branchAnalysisValid = false;
        
        void OnEnable()
        {
            mapper = (LEDCircuitMapper)target;
            graphManager = mapper.GetComponent<LEDGraphManager>();
        }
        
        public override void OnInspectorGUI()
        {
            // Header
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("LED Circuit Mapping Tool", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Workflow: Load Geometry → Configure Strips → Compile Mapping → Export Animations", MessageType.Info);
            
            EditorGUILayout.Space();
            EditorGUILayout.Separator();
            
            // Step 1: Geometry & Status
            DrawGeometryStatusSection();
            EditorGUILayout.Space();
            
            // Step 2: Visual Preview (to see what we're working with)
            DrawVisualPreviewSection();
            EditorGUILayout.Space();
            
            // Step 3: Strip Configuration (the main work)
            DrawBranchConfigurationSection();
            EditorGUILayout.Space();
            
            // Step 4: Compilation & Export Actions
            DrawCompilationSection();
            EditorGUILayout.Space();
            
            // Step 5: Final Strip Assignment (after compilation)
            if (mapper.MappingComplete)
            {
                DrawFinalStripAssignmentSection();
                EditorGUILayout.Space();
            }
            
            // Apply changes
            if (GUI.changed)
            {
                EditorUtility.SetDirty(mapper);
            }
        }
        
        void DrawGeometryStatusSection()
        {
            EditorGUILayout.LabelField("📐 Step 1: Geometry & Status", EditorStyles.boldLabel);
            
            EditorGUI.BeginDisabledGroup(true);
            
            // Graph manager status
            if (graphManager != null)
            {
                EditorGUILayout.TextField("Grasshopper Geometry", graphManager.DataLoaded ? 
                    $"✅ Loaded ({graphManager.NodeCount} nodes, {graphManager.PolylineCount} polylines)" : "❌ No data loaded");
            }
            else
            {
                EditorGUILayout.TextField("Graph Manager", "❌ Component not found");
            }
            
            // Show polyline summary if available
            if (graphManager != null && graphManager.DataLoaded)
            {
                EditorGUILayout.TextField("Polylines Available", $"{graphManager.PolylineCount} polylines ready for mapping");
            }
            
            // Mapping status
            EditorGUILayout.TextField("LED Mapping Status", mapper.MappingComplete ? 
                $"✅ Complete ({mapper.StripCount} strips, {mapper.TotalLEDCount} LEDs)" : "❌ Not compiled yet");
            
            if (mapper.MappingComplete)
            {
                EditorGUILayout.TextField("Export Status", "🎬 Ready for animation export!");
            }
            else if (mapper.LEDStrips.Count > 0)
            {
                EditorGUILayout.TextField("Next Step", "⚡ Use Compile Final LED Order");
            }
            else if (graphManager != null && graphManager.DataLoaded)
            {
                EditorGUILayout.TextField("Next Step", "📐 Import polylines from geometry");
            }
            
            EditorGUI.EndDisabledGroup();
            
            // Quick geometry reload button
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.2f, 0.6f, 1f);
            if (GUILayout.Button("🔄 Reload Geometry from Grasshopper", GUILayout.Height(25)))
            {
                ReloadGeometryFromGrasshopper();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }
        
        void DrawCompilationSection()
        {
            EditorGUILayout.LabelField("⚡ Step 4: Compilation & Actions", EditorStyles.boldLabel);
            
            // Test and presets row
            EditorGUILayout.BeginHorizontal();
            
            // Test polyline mapping button
            GUI.backgroundColor = Color.cyan;
            EditorGUI.BeginDisabledGroup(mapper.LEDStrips.Count == 0);
            if (GUILayout.Button("🧪 Test Configuration", GUILayout.Height(25)))
            {
                TestPolylineMapping();
            }
            EditorGUI.EndDisabledGroup();
            
            if (GUILayout.Button("Standard Direction", GUILayout.Height(25)))
            {
                ApplyStandardDirectionPreset();
            }
            if (GUILayout.Button("Reverse All", GUILayout.Height(25)))
            {
                ApplyReverseAllPreset();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            // Main compilation row
            EditorGUILayout.BeginHorizontal();
            
            // Compile final LED order button (main action)
            GUI.backgroundColor = mapper.MappingComplete ? Color.yellow : Color.green;
            EditorGUI.BeginDisabledGroup(mapper.LEDStrips.Count == 0);
            if (GUILayout.Button(mapper.MappingComplete ? "♻️ Recompile LED Order" : "⚡ Compile Final LED Order", GUILayout.Height(35)))
            {
                CompileFinalLEDMapping();
            }
            EditorGUI.EndDisabledGroup();
            
            // Export mapping button
            GUI.backgroundColor = new Color(0.8f, 0.4f, 0.8f); // Purple
            EditorGUI.BeginDisabledGroup(!mapper.MappingComplete);
            if (GUILayout.Button("📤 Export Config", GUILayout.Height(35)))
            {
                ExportMappingConfiguration();
            }
            EditorGUI.EndDisabledGroup();
            
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
            
            // Debug row: Force mapping completion for testing
            if (!mapper.MappingComplete && mapper.LEDStrips.Count > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.BeginHorizontal();
                GUI.backgroundColor = Color.red;
                if (GUILayout.Button("🔧 DEBUG: Force Mapping Complete", GUILayout.Height(20)))
                {
                    mapper.SetMappingComplete(true);
                    EditorUtility.SetDirty(mapper);
                    Debug.LogWarning("⚠️ DEBUG: Forced mapping completion - this is for testing only!");
                    Debug.LogWarning("⚠️ Use '⚡ Compile Final LED Order' for proper mapping compilation.");
                }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();
            }
        }
        
        void DrawVisualPreviewSection()
        {
            showMappingPreview = EditorGUILayout.Foldout(showMappingPreview, "👁️ Step 2: Visual Preview", true);
            
            if (showMappingPreview)
            {
                EditorGUILayout.BeginVertical(GUI.skin.box);
                
                // Preview options
                EditorGUILayout.BeginHorizontal();
                bool oldShowArrows = showDirectionArrows;
                showDirectionArrows = EditorGUILayout.Toggle("Direction Arrows", showDirectionArrows);
                bool oldShowLEDNumbers = showLEDNumbers;
                showLEDNumbers = EditorGUILayout.Toggle("LED Numbers", showLEDNumbers);
                bool oldShowNodeNumbers = showNodeNumbers;
                showNodeNumbers = EditorGUILayout.Toggle("Node Numbers", showNodeNumbers);
                EditorGUILayout.EndHorizontal();
                
                // Auto-refresh when preview options change
                if (oldShowArrows != showDirectionArrows || oldShowLEDNumbers != showLEDNumbers || oldShowNodeNumbers != showNodeNumbers)
                {
                    SceneView.RepaintAll();
                }
                
                EditorGUILayout.BeginHorizontal();
                showBranchOverlay = EditorGUILayout.Toggle("Branch Overlay", showBranchOverlay);
                EditorGUILayout.EndHorizontal();
                
                // Visual settings with live update
                EditorGUILayout.LabelField("Visual Settings", EditorStyles.miniBoldLabel);
                float oldArrowSize = arrowSize;
                arrowSize = EditorGUILayout.Slider("Arrow Size", arrowSize, 0.2f, 10f);
                float oldLEDSize = ledPointSize;
                ledPointSize = EditorGUILayout.Slider("LED Sphere Size", ledPointSize, 0.05f, 0.8f);
                
                // Auto-refresh Scene View when visual settings change
                if (oldArrowSize != arrowSize || oldLEDSize != ledPointSize)
                {
                    SceneView.RepaintAll();
                }
                
                EditorGUILayout.Space();
                
                if (mapper.MappingComplete)
                {
                    DrawMappingPreview();
                }
                else
                {
                    EditorGUILayout.HelpBox("Create mapping first to see preview", MessageType.Warning);
                }
                
                EditorGUILayout.EndVertical();
            }
        }
        
        void DrawBranchConfigurationSection()
        {
            showBranchConfiguration = EditorGUILayout.Foldout(showBranchConfiguration, "⚙️ Step 3: Strip Configuration", true);
            
            if (showBranchConfiguration)
            {
                EditorGUILayout.BeginVertical(GUI.skin.box);
                EditorGUILayout.HelpBox("Configure individual polylines from your Grasshopper geometry. Each polyline becomes a separate LED strip that you can configure with custom LED counts, directions, and wiring order.", MessageType.Info);
                
                // Import polylines button  
                EditorGUILayout.BeginHorizontal();
                GUI.backgroundColor = new Color(0.3f, 0.7f, 1f);
                if (GUILayout.Button("📐 Import Polylines from Geometry", GUILayout.Height(25)))
                {
                    ImportPolylinesFromGeometry();
                }
                GUI.backgroundColor = Color.white;
                
                if (GUILayout.Button("➕ Add Branch", GUILayout.Width(100)))
                {
                    AddNewBranch();
                }
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.Space();
                
                // Branch list
                if (mapper.LEDStrips != null && mapper.LEDStrips.Count > 0)
                {
                    for (int i = 0; i < mapper.LEDStrips.Count; i++)
                    {
                        DrawBranchConfiguration(i, mapper.LEDStrips[i]);
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("No polylines configured. Use 'Import Polylines from Geometry' to load your 9 Grasshopper polylines or 'Add Branch' to create manually.", MessageType.Warning);
                }
                
                EditorGUILayout.EndVertical();
            }
        }
        
        void DrawAdvancedMappingSection()
        {
            showAdvancedSettings = EditorGUILayout.Foldout(showAdvancedSettings, "Advanced Mapping Customization", true);
            
            if (showAdvancedSettings)
            {
                EditorGUILayout.BeginVertical(GUI.skin.box);
                EditorGUILayout.HelpBox("Customize direction and offset for each strip to match your physical wiring.", MessageType.Info);
                
                for (int i = 0; i < mapper.numLogicalStrips; i++)
                {
                    EditorGUILayout.BeginVertical(GUI.skin.box);
                    EditorGUILayout.LabelField($"Strip {i} Configuration", EditorStyles.boldLabel);
                    
                    EditorGUILayout.BeginHorizontal();
                    
                    // Direction toggle
                    var stripInfo = mapper.LEDStrips[i];
                    bool wasReversed = stripInfo.reverseDirection;
                    stripInfo.reverseDirection = EditorGUILayout.Toggle("Reverse Direction", stripInfo.reverseDirection);
                    
                    // Offset control
                    stripInfo.startOffset = EditorGUILayout.IntSlider("Start Offset", stripInfo.startOffset, 0, mapper.ledsPerStrip - 1);
                    
                    EditorGUILayout.EndHorizontal();
                    
                    // Visual indicator
                    DrawStripPreview(i, stripInfo.reverseDirection, stripInfo.startOffset);
                    
                    EditorGUILayout.EndVertical();
                }
                
                EditorGUILayout.Space();
                
                // Apply customizations button
                GUI.backgroundColor = new Color(1f, 0.5f, 0f); // Orange color
                // REMOVED: Legacy "Apply Customizations" button - now handled automatically
                GUI.backgroundColor = Color.white;
                
                EditorGUILayout.EndVertical();
            }
        }
        
        void DrawMappingPreview()
        {
            EditorGUILayout.HelpBox("💡 View LED configuration in Scene View with live direction arrows and LED positioning based on your custom LED counts!", MessageType.Info);
            
            if (mapper.LEDStrips != null && mapper.LEDStrips.Count > 0)
            {
                EditorGUILayout.LabelField($"📊 Total Strips: {mapper.LEDStrips.Count} | Total LEDs: {mapper.LEDStrips.Sum(s => s.maxLEDsPerBranch)}", EditorStyles.miniLabel);
            }
        }
        
        void DrawLEDPreview(int stripIndex, int ledIndex)
        {
            // Calculate physical LED position
            int physicalLEDIndex = (stripIndex * mapper.ledsPerStrip) + ledIndex;
            
            // Apply direction correction if enabled
            var stripInfo = mapper.LEDStrips[stripIndex];
            if (stripInfo.reverseDirection)
            {
                int reversedPosition = (mapper.ledsPerStrip - 1) - ledIndex;
                physicalLEDIndex = (stripIndex * mapper.ledsPerStrip) + reversedPosition;
            }
            
            // Apply offset
            physicalLEDIndex = (physicalLEDIndex + stripInfo.startOffset) % mapper.ledsPerStrip;
            physicalLEDIndex += stripIndex * mapper.ledsPerStrip;
            
            // Color coding
            Color ledColor = Color.gray;
            if (physicalLEDIndex % 3 == 0) ledColor = Color.red;
            else if (physicalLEDIndex % 3 == 1) ledColor = Color.green;
            else ledColor = Color.blue;
            
            // Draw LED box
            GUI.backgroundColor = ledColor;
            string ledText = showLEDNumbers ? physicalLEDIndex.ToString() : "●";
            if (showNodeNumbers && mapper.HasNodeAtLED(stripIndex, ledIndex))
            {
                int nodeIndex = mapper.GetNodeAtLED(stripIndex, ledIndex);
                ledText += $"\n{nodeIndex}";
            }
            
            GUILayout.Button(ledText, GUILayout.Width(30), GUILayout.Height(30));
            GUI.backgroundColor = Color.white;
        }
        
        void DrawStripPreview(int stripIndex, bool reversed, int offset)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Preview:", GUILayout.Width(60));
            
            // Show first few LEDs of this strip
            for (int i = 0; i < Mathf.Min(8, mapper.ledsPerStrip); i++)
            {
                int displayIndex = reversed ? (mapper.ledsPerStrip - 1 - i) : i;
                displayIndex = (displayIndex + offset) % mapper.ledsPerStrip;
                
                Color color = displayIndex % 2 == 0 ? Color.cyan : Color.magenta;
                GUI.backgroundColor = color;
                GUILayout.Button(displayIndex.ToString(), GUILayout.Width(25), GUILayout.Height(20));
            }
            
            GUI.backgroundColor = Color.white;
            
            if (mapper.ledsPerStrip > 8)
            {
                EditorGUILayout.LabelField("...", GUILayout.Width(20));
            }
            
            // Direction indicator
            GUIStyle dirStyle = new GUIStyle(GUI.skin.label);
            dirStyle.normal.textColor = reversed ? Color.red : Color.green;
            dirStyle.fontSize = 14;
            EditorGUILayout.LabelField(reversed ? "← REVERSED" : "→ NORMAL", dirStyle);
            
            EditorGUILayout.EndHorizontal();
        }
        
        void CreateMappingWithCustomizations()
        {
            if (graphManager == null || !graphManager.DataLoaded)
            {
                EditorUtility.DisplayDialog("Error", "No graph data loaded. Import Grasshopper data first.", "OK");
                return;
            }
            
            Debug.Log("🔧 Creating LED mapping with visual customizations...");
            mapper.CreateLEDMapping();
            
            if (mapper.MappingComplete)
            {
                Debug.Log("✅ LED mapping created - direction settings handled automatically");
            }
        }
        
        // REMOVED: Legacy ApplyCustomizations method - direction is now handled directly in ConvertColorsToLEDData
        
        void ApplyStandardDirectionPreset()
        {
            for (int i = 0; i < mapper.LEDStrips.Count; i++)
            {
                mapper.LEDStrips[i].reverseDirection = false;
                mapper.LEDStrips[i].startOffset = 0;
            }
            Debug.Log("📐 Applied standard direction preset");
            EditorUtility.SetDirty(mapper);
        }
        
        void ApplyReverseAllPreset()
        {
            for (int i = 0; i < mapper.LEDStrips.Count; i++)
            {
                mapper.LEDStrips[i].reverseDirection = true;
                mapper.LEDStrips[i].startOffset = 0;
            }
            Debug.Log("🔄 Applied reverse all strips preset");
            EditorUtility.SetDirty(mapper);
        }
        
        void TestCurrentMapping()
        {
            if (!mapper.MappingComplete) return;
            
            Debug.Log("🧪 Testing current LED mapping...");
            
            // Find ESP32 communicator
            var esp32 = mapper.GetComponent<ESP32Communicator>();
            if (esp32 != null)
            {
                if (esp32.IsConnected)
                {
                    // Send test pattern to verify mapping
                    esp32.TestConnection();
                    Debug.Log("✅ Test pattern sent to ESP32");
                }
                else
                {
                    Debug.LogWarning("⚠️ ESP32 not connected. Connect first to test mapping.");
                }
            }
            else
            {
                Debug.LogWarning("⚠️ ESP32Communicator not found on same GameObject");
            }
        }
        
        void TestPolylineMapping()
        {
            Debug.Log("🧪 Testing polyline mapping...");
            
            if (mapper.LEDStrips.Count == 0)
            {
                Debug.LogWarning("No polylines configured! Import polylines first.");
                return;
            }
            
            var graphManager = mapper.GetComponent<LEDGraphManager>();
            if (graphManager == null || graphManager.NodePositions.Count == 0)
            {
                Debug.LogWarning("No geometry loaded! Reload geometry first.");
                return;
            }
            
            // Test each polyline configuration
            int totalLEDs = 0;
            for (int i = 0; i < mapper.LEDStrips.Count; i++)
            {
                var strip = mapper.LEDStrips[i];
                Debug.Log($"Strip {i}: {strip.branchName}");
                Debug.Log($"  - LEDs: {strip.maxLEDsPerBranch}");
                Debug.Log($"  - Direction: {(strip.reverseDirection ? "REVERSED" : "NORMAL")}");
                Debug.Log($"  - Wiring Order: {strip.wiringOrder}");
                Debug.Log($"  - Enabled: {strip.enabled}");
                
                if (strip.enabled)
                    totalLEDs += strip.maxLEDsPerBranch;
            }
            
            Debug.Log($"✅ Test complete! Total LEDs: {totalLEDs}");
            Debug.Log($"📏 Physical wiring length needed: ~{totalLEDs * 0.3f:F1}m (assuming 30cm spacing)");
        }
        
        void CompileFinalLEDMapping()
        {
            Debug.Log("⚡ Compiling final LED mapping...");
            
            if (mapper.LEDStrips.Count == 0)
            {
                Debug.LogWarning("No polylines configured! Import polylines first.");
                return;
            }
            
            var graphManager = mapper.GetComponent<LEDGraphManager>();
            if (graphManager == null)
            {
                Debug.LogError("LEDGraphManager component not found!");
                return;
            }
            
            // Sort strips by wiring order
            var sortedStrips = mapper.LEDStrips
                .Where(s => s.enabled)
                .OrderBy(s => s.wiringOrder)
                .ToList();
            
            // Build the final LED address mapping
            mapper.ClearLEDMapping(); // Clear existing mapping
            
            int globalLEDIndex = 0;
            
            for (int stripIndex = 0; stripIndex < sortedStrips.Count; stripIndex++)
            {
                var strip = sortedStrips[stripIndex];
                
                Debug.Log($"Processing Strip {stripIndex}: {strip.branchName} (Order: {strip.wiringOrder})");
                
                // Calculate LED positions for this strip
                var ledPositions = CalculateLEDPositions(strip, graphManager.NodePositions);
                
                // Map each LED position to a global LED index
                for (int ledIndex = 0; ledIndex < ledPositions.Count; ledIndex++)
                {
                    int actualLEDIndex = strip.reverseDirection ? 
                        (ledPositions.Count - 1 - ledIndex) : ledIndex;
                    
                    Vector3 ledPosition = ledPositions[actualLEDIndex];
                    
                    // Find the closest graph node to this LED position
                    int closestNodeIndex = FindClosestNode(ledPosition, graphManager.NodePositions);
                    
                    // Add to mapping: graph node -> physical LED address
                    mapper.AddLEDMapping(closestNodeIndex, globalLEDIndex);
                    
                    globalLEDIndex++;
                }
                
                Debug.Log($"  Mapped {ledPositions.Count} LEDs (indices {globalLEDIndex - ledPositions.Count} to {globalLEDIndex - 1})");
            }
            
            // Update the mapper's internal state
            mapper.SetMappingComplete(true);
            mapper.SetTotalLEDCount(globalLEDIndex);
            
            // Mark the mapper as dirty to ensure changes are saved in Edit mode
            EditorUtility.SetDirty(mapper);
            
            Debug.Log($"✅ Final LED mapping compiled!");
            Debug.Log($"📊 Summary:");
            Debug.Log($"   - Total strips: {sortedStrips.Count}");
            Debug.Log($"   - Total LEDs: {globalLEDIndex}");
            Debug.Log($"   - GPIO Pin: 19 (continuous strip)");
            Debug.Log($"   - Ready for animations!");
            Debug.Log($"💾 Mapping saved - export will work in both Edit and Play mode!");
        }
        
        void ExportMappingConfiguration()
        {
            Debug.Log("📤 Exporting mapping configuration...");
            
            if (!mapper.MappingComplete)
            {
                Debug.LogWarning("Mapping not complete! Compile final LED order first.");
                return;
            }
            
            // Calculate actual totals from enabled strips
            var enabledStrips = mapper.LEDStrips.Where(s => s.enabled).ToList();
            int actualTotalLEDs = enabledStrips.Sum(s => s.maxLEDsPerBranch);
            
            // Create a summary of the mapping configuration
            var config = new System.Text.StringBuilder();
            config.AppendLine("=== LED Circuit Mapping Configuration ===");
            config.AppendLine($"Total LEDs: {actualTotalLEDs}");
            config.AppendLine($"Total Strips: {enabledStrips.Count}");
            config.AppendLine($"GPIO Pin: 19");
            config.AppendLine();
            
            config.AppendLine("Strip Configuration (by wiring order):");
            var sortedStrips = mapper.LEDStrips
                .Where(s => s.enabled)
                .OrderBy(s => s.wiringOrder)
                .ToList();
            
            int startIndex = 0;
            for (int i = 0; i < sortedStrips.Count; i++)
            {
                var strip = sortedStrips[i];
                int endIndex = startIndex + strip.maxLEDsPerBranch - 1;
                
                config.AppendLine($"  {i + 1}. {strip.branchName}:");
                config.AppendLine($"     LEDs: {strip.maxLEDsPerBranch} (addresses {startIndex}-{endIndex})");
                config.AppendLine($"     Direction: {(strip.reverseDirection ? "REVERSED" : "NORMAL")}");
                config.AppendLine($"     Wiring Order: {strip.wiringOrder}");
                
                startIndex = endIndex + 1;
            }
            
            string configText = config.ToString();
            Debug.Log(configText);
            
            // Copy to clipboard
            EditorGUIUtility.systemCopyBuffer = configText;
            Debug.Log("📋 Configuration copied to clipboard!");
        }
        
        int FindClosestNode(Vector3 position, List<Vector3> nodePositions)
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
        
        void DrawFinalStripAssignmentSection()
        {
            EditorGUILayout.LabelField("🎯 Step 5: Final Strip Assignment", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.HelpBox("Mapping complete! Here's your final LED strip assignment for ESP32:", MessageType.Info);
            
            // Summary header
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.TextField("Total LEDs", mapper.TotalLEDCount.ToString());
            EditorGUILayout.TextField("GPIO Pin", "19");
            EditorGUILayout.TextField("Wiring", "Continuous strip");
            EditorGUILayout.EndHorizontal();
            EditorGUI.EndDisabledGroup();
            
            EditorGUILayout.Space();
            
            // Show final strip order
            var sortedStrips = mapper.LEDStrips
                .Where(s => s.enabled)
                .OrderBy(s => s.wiringOrder)
                .ToList();
            
            EditorGUILayout.LabelField("Physical LED Assignment (Wiring Order):", EditorStyles.boldLabel);
            
            int startIndex = 0;
            for (int i = 0; i < sortedStrips.Count; i++)
            {
                var strip = sortedStrips[i];
                int endIndex = startIndex + strip.maxLEDsPerBranch - 1;
                
                EditorGUILayout.BeginHorizontal();
                
                // Strip info
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.TextField($"Strip {i + 1}", $"{strip.branchName}");
                EditorGUILayout.TextField("LEDs", $"{startIndex}-{endIndex} ({strip.maxLEDsPerBranch} total)");
                EditorGUILayout.TextField("Direction", strip.reverseDirection ? "REVERSED" : "NORMAL");
                EditorGUI.EndDisabledGroup();
                
                // Visual indicator
                GUIStyle colorStyle = new GUIStyle(GUI.skin.button);
                colorStyle.normal.background = MakeTexture(2, 2, strip.visualColor);
                GUILayout.Button("", colorStyle, GUILayout.Width(20), GUILayout.Height(20));
                
                EditorGUILayout.EndHorizontal();
                
                startIndex = endIndex + 1;
            }
            
            EditorGUILayout.Space();
            
            // Action buttons
            EditorGUILayout.BeginHorizontal();
            
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("🎬 Ready for Animation Export!", GUILayout.Height(30)))
            {
                Debug.Log("✅ LED Circuit Mapper: Ready for animation export!");
                Debug.Log("💡 Next steps:");
                Debug.Log("   1. Go to ESP32 Animation Exporter component");
                Debug.Log("   2. Select an animation to export");  
                Debug.Log("   3. Click 'Export Current Animation'");
                Debug.Log("   4. Animation will be uploaded to ESP32 with your mapping!");
            }
            
            GUI.backgroundColor = new Color(0.8f, 0.4f, 0.8f);
            if (GUILayout.Button("📋 Copy to Clipboard", GUILayout.Height(30)))
            {
                ExportMappingConfiguration();
            }
            
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
        }
        
        // Helper method to create colored textures for strip indicators
        private Texture2D MakeTexture(int width, int height, Color color)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++)
                pix[i] = color;
            
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }
        
        // Scene view visualization
        void OnSceneGUI()
        {
            if (!showMappingPreview || graphManager == null) return;
            
            var nodePositions = graphManager.NodePositions;
            if (nodePositions == null || nodePositions.Count == 0) return;
            
            // Debug info in Scene View
            if (Event.current.type == EventType.Repaint)
            {
                var style = new GUIStyle();
                style.normal.textColor = Color.white;
                style.fontSize = 12;
                
                string debugInfo = $"Direction Arrows: {(showDirectionArrows ? "ON" : "OFF")}\n";
                debugInfo += $"Branch Overlay: {(showBranchOverlay ? "ON" : "OFF")}\n";
                debugInfo += $"Strips: {(mapper.LEDStrips?.Count ?? 0)}";
                
                Handles.Label(Vector3.zero, debugInfo, style);
            }
            
            // Draw unified LED visualization (replaces old dual system)
            if (showBranchOverlay && mapper.LEDStrips != null)
            {
                DrawLEDStripVisualization(nodePositions);
            }
        }
        
        // REMOVED: Old DrawStripInScene and GetStripColor methods (replaced by unified LED visualization)
        
        void DrawLEDStripVisualization(List<Vector3> nodePositions)
        {
            for (int i = 0; i < mapper.LEDStrips.Count; i++)
            {
                var stripInfo = mapper.LEDStrips[i];
                if (!stripInfo.enabled) continue;

                Handles.color = stripInfo.visualColor;
                
                // Calculate actual LED positions along the polyline
                var ledPositions = CalculateLEDPositions(stripInfo, nodePositions);
                
                // Draw LED spheres at calculated positions
                for (int ledIndex = 0; ledIndex < ledPositions.Count; ledIndex++)
                {
                    Vector3 ledPos = ledPositions[ledIndex];
                    
                    // Draw LED sphere
                    Handles.SphereHandleCap(0, ledPos, Quaternion.identity, ledPointSize, EventType.Repaint);
                    
                    // Show LED number if enabled
                    if (showLEDNumbers)
                    {
                        // Adjust LED index based on direction
                        int displayIndex = stripInfo.reverseDirection ? (ledPositions.Count - 1 - ledIndex) : ledIndex;
                        Handles.Label(ledPos + Vector3.up * (ledPointSize + 0.05f), 
                                    displayIndex.ToString(),
                                    new GUIStyle { fontSize = 10, normal = { textColor = stripInfo.visualColor } });
                    }
                    
                    // Show original node numbers if enabled (for debugging)
                    if (showNodeNumbers && ledIndex < stripInfo.nodeIndices.Count)
                    {
                        int originalNodeIndex = stripInfo.nodeIndices[Mathf.RoundToInt((float)ledIndex / ledPositions.Count * stripInfo.nodeIndices.Count)];
                        Handles.Label(ledPos + Vector3.down * (ledPointSize + 0.05f), 
                                    $"N{originalNodeIndex}",
                                    new GUIStyle { fontSize = 8, normal = { textColor = Color.gray } });
                    }
                }
                
                // Draw connections between LEDs
                if (ledPositions.Count > 1)
                {
                    for (int j = 0; j < ledPositions.Count - 1; j++)
                    {
                        Vector3 posA = ledPositions[j];
                        Vector3 posB = ledPositions[j + 1];
                        Handles.DrawLine(posA, posB);
                    }
                    
                    // Draw direction cone at the END of each polyline (easy to see!)
                    if (showDirectionArrows && ledPositions.Count > 1)
                    {
                        // Calculate the direction along the polyline
                        Vector3 direction;
                        Vector3 conePosition;
                        
                        if (stripInfo.reverseDirection)
                        {
                            // Reverse: cone at first LED, pointing backwards
                            direction = (ledPositions[0] - ledPositions[1]).normalized;
                            conePosition = ledPositions[0] + direction * arrowSize * 0.3f;
                        }
                        else
                        {
                            // Normal: cone at last LED, pointing forwards
                            direction = (ledPositions[ledPositions.Count - 1] - ledPositions[ledPositions.Count - 2]).normalized;
                            conePosition = ledPositions[ledPositions.Count - 1] + direction * arrowSize * 0.3f;
                        }
                        
                        // Draw a bright cone to show direction
                        Handles.color = Color.white; // Use white for high visibility
                        
                        // Draw the cone using ConeHandleCap
                        float coneSize = arrowSize * 0.8f;
                        Handles.ConeHandleCap(
                            0, 
                            conePosition, 
                            Quaternion.LookRotation(direction), 
                            coneSize, 
                            EventType.Repaint
                        );
                        
                        // Add a colored sphere at the base for strip identification
                        Handles.color = stripInfo.visualColor;
                        Vector3 spherePos = stripInfo.reverseDirection ? ledPositions[0] : ledPositions[ledPositions.Count - 1];
                        Handles.SphereHandleCap(0, spherePos, Quaternion.identity, ledPointSize * 1.5f, EventType.Repaint);
                    }
                }
                
                // Draw strip label at middle LED position
                if (ledPositions.Count > 0)
                {
                    int middleIndex = ledPositions.Count / 2;
                    Vector3 labelPos = ledPositions[middleIndex] + Vector3.up * 0.8f;
                    
                    string labelText = $"{stripInfo.branchName}\n{stripInfo.maxLEDsPerBranch} LEDs (Order: {stripInfo.wiringOrder})";
                    
                    Handles.Label(labelPos, labelText, 
                                new GUIStyle 
                                { 
                                    fontSize = 14,
                                    fontStyle = FontStyle.Bold,
                                    normal = { textColor = stripInfo.visualColor },
                                    alignment = TextAnchor.MiddleCenter
                                });
                }
            }
        }
        
        /// <summary>
        /// Calculate actual LED positions along a polyline based on LED count setting
        /// </summary>
        List<Vector3> CalculateLEDPositions(LEDStripInfo stripInfo, List<Vector3> nodePositions)
        {
            var ledPositions = new List<Vector3>();
            
            if (stripInfo.nodeIndices.Count < 2)
                return ledPositions;
            
            // Get the actual polyline points
            var polylinePoints = new List<Vector3>();
            foreach (int nodeIndex in stripInfo.nodeIndices)
            {
                if (nodeIndex < nodePositions.Count)
                {
                    polylinePoints.Add(nodePositions[nodeIndex]);
                }
            }
            
            if (polylinePoints.Count < 2)
                return ledPositions;
            
            // Distribute LEDs evenly along the polyline
            int ledCount = stripInfo.maxLEDsPerBranch;
            
            if (ledCount == 1)
            {
                // Single LED at start
                ledPositions.Add(polylinePoints[0]);
            }
            else
            {
                // Multiple LEDs distributed along length
                for (int i = 0; i < ledCount; i++)
                {
                    float t = (float)i / (ledCount - 1); // 0 to 1
                    Vector3 ledPos = GetPointAlongPolyline(polylinePoints, t);
                    ledPositions.Add(ledPos);
                }
            }
            
            return ledPositions;
        }
        
        /// <summary>
        /// Get a point along a polyline at parameter t (0 to 1)
        /// </summary>
        Vector3 GetPointAlongPolyline(List<Vector3> points, float t)
        {
            if (points.Count < 2) return points[0];
            
            // Calculate cumulative distances
            var distances = new List<float> { 0f };
            float totalDistance = 0f;
            
            for (int i = 0; i < points.Count - 1; i++)
            {
                float segmentDistance = Vector3.Distance(points[i], points[i + 1]);
                totalDistance += segmentDistance;
                distances.Add(totalDistance);
            }
            
            // Find target distance
            float targetDistance = t * totalDistance;
            
            // Find which segment contains this distance
            for (int i = 0; i < distances.Count - 1; i++)
            {
                if (targetDistance >= distances[i] && targetDistance <= distances[i + 1])
                {
                    // Interpolate within this segment
                    float segmentLength = distances[i + 1] - distances[i];
                    float segmentT = (targetDistance - distances[i]) / segmentLength;
                    return Vector3.Lerp(points[i], points[i + 1], segmentT);
                }
            }
            
            // Fallback to last point
            return points[points.Count - 1];
        }
        
        // Branch configuration methods
        void DrawBranchConfiguration(int index, LEDStripInfo stripInfo)
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            
            // Header with color indicator
            EditorGUILayout.BeginHorizontal();
            
            // Color indicator
            Rect colorRect = GUILayoutUtility.GetRect(20, 20, GUILayout.Width(20), GUILayout.Height(20));
            EditorGUI.DrawRect(colorRect, stripInfo.visualColor);
            
            // Branch name and enabled toggle
            stripInfo.enabled = EditorGUILayout.Toggle(stripInfo.enabled, GUILayout.Width(20));
            stripInfo.branchName = EditorGUILayout.TextField(stripInfo.branchName);
            
            // Delete button
            GUI.backgroundColor = new Color(1f, 0.3f, 0.3f);
            if (GUILayout.Button("🗑️", GUILayout.Width(30)))
            {
                DeleteBranch(index);
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }
            GUI.backgroundColor = Color.white;
            
            EditorGUILayout.EndHorizontal();
            
            if (stripInfo.enabled)
            {
                EditorGUI.indentLevel++;
                
                // Row 1: Wiring Order and LED Count
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Order:", GUILayout.Width(50));
                int oldOrder = stripInfo.wiringOrder;
                stripInfo.wiringOrder = EditorGUILayout.IntField(stripInfo.wiringOrder, GUILayout.Width(40));
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("LEDs:", GUILayout.Width(40));
                int oldLEDCount = stripInfo.maxLEDsPerBranch;
                stripInfo.maxLEDsPerBranch = EditorGUILayout.IntSlider(stripInfo.maxLEDsPerBranch, 1, 200, GUILayout.MinWidth(100));
                EditorGUILayout.EndHorizontal();
                
                // Auto-refresh Scene View when values change
                if (oldOrder != stripInfo.wiringOrder || oldLEDCount != stripInfo.maxLEDsPerBranch)
                {
                    SceneView.RepaintAll(); // Force Scene View refresh
                }
                
                // Row 2: Direction and GPIO
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Direction:", GUILayout.Width(60));
                bool oldReverse = stripInfo.reverseDirection;
                stripInfo.reverseDirection = EditorGUILayout.Toggle(stripInfo.reverseDirection, GUILayout.Width(20));
                EditorGUILayout.LabelField(stripInfo.reverseDirection ? "← Reverse" : "→ Normal", GUILayout.Width(80));
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("GPIO:", GUILayout.Width(40));
                stripInfo.dataPin = EditorGUILayout.IntField(stripInfo.dataPin, GUILayout.Width(40));
                EditorGUILayout.EndHorizontal();
                
                // Auto-refresh Scene View when direction changes
                if (oldReverse != stripInfo.reverseDirection)
                {
                    EditorUtility.SetDirty(mapper); // Mark as dirty to save changes
                    SceneView.RepaintAll(); // Force Scene View refresh
                    Debug.Log($"🔄 Direction changed for {stripInfo.branchName}: {(stripInfo.reverseDirection ? "Reverse" : "Normal")}");
                }
                
                // Row 3: Offset (full width for better control)
                stripInfo.startOffset = EditorGUILayout.IntSlider("Start Offset", stripInfo.startOffset, 0, Mathf.Max(1, stripInfo.maxLEDsPerBranch - 1));
                
                // Visual color
                stripInfo.visualColor = EditorGUILayout.ColorField("Color", stripInfo.visualColor);
                
                // Info display
                EditorGUILayout.LabelField($"Node Count: {stripInfo.nodeIndices.Count} | LED Index: {stripInfo.stripIndex}", EditorStyles.miniLabel);
                
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.EndVertical();
        }
        
        void ImportPolylinesFromGeometry()
        {
            if (graphManager == null || !graphManager.DataLoaded)
            {
                EditorUtility.DisplayDialog("Error", "No graph data loaded. Please load geometry first.", "OK");
                return;
            }
            
            Debug.Log("📐 Importing polylines from geometry...");
            
            // Clear existing strips
            mapper.ClearLEDStrips();
            
            // Get polyline data from LEDGraphManager
            var polylines = graphManager.Polylines;
            
            if (polylines.Count == 0)
            {
                EditorUtility.DisplayDialog("Warning", "No polylines found in geometry data. Please ensure your Grasshopper export contains polyline data.", "OK");
                return;
            }
            
            Debug.Log($"📐 Found {polylines.Count} polylines to import as LED strips");
            
            // Create LED strip for each polyline
            for (int i = 0; i < polylines.Count; i++)
            {
                var polyline = polylines[i];
                
                // Create LEDStripInfo from polyline data
                var stripInfo = new LEDStripInfo(i, 19, polyline.suggestedLEDCount);
                stripInfo.branchName = polyline.polylineName;
                stripInfo.nodeIndices = new List<int>(polyline.nodeIndices);
                stripInfo.maxLEDsPerBranch = polyline.suggestedLEDCount;
                stripInfo.reverseDirection = false; // Default direction
                stripInfo.startOffset = 0; // Default offset
                stripInfo.enabled = true; // Enable by default
                stripInfo.wiringOrder = i; // Default to polyline order
                stripInfo.GenerateRandomColor();
                
                mapper.AddLEDStrip(stripInfo);
                
                Debug.Log($"  ✓ {stripInfo.branchName}: {stripInfo.nodeIndices.Count} nodes → {stripInfo.maxLEDsPerBranch} LEDs");
            }
            
            EditorUtility.SetDirty(mapper);
            Debug.Log($"✅ Successfully imported {mapper.LEDStrips.Count} polylines as LED strips");
            
            // Show success dialog with summary
            string summary = $"Imported {polylines.Count} polylines as LED strips:\n\n";
            for (int i = 0; i < polylines.Count; i++)
            {
                var p = polylines[i];
                summary += $"• {p.polylineName}: {p.suggestedLEDCount} LEDs (length: {p.totalLength:F1})\n";
            }
            summary += $"\nTotal suggested LEDs: {polylines.Sum(p => p.suggestedLEDCount)}";
            
            EditorUtility.DisplayDialog("Polylines Imported", summary, "OK");
        }
        
        // REMOVED: TraceBranch method (legacy code from old branch detection system)
        
        void AddNewBranch()
        {
            // Ensure strips list is ready
                
            var newStrip = new LEDStripInfo(mapper.LEDStrips.Count, 19, 33);
            newStrip.branchName = $"Manual Branch {mapper.LEDStrips.Count + 1}";
            newStrip.GenerateRandomColor();
            mapper.AddLEDStrip(newStrip);
            EditorUtility.SetDirty(mapper);
        }
        
        void DeleteBranch(int index)
        {
            if (mapper.LEDStrips != null && index >= 0 && index < mapper.LEDStrips.Count)
            {
                mapper.RemoveLEDStripAt(index);
                
                // Update strip indices
                for (int i = 0; i < mapper.LEDStrips.Count; i++)
                {
                    mapper.LEDStrips[i].stripIndex = i;
                }
                
                EditorUtility.SetDirty(mapper);
            }
        }
        
        void ReloadGeometryFromGrasshopper()
        {
            if (graphManager == null)
            {
                EditorUtility.DisplayDialog("Error", "LEDGraphManager not found on same GameObject.", "OK");
                return;
            }
            
            Debug.Log("🔄 Manually reloading geometry from Grasshopper...");
            
            bool success = graphManager.ForceReloadGeometry();
            
            if (success)
            {
                Debug.Log("✅ Geometry reloaded successfully! Branch analysis will be updated.");
                
                // Clear existing branch analysis since geometry changed
                if (mapper.LEDStrips != null)
                {
                    mapper.ClearLEDStrips();
                }
                branchAnalysisValid = false;
                
                EditorUtility.SetDirty(mapper);
                EditorUtility.DisplayDialog("Success", "Geometry reloaded from Grasshopper successfully!\n\nUse 'Analyze Branches' to detect the new geometry structure.", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Error", "Failed to reload geometry. Check Console for details.", "OK");
            }
        }
    }
}
#endif
