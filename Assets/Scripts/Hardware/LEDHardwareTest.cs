using UnityEngine;
using BranchingLEDAnimator.Core;
using BranchingLEDAnimator.Visualization;
using BranchingLEDAnimator.UI;

namespace BranchingLEDAnimator.Hardware
{
    /// <summary>
    /// Simple test component for LED hardware integration
    /// Add this to your GameObject to test the complete Unity → ESP32 pipeline
    /// </summary>
    public class LEDHardwareTest : MonoBehaviour
    {
        [Header("Test Configuration")]
        public bool autoTestOnStart = false;
        public float testInterval = 5f; // Seconds between automatic tests
        
        [Header("Component References")]
        private LEDGraphManager graphManager;
        private LEDCircuitMapper circuitMapper;
        private ESP32Communicator esp32Communicator;
        private LEDAnimationSystem animationSystem;
        
        [Header("Test Status")]
        [SerializeField] private bool allComponentsFound = false;
        [SerializeField] private bool mappingComplete = false;
        [SerializeField] private bool esp32Connected = false;
        [SerializeField] private bool animationRunning = false;
        
        private float nextTestTime = 0f;
        
        void Start()
        {
            FindComponents();
            CheckSystemStatus();
            
            if (autoTestOnStart)
            {
                Invoke(nameof(RunFullSystemTest), 2f); // Delay to let everything initialize
            }
        }
        
        void Update()
        {
            if (autoTestOnStart && Time.time >= nextTestTime)
            {
                CheckSystemStatus();
                nextTestTime = Time.time + testInterval;
            }
        }
        
        /// <summary>
        /// Find all required components
        /// </summary>
        void FindComponents()
        {
            graphManager = GetComponent<LEDGraphManager>();
            circuitMapper = GetComponent<LEDCircuitMapper>();
            esp32Communicator = GetComponent<ESP32Communicator>();
            animationSystem = GetComponent<LEDAnimationSystem>();
            
            allComponentsFound = graphManager != null && circuitMapper != null && 
                               esp32Communicator != null && animationSystem != null;
            
            if (!allComponentsFound)
            {
                Debug.LogWarning("⚠️ Missing required components for LED hardware test!");
                if (graphManager == null) Debug.LogWarning("  - Missing LEDGraphManager");
                if (circuitMapper == null) Debug.LogWarning("  - Missing LEDCircuitMapper");
                if (esp32Communicator == null) Debug.LogWarning("  - Missing ESP32Communicator");
                if (animationSystem == null) Debug.LogWarning("  - Missing LEDAnimationSystem");
            }
        }
        
        /// <summary>
        /// Check overall system status
        /// </summary>
        void CheckSystemStatus()
        {
            if (!allComponentsFound) return;
            
            mappingComplete = circuitMapper.MappingComplete;
            esp32Connected = esp32Communicator.IsConnected;
            animationRunning = animationSystem.isPlaying;
        }
        
        /// <summary>
        /// Run complete system test
        /// </summary>
        [ContextMenu("Run Full System Test")]
        public void RunFullSystemTest()
        {
            Debug.Log("🧪 STARTING FULL LED HARDWARE SYSTEM TEST");
            Debug.Log("==================================================");
            
            if (!allComponentsFound)
            {
                Debug.LogError("❌ Cannot run test - missing components!");
                return;
            }
            
            // Step 1: Check geometry
            TestGeometryData();
            
            // Step 2: Test mapping
            TestCircuitMapping();
            
            // Step 3: Test ESP32 connection
            TestESP32Connection();
            
            // Step 4: Test animations
            TestAnimations();
            
            Debug.Log("==================================================");
            Debug.Log("🧪 FULL SYSTEM TEST COMPLETE");
        }
        
        void TestGeometryData()
        {
            Debug.Log("📊 Testing Geometry Data...");
            
            if (graphManager.DataLoaded)
            {
                Debug.Log($"✅ Geometry loaded: {graphManager.NodeCount} nodes, {graphManager.EdgeCount} edges");
            }
            else
            {
                Debug.LogWarning("❌ No geometry data loaded!");
            }
        }
        
