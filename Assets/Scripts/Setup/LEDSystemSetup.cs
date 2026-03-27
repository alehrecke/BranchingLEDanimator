using UnityEngine;
using BranchingLEDAnimator.Core;
using BranchingLEDAnimator.Visualization;
using BranchingLEDAnimator.UI;
using BranchingLEDAnimator.Hardware;
using BranchingLEDAnimator.Mapping;
using BranchingLEDAnimator.Player;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BranchingLEDAnimator.Setup
{
    /// <summary>
    /// One-click setup for the complete LED Animation System with hardware integration
    /// </summary>
    public class LEDSystemSetup : MonoBehaviour
    {
        #if UNITY_EDITOR
        [MenuItem("Tools/LED Animation System/Create Complete System GameObject")]
        static void CreateCompleteLEDSystem()
        {
            // Create the main GameObject
            GameObject ledSystem = new GameObject("LED Animation System");
            
            // Add all required components in the correct order for current working system
            Debug.Log("🔧 Creating LED Animation System with complete hardware integration...");
            
            // 1. Core Components (Data & Animation)
            var graphManager = ledSystem.AddComponent<LEDGraphManager>();
            Debug.Log("✓ Added LEDGraphManager - Geometry data and Grasshopper import");
            
            var animationSystem = ledSystem.AddComponent<LEDAnimationSystem>();
            Debug.Log("✓ Added LEDAnimationSystem - Animation logic and color calculation");
            
            // 2. Visualization Components (Scene & Game View)
            var sceneVisualizer = ledSystem.AddComponent<LEDSceneVisualizer>();
            Debug.Log("✓ Added LEDSceneVisualizer - Scene view gizmos (works in Edit mode)");
            
            var gameVisualizer = ledSystem.AddComponent<UnifiedGameVisualizer>();
            Debug.Log("✓ Added UnifiedGameVisualizer - Game view 3D objects + nighttime environment");
            
            // 3. Hardware Integration Components
            var circuitMapper = ledSystem.AddComponent<LEDCircuitMapper>();
            Debug.Log("✓ Added LEDCircuitMapper - Maps Unity nodes to physical LED strips");
            
            var esp32Communicator = ledSystem.AddComponent<ESP32Communicator>();
            Debug.Log("✓ Added ESP32Communicator - UDP communication with ESP32");
            
            var hardwareTest = ledSystem.AddComponent<LEDHardwareTest>();
            Debug.Log("✓ Added LEDHardwareTest - Complete testing suite for hardware");
            
            var esp32Monitor = ledSystem.AddComponent<ESP32Monitor>();
            Debug.Log("✓ Added ESP32Monitor - Real-time ESP32 status monitoring");
            
            // 4. Optional Advanced Components
            var physicalMapper = ledSystem.AddComponent<LEDPhysicalMapper>();
            Debug.Log("✓ Added LEDPhysicalMapper - Advanced physical LED mapping tools");
            
            // 5. Audio Integration Components (NEW)
            var audioGenerator = ledSystem.AddComponent<BranchingLEDAnimator.Audio.LEDAudioGenerator>();
            Debug.Log("✓ Added LEDAudioGenerator - Generate procedural audio for LED animations");
            
            var audioUploader = ledSystem.AddComponent<BranchingLEDAnimator.Audio.ESP32AudioUploader>();
            Debug.Log("✓ Added ESP32AudioUploader - Upload audio files to ESP32 SD card");
            
            var bfsAudioTester = ledSystem.AddComponent<BranchingLEDAnimator.Audio.BFSAudioTester>();
            Debug.Log("✓ Added BFSAudioTester - Test specialized BFS audio generation");
            
            var graphAudioMapper = ledSystem.AddComponent<BranchingLEDAnimator.Audio.GraphAudioMapper>();
            Debug.Log("✓ Added GraphAudioMapper - Graph topology to audio mapping system");
            
            var smoothAudioPlayer = ledSystem.AddComponent<BranchingLEDAnimator.Audio.SmoothAudioPlayer>();
            Debug.Log("✓ Added SmoothAudioPlayer - Smooth chime playback using pre-generated clips");
            
            var syncedAudioExporter = ledSystem.AddComponent<BranchingLEDAnimator.Audio.SyncedAudioExporter>();
            Debug.Log("✓ Added SyncedAudioExporter - Export synchronized LED+audio to ESP32");
            
            // 6. UI Component
            var controlPanel = ledSystem.AddComponent<LEDControlPanel>();
            Debug.Log("✓ Added LEDControlPanel - Unified control interface");
            
            // 7. Create Player for Interactive Animations
            GameObject player = CreatePlayerWithCamera(graphManager);
            Debug.Log("✓ Created Player with GraphPlayerController - Required for interactive animations");
            
            // Configure default settings
            ConfigureDefaultSettings(ledSystem);
            
            // Select the created GameObject
            Selection.activeGameObject = ledSystem;
            
            Debug.Log("🎉 LED Animation System created successfully! (16 components + Player)");
            Debug.Log("📋 Next steps:");
            Debug.Log("   1. Import Grasshopper data: Right-click LEDGraphManager → 'Import Grasshopper Data'");
            Debug.Log("   2. Create animation assets: Right-click → Assets → Create → LED Animation → Create All Default Animations");
            Debug.Log("   3. Drag animation assets to LEDAnimationSystem's 'Available Animations' list");
            Debug.Log("   4. Configure LED mapping: Right-click LEDCircuitMapper → 'Create LED Mapping'");
            Debug.Log("   5. Set your ESP32 IP address in ESP32Communicator component (port 8888)");
            Debug.Log("   6. Right-click ESP32Communicator → 'Refresh Component References'");
            Debug.Log("   7. Right-click ESP32Communicator → 'Connect to ESP32' to test connection");
            Debug.Log("   8. 🎼 NEW: Right-click GraphAudioMapper → 'Analyze Graph Audio' to map nodes to sounds");
            Debug.Log("   9. 🔊 NEW: SmoothAudioPlayer will automatically play smooth chimes during animations");
            Debug.Log("  10. 📤 NEW: Right-click SyncedAudioExporter → 'Export Synced Animation + Audio'");
            Debug.Log("  11. 📡 NEW: Right-click ESP32AudioUploader → 'Upload All Audio Files' to ESP32");
        }
        
        static void ConfigureDefaultSettings(GameObject ledSystem)
        {
            // Configure ESP32Communicator with current working defaults
            var esp32 = ledSystem.GetComponent<ESP32Communicator>();
            if (esp32 != null)
            {
                // Set default IP (user will need to change to their ESP32's IP)
                esp32.esp32IPAddress = "192.168.0.66"; // Update this to your ESP32's IP
                esp32.esp32Port = 8888; // Fixed port to match ESP32 firmware
                esp32.maxFPS = 30;
                esp32.useCustomProtocol = true;
                esp32.showDebugInfo = true;
                esp32.logDataSent = false; // Reduce console spam
            }
            
            // Configure LEDCircuitMapper - User will configure LED counts manually
            var mapper = ledSystem.GetComponent<LEDCircuitMapper>();
            if (mapper != null)
            {
                mapper.showDebugInfo = true;
                Debug.Log("💡 LEDCircuitMapper: Configure your LED strips manually in the inspector");
                Debug.Log("   - Set LED counts per strip based on your physical setup");
                Debug.Log("   - Configure wiring directions (forward/reverse)");
                Debug.Log("   - Right-click → 'Create LED Mapping' when ready");
            }
            
            // Configure LEDPhysicalMapper for advanced users
            var physMapper = ledSystem.GetComponent<LEDPhysicalMapper>();
            if (physMapper != null)
            {
                physMapper.totalPhysicalLEDs = 99; // Example default, user will adjust
                physMapper.showMappingVisualization = true;
                physMapper.enableManualMapping = false; // Start with auto-detection
            }
            
            // Configure LEDHardwareTest
            var test = ledSystem.GetComponent<LEDHardwareTest>();
            if (test != null)
            {
                test.autoTestOnStart = false; // User can enable if wanted
                test.testInterval = 5f;
            }
            
            // Configure animation system
            var animation = ledSystem.GetComponent<LEDAnimationSystem>();
            if (animation != null)
            {
                animation.autoPlayOnStart = true;
                animation.globalSpeed = 1f;
                animation.showDebugInfo = false; // Reduce console spam
            }
            
            // Configure visualization components
            var sceneVis = ledSystem.GetComponent<LEDSceneVisualizer>();
            if (sceneVis != null)
            {
                sceneVis.showInSceneView = true;
                sceneVis.showAnimatedColors = true;
            }
            
            var gameVis = ledSystem.GetComponent<UnifiedGameVisualizer>();
            if (gameVis != null)
            {
                gameVis.autoCreateOnStart = false; // User creates manually
                gameVis.applyNightEnvironment = true;
                gameVis.showDebugInfo = false;
            }
            
            Debug.Log("⚙️ Configured default settings for complete hardware integration");
            Debug.Log("🔧 Remember to set your ESP32's actual IP address in ESP32Communicator!");
        }
        
        /// <summary>
        /// Create a Player GameObject with Camera and GraphPlayerController
        /// Required for interactive animations (BallSettle, ChordTouch, GraphJuggle, Fountain, etc.)
        /// </summary>
        static GameObject CreatePlayerWithCamera(LEDGraphManager graphManager)
        {
            // Create Player GameObject
            GameObject player = new GameObject("Interactive Player");
            
            // Add GraphPlayerController
            var playerController = player.AddComponent<GraphPlayerController>();
            playerController.graphManager = graphManager;
            
            // Configure for third-person view by default (easier to see the whole structure)
            playerController.cameraMode = GraphPlayerController.CameraMode.ThirdPerson;
            playerController.autoFitCameraToGeometry = true; // Auto-scale camera to geometry size
            playerController.autoFitPadding = 1.5f; // 50% padding around geometry
            playerController.thirdPersonAngle = 45f;
            playerController.autoDetectGround = true;
            playerController.createGroundPlane = true;
            playerController.autoInteractOnTouch = true;
            playerController.touchRadius = 3f;
            playerController.touchHysteresis = 2f;
            
            // Create or find camera
            Camera existingCamera = Camera.main;
            if (existingCamera != null)
            {
                // Use existing main camera
                playerController.playerCamera = existingCamera;
                Debug.Log("   Using existing Main Camera");
            }
            else
            {
                // Create new camera as child of player
                GameObject cameraObj = new GameObject("Player Camera");
                cameraObj.transform.SetParent(player.transform);
                cameraObj.transform.localPosition = new Vector3(0, 30, -40);
                cameraObj.transform.localRotation = Quaternion.Euler(35, 0, 0);
                
                Camera cam = cameraObj.AddComponent<Camera>();
                cam.tag = "MainCamera";
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.02f, 0.02f, 0.05f); // Dark blue-black
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 500f;
                
                // Add audio listener
                cameraObj.AddComponent<AudioListener>();
                
                playerController.playerCamera = cam;
                Debug.Log("   Created new Player Camera");
            }
            
            // Position player at origin (will auto-adjust when graph loads)
            player.transform.position = Vector3.zero;
            
            Debug.Log("   Player configured for Third-Person view");
            Debug.Log("   Press Tab in Play mode to switch camera modes");
            Debug.Log("   WASD to move, mouse to look, click to interact with endpoints");
            
            return player;
        }
        
        [MenuItem("Tools/LED Animation System/Create Animation Assets")]
        static void CreateAnimationAssets()
        {
            // Create default animation assets if they don't exist
            string[] animationTypes = { "Wave", "Pulse", "Sparkle" };
            
            foreach (string animType in animationTypes)
            {
                string path = $"Assets/Animations/{animType}Animation.asset";
                
                // Check if asset already exists
                if (AssetDatabase.LoadAssetAtPath(path, typeof(ScriptableObject)) == null)
                {
                    // Create the directory if it doesn't exist
                    if (!AssetDatabase.IsValidFolder("Assets/Animations"))
                    {
                        AssetDatabase.CreateFolder("Assets", "Animations");
                    }
                    
                    // Create the asset based on type
                    ScriptableObject asset = null;
                    switch (animType)
                    {
                        case "Wave":
                            asset = ScriptableObject.CreateInstance("WaveAnimation");
                            break;
                        case "Pulse":
                            asset = ScriptableObject.CreateInstance("PulseAnimation");
                            break;
                        case "Sparkle":
                            asset = ScriptableObject.CreateInstance("SparkleAnimation");
                            break;
                    }
                    
                    if (asset != null)
                    {
                        AssetDatabase.CreateAsset(asset, path);
                        Debug.Log($"✓ Created {animType}Animation.asset");
                    }
                }
                else
                {
                    Debug.Log($"⚠️ {animType}Animation.asset already exists");
                }
            }
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            Debug.Log("🎬 Animation assets creation complete!");
            Debug.Log("📋 Don't forget to drag these assets into your LEDAnimationSystem's 'Available Animations' list");
        }
        
        [MenuItem("Tools/LED Animation System/Add Player to Existing Scene")]
        static void AddPlayerToExistingScene()
        {
            // Find existing graph manager
            var graphManager = Object.FindObjectOfType<LEDGraphManager>();
            if (graphManager == null)
            {
                EditorUtility.DisplayDialog("Error", 
                    "No LEDGraphManager found in scene!\n\nPlease create the complete system first using:\nTools > LED Animation System > Create Complete System GameObject", 
                    "OK");
                return;
            }
            
            // Check if player already exists
            var existingPlayer = Object.FindObjectOfType<GraphPlayerController>();
            if (existingPlayer != null)
            {
                EditorUtility.DisplayDialog("Player Already Exists", 
                    $"A GraphPlayerController already exists on '{existingPlayer.gameObject.name}'.\n\nDelete it first if you want to create a new one.", 
                    "OK");
                Selection.activeGameObject = existingPlayer.gameObject;
                return;
            }
            
            // Create player
            GameObject player = CreatePlayerWithCamera(graphManager);
            Selection.activeGameObject = player;
            
            Debug.Log("🎮 Player created and added to scene!");
            Debug.Log("   The player is required for interactive animations like:");
            Debug.Log("   - Ball Settle Animation");
            Debug.Log("   - Chord Touch Animation");
            Debug.Log("   - Graph Juggle Animation");
            Debug.Log("   - Fountain Animation");
        }
        
        [MenuItem("Tools/LED Animation System/Help - Setup Guide")]
        static void ShowSetupHelp()
        {
            string helpMessage = @"LED Animation System - Complete Setup Guide

🎯 SYSTEM COMPONENTS:
Main System GameObject:
1. LEDGraphManager - Geometry data and Grasshopper import
2. LEDAnimationSystem - Animation logic and color calculation
3. LEDSceneVisualizer - Scene view gizmos (works in Edit mode)
4. UnifiedGameVisualizer - Game view 3D objects + nighttime environment
5. LEDCircuitMapper - Maps Unity nodes to physical LED strips
6. ESP32Communicator - UDP communication with ESP32
7. LEDHardwareTest - Complete testing suite
8. ESP32Monitor - Real-time ESP32 status monitoring
9. LEDPhysicalMapper - Advanced physical LED mapping tools
10. Audio components (LEDAudioGenerator, etc.)
11. LEDControlPanel - Unified control interface

Player GameObject (for Interactive Animations):
- GraphPlayerController - Handles movement and touch input
- Camera - For viewing and interacting with the scene

🔧 SETUP WORKFLOW:
1. Create Complete System GameObject ← Creates everything!
2. Import Grasshopper Data: Set path in LEDGraphManager, right-click → 'Force Reload Geometry'
3. Create Animation Assets: Assets → Create → LED Animations → [type]
4. Drag animations to LEDAnimationSystem's 'Available Animations' list
5. Configure LED Mapping: Right-click LEDCircuitMapper → 'Create LED Mapping'
6. Set your ESP32 IP address in ESP32Communicator
7. Enter Play mode - Player will auto-position on the geometry

🎮 INTERACTIVE ANIMATIONS:
The Player (GraphPlayerController) is REQUIRED for:
- Ball Settle Animation - Touch endpoints to disperse balls
- Chord Touch Animation - Touch and hold for tones
- Graph Juggle Animation - Catch the bouncing balls
- Fountain Animation - Block branches to redirect flow

Controls in Play Mode:
- WASD: Move player
- Mouse: Look around
- Tab: Switch camera modes (First/Third person)
- Click: Interact with endpoints

📁 FOR NEW GEOMETRIES:
1. Create a NEW SCENE (File → New Scene)
2. Run 'Create Complete System GameObject' again
3. Set a DIFFERENT Grasshopper file path
4. Each scene stores its own geometry + mapping

🎉 Your Unity animations will display on physical LEDs with full hardware integration!";

            EditorUtility.DisplayDialog("LED Animation System Setup", helpMessage, "Got it!");
        }
        #endif
    }
}