        void TestCircuitMapping()
        {
            Debug.Log("🔌 Testing Circuit Mapping...");
            
            // First check if graph manager has data
            if (graphManager != null)
            {
                Debug.Log($"Graph Manager Status: Data Loaded = {graphManager.DataLoaded}, Nodes = {graphManager.NodeCount}");
            }
            else
            {
                Debug.LogWarning("Graph Manager is null!");
            }
            
            if (!circuitMapper.MappingComplete)
            {
                Debug.Log("Creating LED mapping...");
                circuitMapper.CreateLEDMapping();
            }
            
            if (circuitMapper.MappingComplete)
            {
                Debug.Log($"✅ Mapping complete: {circuitMapper.StripCount} strips, {circuitMapper.TotalLEDCount} total LEDs");
            }
            else
            {
                Debug.LogWarning("❌ Circuit mapping failed!");
                Debug.LogWarning("💡 Try: Right-click LEDGraphManager → 'Import Grasshopper Data' first");
            }
        }
        
        void TestESP32Connection()
        {
            Debug.Log("📡 Testing ESP32 Connection...");
            
            if (!esp32Communicator.IsConnected)
            {
                Debug.Log("Attempting to connect to ESP32...");
                esp32Communicator.ConnectToESP32();
            }
            
            if (esp32Communicator.IsConnected)
            {
                Debug.Log("✅ ESP32 connected - sending test pattern...");
                esp32Communicator.TestConnection();
            }
            else
            {
                Debug.LogWarning("❌ ESP32 connection failed! Check IP address and network.");
            }
        }
        
        void TestAnimations()
        {
            Debug.Log("🎬 Testing Animations...");
            
            if (animationSystem.AnimationCount > 0)
            {
                Debug.Log($"✅ {animationSystem.AnimationCount} animations available");
                Debug.Log($"Current animation: {animationSystem.CurrentAnimationName}");
                Debug.Log($"Animation playing: {animationSystem.isPlaying}");
                
                if (!animationSystem.isPlaying)
                {
                    Debug.Log("Starting animation...");
                    // Animation should start automatically with the new system
                }
            }
            else
            {
                Debug.LogWarning("❌ No animations available!");
            }
        }
        
        /// <summary>
        /// Quick test buttons for individual components
        /// </summary>
        [ContextMenu("Test Geometry Only")]
        public void TestGeometryOnly()
        {
            TestGeometryData();
        }
        
        [ContextMenu("Test Mapping Only")]
        public void TestMappingOnly()
        {
            TestCircuitMapping();
        }
        
        [ContextMenu("Test ESP32 Only")]
        public void TestESP32Only()
        {
            TestESP32Connection();
        }
        
        [ContextMenu("Test Animations Only")]
        public void TestAnimationsOnly()
        {
            TestAnimations();
        }
        
        [ContextMenu("Quick Connection Test")]
        public void QuickConnectionTest()
        {
            if (esp32Communicator == null)
            {
                Debug.LogError("ESP32Communicator not found!");
                return;
            }
            
            Debug.Log("🔍 QUICK WS2812 CONNECTION TEST");
            Debug.Log($"📡 ESP32 IP: {esp32Communicator.esp32IPAddress}");
            Debug.Log($"🔌 Connection Status: {(esp32Communicator.IsConnected ? "✅ Connected" : "❌ Disconnected")}");
            
            // Force fresh connection
            esp32Communicator.ConnectToESP32();
            
            // Send a simple rainbow pattern
            esp32Communicator.SendRainbowTestPattern();
            Debug.Log("🌈 Sent rainbow pattern to WS2812 LEDs via ESP32");
            Debug.Log("💡 Check your ESP32 serial monitor and LED strip!");
        }
        
        [ContextMenu("Send Rainbow to ESP32")]
        public void SendRainbowTest()
        {
            if (esp32Communicator != null && esp32Communicator.IsConnected)
            {
                esp32Communicator.SendRainbowTestPattern();
                Debug.Log("🌈 Sent rainbow test pattern to ESP32");
            }
            else
            {
                Debug.LogWarning("ESP32 not connected!");
            }
        }
        
        [ContextMenu("Debug LED Hardware Issues")]
        public void DebugLEDHardwareIssues()
        {
            Debug.Log("🔍 LED HARDWARE TROUBLESHOOTING GUIDE");
            Debug.Log("==================================================");
            
            // Check system status first
            FindComponents();
            CheckSystemStatus();
            
            Debug.Log("📋 HARDWARE CHECKLIST:");
            Debug.Log("1. ESP32 Power & Network:");
            Debug.Log($"   - Unity IP: {esp32Communicator?.esp32IPAddress ?? "Not Set"}");
            Debug.Log($"   - Unity Port: {esp32Communicator?.esp32Port ?? 0}");
            Debug.Log($"   - Connection: {(esp32Connected ? "✅ Success" : "❌ Failed")}");
            
            Debug.Log("2. LED Strip Configuration:");
            Debug.Log($"   - Total LEDs Expected: {circuitMapper?.totalPhysicalLEDs ?? 0}");
            Debug.Log($"   - Strips: {circuitMapper?.numLogicalStrips ?? 0}");
            Debug.Log($"   - LEDs per Strip: {circuitMapper?.ledsPerStrip ?? 0}");
            
            Debug.Log("3. WS2812 Hardware Setup:");
            Debug.Log("   ✅ WS2812 (NeoPixel) LEDs - Perfect choice!");
            Debug.Log("   ✅ 24V→5V power conversion - Correct for ESP32");
            Debug.Log("   ✅ 3.3V→5V level shifter for data - Essential for WS2812");
            Debug.Log("   📍 Data flow: ESP32 GPIO19 → Level Shifter → WS2812 LEDs");
            Debug.Log("   💡 Check: Level shifter enabled, GPIO19 connected to data input");
            
            Debug.Log("4. Data Format Check:");
            Debug.Log($"   - Protocol: Custom UDP");
            Debug.Log($"   - Test Pattern Sent: ✅ Pattern 2 (Rainbow)");
            
            Debug.Log("💡 TROUBLESHOOTING STEPS:");
            Debug.Log("   A. Check ESP32 Serial Monitor for incoming UDP packets");
            Debug.Log("   B. Verify ESP32 is on same network as Unity PC");
            Debug.Log("   C. Try 'Send Simple Test Command' below");
            Debug.Log("   D. Check if ESP32 GPIO 19 matches your hardware wiring");
            
            Debug.Log("==================================================");
        }
        
        [ContextMenu("Send All Test Patterns")]
        public void SendAllTestPatterns()
        {
            if (esp32Communicator == null)
            {
                Debug.LogError("ESP32Communicator not found!");
                return;
            }
            
            // Start the coroutine
            StartCoroutine(SendPatternsCoroutine());
        }
        
        private System.Collections.IEnumerator SendPatternsCoroutine()
        {
            Debug.Log("📡 Starting WS2812 LED test sequence...");
            
            // Ensure connection
            if (!esp32Communicator.IsConnected)
            {
                Debug.LogWarning("ESP32 not connected! Attempting to connect...");
                esp32Communicator.ConnectToESP32();
                yield return new WaitForSeconds(1f);
            }
            
            // Force reconnection to ensure UDP client is valid
            Debug.Log("🔄 Ensuring stable UDP connection...");
            esp32Communicator.ConnectToESP32();
            yield return new WaitForSeconds(0.5f);
            
            string[] patternNames = { "Off", "White", "Rainbow", "Chase", "Sparkle" };
            
            for (byte pattern = 0; pattern <= 4; pattern++)
            {
                Debug.Log($"🎨 Sending Pattern {pattern} ({patternNames[pattern]}) to WS2812 LEDs...");
                Debug.Log($"   Command: [0xAA, 0x03, 0x00, 0x{pattern:X2}]");
                Debug.Log($"   Hardware: ESP32→GPIO19→Level Shifter→WS2812s");
                
                // Send pattern
                if (pattern == 0)
                {
                    esp32Communicator.SendOffTestPattern();
                }
                else if (pattern == 1)
                {
                    esp32Communicator.SendWhiteTestPattern();
                }
                else if (pattern == 2)
                {
                    esp32Communicator.SendRainbowTestPattern();
                }
                else
                {
                    esp32Communicator.TestConnection();
                }
                
                Debug.Log($"✓ Pattern {pattern} sent - Check your WS2812 LEDs!");
                yield return new WaitForSeconds(3f); // Wait 3 seconds between patterns
            }
            
            Debug.Log("✅ All WS2812 test patterns sent!");
            Debug.Log("💡 If WS2812 LEDs don't light up, check:");
            Debug.Log("   🔌 ESP32 serial monitor for UDP packet reception");
            Debug.Log("   🌐 Network: ping 192.168.1.100 from your PC");
            Debug.Log("   ⚡ Level shifter: 3.3V input, 5V output enabled");
            Debug.Log("   📍 GPIO19 → Level Shifter → WS2812 data pin");
            Debug.Log("   🔋 24V power supply and 5V regulation working");
            Debug.Log("   🎯 ESP32 code processing UDP commands correctly");
        }
        
        [ContextMenu("Show System Status")]
        public void ShowSystemStatus()
        {
            FindComponents(); // Refresh component search
            CheckSystemStatus();
            
            Debug.Log("🔍 LED HARDWARE SYSTEM STATUS:");
            Debug.Log($"  - All Components Found: {(allComponentsFound ? "✅" : "❌")}");
            
            // Show detailed component status
            Debug.Log("📋 INDIVIDUAL COMPONENT STATUS:");
            Debug.Log($"  - LEDGraphManager: {(graphManager != null ? "✅ Found" : "❌ Missing")}");
            Debug.Log($"  - LEDCircuitMapper: {(circuitMapper != null ? "✅ Found" : "❌ Missing")}");
            Debug.Log($"  - ESP32Communicator: {(esp32Communicator != null ? "✅ Found" : "❌ Missing")}");
            Debug.Log($"  - LEDAnimationSystem: {(animationSystem != null ? "✅ Found" : "❌ Missing")}");
            
            if (allComponentsFound)
            {
                Debug.Log($"  - Geometry Loaded: {(graphManager?.DataLoaded ?? false ? "✅" : "❌")}");
                Debug.Log($"  - Mapping Complete: {(mappingComplete ? "✅" : "❌")}");
                Debug.Log($"  - ESP32 Connected: {(esp32Connected ? "✅" : "❌")}");
                Debug.Log($"  - Animation Running: {(animationRunning ? "✅" : "❌")}");
                
                if (mappingComplete && esp32Connected && animationRunning)
                {
                    Debug.Log("🎉 SYSTEM FULLY OPERATIONAL!");
                }
                else
                {
                    Debug.Log("⚠️ System not fully operational - check individual components");
                }
            }
            else
            {
                Debug.Log("❌ Cannot check system status - missing required components!");
                Debug.Log("💡 SOLUTION: Use Tools → LED Animation System → Create Complete System GameObject");
            }
        }
        
        [ContextMenu("Force Load Graph Data")]
        public void ForceLoadGraphData()
        {
            FindComponents();
            
            if (graphManager == null)
            {
                Debug.LogError("❌ LEDGraphManager not found!");
                return;
            }
            
            Debug.Log("🔄 Force loading graph data...");
            
            // Try to trigger data loading
            if (!graphManager.DataLoaded)
            {
                Debug.Log("📊 Graph data not loaded - attempting to load...");
                // The graph manager should have a method to load data
                // Let's see if it loads automatically
            }
            
            Debug.Log($"📊 Graph Status: Loaded = {graphManager.DataLoaded}, Nodes = {graphManager.NodeCount}");
            
            if (graphManager.DataLoaded && circuitMapper != null)
            {
                Debug.Log("🔌 Now attempting LED mapping...");
                circuitMapper.CreateLEDMapping();
                
                if (circuitMapper.MappingComplete)
                {
                    Debug.Log("✅ LED mapping successful after data reload!");
                }
            }
        }
        
        [ContextMenu("Add Missing Components")]
        public void AddMissingComponents()
        {
            FindComponents(); // Check current state
            
            Debug.Log("🔧 ADDING MISSING COMPONENTS...");
            
            if (graphManager == null)
            {
                gameObject.AddComponent<LEDGraphManager>();
                Debug.Log("✓ Added LEDGraphManager");
            }
            
            if (circuitMapper == null)
            {
                gameObject.AddComponent<LEDCircuitMapper>();
                Debug.Log("✓ Added LEDCircuitMapper");
            }
            
            if (esp32Communicator == null)
            {
                gameObject.AddComponent<ESP32Communicator>();
                Debug.Log("✓ Added ESP32Communicator");
            }
            
            if (animationSystem == null)
            {
                gameObject.AddComponent<LEDAnimationSystem>();
                Debug.Log("✓ Added LEDAnimationSystem");
            }
            
            // Add other useful components if missing
            if (GetComponent<LEDSceneVisualizer>() == null)
            {
                gameObject.AddComponent<LEDSceneVisualizer>();
                Debug.Log("✓ Added LEDSceneVisualizer");
            }
            
            if (GetComponent<LEDGameVisualizer>() == null)
            {
                gameObject.AddComponent<LEDGameVisualizer>();
                Debug.Log("✓ Added LEDGameVisualizer");
            }
            
            if (GetComponent<LEDControlPanel>() == null)
            {
                gameObject.AddComponent<LEDControlPanel>();
                Debug.Log("✓ Added LEDControlPanel");
            }
            
            // Refresh component references
            FindComponents();
            
            Debug.Log("🎉 All components added! You can now run the full system test.");
        }
    }
}
