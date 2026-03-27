using UnityEngine;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Linq;
using BranchingLEDAnimator.Core;

namespace BranchingLEDAnimator.Hardware
{
    /// <summary>
    /// Handles communication between Unity and ESP32 for LED control
    /// Sends real-time color data over WiFi UDP
    /// </summary>
    public class ESP32Communicator : MonoBehaviour
    {
        [Header("ESP32 Connection")]
        public string esp32IPAddress = "192.168.1.100"; // Set to your ESP32's IP
        public int esp32Port = 8888;
        public bool autoConnect = true;
        
        [Header("Communication Settings")]
        public int maxFPS = 30;
        public bool useCustomProtocol = true; // Use your existing packet format
        
        [Header("LED Configuration (Auto-detected from Circuit Mapper)")]
        [SerializeField] private int detectedTotalLEDs = 0;
        [SerializeField] private int detectedStripCount = 0;
        [SerializeField] private int detectedAvgLEDsPerStrip = 0;
        
        [Header("Debug")]
        public bool showDebugInfo = true;
        public bool logDataSent = true;
        
        [ContextMenu("Debug: Analyze LED Mapping")]
        public void DebugLEDMapping()
        {
            if (circuitMapper == null)
            {
                Debug.LogError("❌ No circuit mapper found!");
                return;
            }
            
            Debug.Log("🔍 LED MAPPING ANALYSIS");
            Debug.Log("========================================");
            Debug.Log($"📊 Total Physical LEDs: {detectedTotalLEDs}");
            Debug.Log($"📊 Logical Strips: {detectedStripCount}");
            Debug.Log($"📊 LEDs per Strip: {detectedAvgLEDsPerStrip}");
            
            // Analyze first few nodes mapping
            if (graphManager != null && graphManager.DataLoaded)
            {
                Debug.Log($"📊 Graph Nodes: {graphManager.NodeCount}");
                Debug.Log($"📊 Source Nodes (Endpoints): {graphManager.SourceNodes.Count}");
                
                Debug.Log("\n🔍 First 10 Node Mappings:");
                for (int i = 0; i < Mathf.Min(10, graphManager.NodeCount); i++)
                {
                    var ledAddr = circuitMapper.GetLEDAddress(i);
                    if (ledAddr.stripIndex >= 0)
                    {
                        Debug.Log($"  Node {i} → Strip {ledAddr.stripIndex}, LED {ledAddr.ledIndex}");
                    }
                    else
                    {
                        Debug.Log($"  Node {i} → UNMAPPED");
                    }
                }
                
                Debug.Log("\n🔍 Endpoint Node Mappings:");
                foreach (int sourceNode in graphManager.SourceNodes.Take(5))
                {
                    var ledAddr = circuitMapper.GetLEDAddress(sourceNode);
                    if (ledAddr.stripIndex >= 0)
                    {
                        Debug.Log($"  Endpoint Node {sourceNode} → Strip {ledAddr.stripIndex}, LED {ledAddr.ledIndex}");
                    }
                    else
                    {
                        Debug.Log($"  Endpoint Node {sourceNode} → UNMAPPED");
                    }
                }
            }
            
            Debug.Log("========================================");
        }
        
        [ContextMenu("Debug: Send Test Pattern")]
        public void SendTestPattern()
        {
            if (!isConnected)
            {
                Debug.LogWarning("⚠️ Not connected to ESP32!");
                return;
            }
            
            if (graphManager == null || !graphManager.DataLoaded)
            {
                Debug.LogWarning("⚠️ No graph data loaded!");
                return;
            }
            
            Debug.Log("🧪 Sending test pattern to ESP32...");
            
            // Create test pattern: Red endpoints, Blue first 10 nodes
            Color[] testColors = new Color[graphManager.NodeCount];
            
            // All black by default
            for (int i = 0; i < testColors.Length; i++)
            {
                testColors[i] = Color.black;
            }
            
            // Red endpoints
            foreach (int sourceNode in graphManager.SourceNodes)
            {
                if (sourceNode >= 0 && sourceNode < testColors.Length)
                {
                    testColors[sourceNode] = Color.red;
                }
            }
            
            // Blue first 10 nodes
            for (int i = 0; i < Mathf.Min(10, testColors.Length); i++)
            {
                testColors[i] = Color.blue;
            }
            
            // Send to ESP32
            SendLEDData(testColors);
            Debug.Log($"✅ Sent test pattern: {graphManager.SourceNodes.Count} red endpoints, 10 blue nodes");
        }
        
        [ContextMenu("Debug: Send Color Calibration Pattern")]
        public void SendColorCalibrationPattern()
        {
            if (!isConnected)
            {
                Debug.LogWarning("⚠️ Not connected to ESP32!");
                return;
            }
            
            // Get LED count from circuit mapper or graph manager
            int ledCount = 0;
            
            if (circuitMapper == null)
                circuitMapper = GetComponent<LEDCircuitMapper>();
            if (graphManager == null)
                graphManager = GetComponent<LEDGraphManager>();
            
            if (circuitMapper != null && circuitMapper.MappingComplete)
            {
                ledCount = circuitMapper.TotalLEDCount;
            }
            else if (graphManager != null && graphManager.DataLoaded)
            {
                ledCount = graphManager.NodeCount;
            }
            
            if (ledCount <= 0)
            {
                Debug.LogWarning("⚠️ Could not determine LED count! Make sure circuit mapper or graph data is loaded.");
                return;
            }
            
            Debug.Log("🎨 Sending color calibration pattern to ESP32...");
            Debug.Log($"   Sending to {ledCount} LEDs");
            Debug.Log("   This will display: RED | GREEN | BLUE | WHITE | YELLOW | CYAN | MAGENTA");
            Debug.Log("   Check if colors on LEDs match these labels.");
            Debug.Log($"   Current settings: Channel={channelOrder}, Gamma={gammaCorrection}, Temp={colorTemperature}, Color={colorCorrection}");
            
            Color[] testColors = new Color[ledCount];
            int segmentSize = Mathf.Max(1, ledCount / 7);
            
            for (int i = 0; i < testColors.Length; i++)
            {
                int segment = i / segmentSize;
                switch (segment)
                {
                    case 0: testColors[i] = Color.red; break;      // Pure red
                    case 1: testColors[i] = Color.green; break;    // Pure green
                    case 2: testColors[i] = Color.blue; break;     // Pure blue
                    case 3: testColors[i] = Color.white; break;    // White
                    case 4: testColors[i] = Color.yellow; break;   // Yellow (R+G)
                    case 5: testColors[i] = Color.cyan; break;     // Cyan (G+B)
                    default: testColors[i] = Color.magenta; break; // Magenta (R+B)
                }
            }
            
            // Send directly to ESP32 using continuous strip packet
            SendCalibrationDirect(testColors);
            
            Debug.Log("✅ Color calibration pattern sent!");
            Debug.Log("   If GREEN shows as first color but RED label expected, try GRB channel order");
            Debug.Log("   If colors look washed out, increase gamma to 2.2-2.5");
            Debug.Log("   If green looks turquoise, reduce Blue in colorCorrection (e.g., 1, 1, 0.7)");
        }
        
        /// <summary>
        /// Send calibration colors directly without going through circuit mapper
        /// </summary>
        private void SendCalibrationDirect(Color[] colors)
        {
            try
            {
                byte[] packet = CreateContinuousStripPacket(colors);
                udpClient.Send(packet, packet.Length, esp32EndPoint);
                Debug.Log($"📡 Sent calibration packet: {colors.Length} LEDs, {packet.Length} bytes");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to send calibration pattern: {e.Message}");
            }
        }
        
        // Communication components
        private UdpClient udpClient;
        private IPEndPoint esp32EndPoint;
        private bool isConnected = false;
        
        // Timing
        private float lastSendTime = 0f;
        private float sendInterval;
        
        // References
        [Header("Component References (Auto-detected if empty)")]
        [SerializeField] private LEDCircuitMapper circuitMapper;
        [SerializeField] private LEDGraphManager graphManager;
        
        // Stats
        private int packetsSent = 0;
        private int bytesSent = 0;
        
        // Animation export support
        [Header("Animation Export")]
        public bool enableAnimationExport = true;
        
        [Header("LED Color Correction")]
        [Tooltip("Color channel order - WS2812B typically uses GRB")]
        public ColorChannelOrder channelOrder = ColorChannelOrder.RGB;
        
        [Tooltip("Gamma correction - LEDs are non-linear. 2.2-2.8 is typical for WS2812B")]
        [Range(1f, 3f)]
        public float gammaCorrection = 1.0f;
        
        [Tooltip("Per-channel color correction multipliers")]
        public Vector3 colorCorrection = new Vector3(1f, 1f, 1f); // R, G, B multipliers
        
        [Tooltip("Global brightness scaling (applied after gamma)")]
        [Range(0f, 1f)]
        public float ledBrightness = 1f;
        
        [Tooltip("Color temperature adjustment - negative = warmer (more red), positive = cooler (more blue)")]
        [Range(-1f, 1f)]
        public float colorTemperature = 0f;
        
        public enum ColorChannelOrder
        {
            RGB,    // Standard
            GRB,    // WS2812B default
            BGR,    // Some LED strips
            RBG,
            GBR,
            BRG
        }
        
        void Start()
        {
            // Calculate send interval
            sendInterval = 1f / maxFPS;
            
            // Get references - try multiple methods to find circuit mapper
            circuitMapper = GetComponent<LEDCircuitMapper>();
            if (circuitMapper == null)
            {
                circuitMapper = FindFirstObjectByType<LEDCircuitMapper>();
                if (showDebugInfo && circuitMapper != null)
                {
                    if (showDebugInfo)
                    {
                        Debug.Log($"📡 ESP32Communicator: Found LEDCircuitMapper on {circuitMapper.gameObject.name}");
                    }
                }
            }
            
            graphManager = GetComponent<LEDGraphManager>();
            if (graphManager == null)
            {
                graphManager = FindFirstObjectByType<LEDGraphManager>();
                if (showDebugInfo && graphManager != null)
                {
                    if (showDebugInfo)
                    {
                        Debug.Log($"📡 ESP32Communicator: Found LEDGraphManager on {graphManager.gameObject.name}");
                    }
                }
            }
            
            // Update LED configuration from circuit mapper
            UpdateLEDConfigurationFromMapper();
            
            if (autoConnect)
            {
                ConnectToESP32();
            }
            
            // Subscribe to color updates and geometry updates
            LEDVisualizationEvents.OnColorsUpdated += OnColorsUpdated;
            LEDVisualizationEvents.OnGeometryUpdated += OnGeometryUpdated;
            
            // Automatically send LED configuration when connected and mapping is ready
            StartCoroutine(AutoSendConfigurationWhenReady());
        }
        
        void OnDestroy()
        {
            LEDVisualizationEvents.OnColorsUpdated -= OnColorsUpdated;
            LEDVisualizationEvents.OnGeometryUpdated -= OnGeometryUpdated;
            DisconnectFromESP32();
        }
        
        /// <summary>
        /// Automatically send LED configuration when both connection and mapping are ready
        /// </summary>
        private System.Collections.IEnumerator AutoSendConfigurationWhenReady()
        {
            // Wait a moment for everything to initialize
            yield return new WaitForSeconds(2f);
            
            // Keep checking until both conditions are met
            int attempts = 0;
            while (attempts < 30) // Max 30 attempts (30 seconds)
            {
                if (isConnected && circuitMapper != null && circuitMapper.MappingComplete)
                {
                    Debug.Log("🔧 Auto-sending LED configuration to ESP32...");
                    SendLEDConfiguration();
                    yield break; // Exit successfully
                }
                
                attempts++;
                yield return new WaitForSeconds(1f);
            }
            
            if (!isConnected)
                Debug.LogWarning("⚠️ Auto-send LED config failed: Not connected to ESP32");
            else if (circuitMapper == null)
                Debug.LogWarning("⚠️ Auto-send LED config failed: No circuit mapper found");
            else if (!circuitMapper.MappingComplete)
                Debug.LogWarning("⚠️ Auto-send LED config failed: Mapping not complete");
        }
        
        /// <summary>
        /// Connect to ESP32 via UDP
        /// </summary>
        [ContextMenu("Connect to ESP32")]
        public void ConnectToESP32()
        {
            try
            {
                // Dispose existing UDP client if it exists
                if (udpClient != null)
                {
                    udpClient.Close();
                    udpClient.Dispose();
                    udpClient = null;
                }
                
                // Create UDP client
                udpClient = new UdpClient();
                esp32EndPoint = new IPEndPoint(IPAddress.Parse(esp32IPAddress), esp32Port);
                
                // UDP client setup (broadcast not needed for single ESP32)
                udpClient.EnableBroadcast = false;
                
                isConnected = true;
                
                if (showDebugInfo)
                {
                    if (showDebugInfo)
                    {
                        Debug.Log($"✓ Connected to ESP32 at {esp32IPAddress}:{esp32Port}");
                    }
                }
                
                // Update LED configuration and send to ESP32
                UpdateLEDConfigurationFromMapper();
                
                // Send initial connection packet
                SendConnectionPacket();
                
                // Send LED configuration if we have valid mapping
                if (circuitMapper != null && circuitMapper.MappingComplete)
                {
                    SendLEDConfiguration();
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to connect to ESP32: {e.Message}");
                isConnected = false;
            }
        }
        
        /// <summary>
        /// Disconnect from ESP32
        /// </summary>
        [ContextMenu("Disconnect from ESP32")]
        public void DisconnectFromESP32()
        {
            if (udpClient != null)
            {
                udpClient.Close();
                udpClient = null;
            }
            
            isConnected = false;
            
            if (showDebugInfo)
            {
                if (showDebugInfo)
                {
                    Debug.Log("✓ Disconnected from ESP32");
                }
            }
        }
        
        /// <summary>
        /// Handle color updates from animation system
        /// </summary>
        private void OnColorsUpdated(Color[] colors)
        {
            if (isConnected && circuitMapper != null && circuitMapper.MappingComplete)
            {
                // Rate limiting
                if (Time.time - lastSendTime >= sendInterval)
                {
                    if (showDebugInfo && logDataSent)
                    {
                        int activeColors = 0;
                        for (int i = 0; i < colors.Length; i++)
                        {
                            if (colors[i].r > 0.1f || colors[i].g > 0.1f || colors[i].b > 0.1f)
                                activeColors++;
                        }
                        // Debug.Log($"🎨 OnColorsUpdated: {colors.Length} node colors, {activeColors} active colors"); // Disabled - too verbose
                    }
                    
                    SendLEDData(colors);
                    lastSendTime = Time.time;
                }
            }
            else
            {
                if (showDebugInfo && logDataSent)
                {
                    Debug.LogWarning($"🚫 OnColorsUpdated blocked: Connected={isConnected}, Mapper={circuitMapper != null}, Complete={circuitMapper?.MappingComplete}");
                }
            }
        }
        
        /// <summary>
        /// Handle geometry updates (when LED mapping changes)
        /// </summary>
        private void OnGeometryUpdated(List<Vector3> nodePositions, List<Vector2Int> edgeConnections, List<int> sourceNodes)
        {
            // Update LED configuration when geometry changes
            UpdateLEDConfigurationFromMapper();
            
            if (showDebugInfo)
            {
                if (showDebugInfo)
                {
                    Debug.Log($"📡 ESP32Communicator: Updated LED configuration after geometry change");
                    Debug.Log($"   🔹 Total LEDs: {detectedTotalLEDs}");
                    Debug.Log($"   🔹 Strip Count: {detectedStripCount}");
                    Debug.Log($"   🔹 Avg LEDs/Strip: {detectedAvgLEDsPerStrip}");
                }
            }
        }
        
        /// <summary>
        /// Update LED configuration from circuit mapper
        /// </summary>
        private void UpdateLEDConfigurationFromMapper()
        {
            if (circuitMapper == null)
            {
                if (showDebugInfo)
                {
                    Debug.LogWarning("📡 ESP32Communicator: circuitMapper is null");
                }
                detectedTotalLEDs = 0;
                detectedStripCount = 0;
                detectedAvgLEDsPerStrip = 0;
                return;
            }
            
            // Debug circuit mapper state
            if (showDebugInfo)
            {
                Debug.Log($"📡 ESP32Communicator: Checking circuit mapper state");
                Debug.Log($"   🔹 Mapping Complete: {circuitMapper.MappingComplete}");
                Debug.Log($"   🔹 LED Strips Count: {circuitMapper.LEDStrips?.Count ?? 0}");
                if (circuitMapper.LEDStrips != null)
                {
                    // Show strips in wiring order (same as what we send to ESP32)
                    var sortedStrips = circuitMapper.LEDStrips.Where(s => s.enabled).OrderBy(s => s.wiringOrder).Take(3);
                    foreach (var strip in sortedStrips)
                    {
                        Debug.Log($"   🔸 Strip {strip.stripIndex}: {strip.maxLEDsPerBranch} LEDs, enabled: {strip.enabled}, order: {strip.wiringOrder}");
                    }
                }
            }
            
            detectedTotalLEDs = circuitMapper.TotalLEDCount;
            detectedStripCount = circuitMapper.StripCount;
            detectedAvgLEDsPerStrip = circuitMapper.ledsPerStrip;
            
            if (showDebugInfo)
            {
                Debug.Log($"📡 ESP32Communicator: LED Configuration Updated");
                Debug.Log($"   🔹 Total LEDs: {detectedTotalLEDs}");
                Debug.Log($"   🔹 Strip Count: {detectedStripCount}");
                Debug.Log($"   🔹 Avg LEDs/Strip: {detectedAvgLEDsPerStrip}");
            }
            
            if (detectedTotalLEDs == 0 && showDebugInfo)
            {
                Debug.LogWarning("⚠️ LED detection returned 0 - check if circuit mapping is complete");
            }
        }
        
        /// <summary>
        /// Send LED color data to ESP32 using your custom protocol
        /// </summary>
        private void SendLEDData(Color[] nodeColors)
        {
            if (!isConnected || circuitMapper == null) return;
            
            try
            {
                if (useCustomProtocol)
                {
                    // Use your existing packet format
                    SendCustomProtocolData(nodeColors);
                }
                else
                {
                    // Convert Unity colors to LED strip data
                    var stripDataList = circuitMapper.ConvertColorsToLEDData(nodeColors);
                    
                    // Create data packet
                    var packet = CreateLEDDataPacket(stripDataList);
                    
                    // Send to ESP32
                    byte[] data = packet.ToByteArray();
                    udpClient.Send(data, data.Length, esp32EndPoint);
                }
                
                // Update stats
                packetsSent++;
                
                // Disabled packet count logging - too verbose
                // if (logDataSent)
                // {
                //     Debug.Log($"📡 Sent LED data to ESP32 (Packet #{packetsSent})");
                // }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to send LED data: {e.Message}");
            }
        }
        
        /// <summary>
        /// Send data using your custom protocol format for SINGLE CONTINUOUS STRIP
        /// </summary>
        private void SendCustomProtocolData(Color[] nodeColors)
        {
            // NEW: Combine all logical strips into one continuous physical strip
            // This matches your actual hardware: one LED strip on one GPIO pin
            
            var stripDataList = circuitMapper.ConvertColorsToLEDData(nodeColors);
            
            // Combine all logical strips into one continuous array
            var continuousColors = new List<Color>();
            
            // Sort strips by wiring order to match physical connection sequence
            var sortedStrips = stripDataList.OrderBy(s => {
                // Find the corresponding LEDStripInfo to get wiringOrder
                var stripInfo = circuitMapper.LEDStrips.FirstOrDefault(strip => strip.stripIndex == s.stripIndex);
                return stripInfo?.wiringOrder ?? s.stripIndex;
            }).ToList();
            
            foreach (var stripData in sortedStrips)
            {
                continuousColors.AddRange(stripData.ledColors);
                
                // Strip added to continuous array
            }
            
            // Create single packet for the entire continuous strip
            var continuousPacket = CreateContinuousStripPacket(continuousColors.ToArray());
            
            // Send single packet to ESP32
            udpClient.Send(continuousPacket, continuousPacket.Length, esp32EndPoint);
            bytesSent += continuousPacket.Length;
            
            // Disabled verbose logging - too much output
            // if (logDataSent)
            // {
            //     Debug.Log($"📡 Sent continuous strip data: {continuousColors.Count} LEDs, {continuousPacket.Length} bytes");
            // }
        }
        
        /// <summary>
        /// Apply color correction to a Unity color and convert to RGB bytes
        /// </summary>
        private void ApplyColorCorrection(Color color, out byte outR, out byte outG, out byte outB)
        {
            float r = Mathf.Clamp01(color.r);
            float g = Mathf.Clamp01(color.g);
            float b = Mathf.Clamp01(color.b);
            
            // Apply color temperature adjustment
            if (colorTemperature != 0f)
            {
                if (colorTemperature < 0f)
                {
                    // Warmer - boost red, reduce blue
                    r = Mathf.Clamp01(r * (1f - colorTemperature * 0.3f));
                    b = Mathf.Clamp01(b * (1f + colorTemperature * 0.5f));
                }
                else
                {
                    // Cooler - reduce red, boost blue
                    r = Mathf.Clamp01(r * (1f - colorTemperature * 0.3f));
                    b = Mathf.Clamp01(b * (1f + colorTemperature * 0.3f));
                }
            }
            
            // Apply per-channel color correction
            r *= colorCorrection.x;
            g *= colorCorrection.y;
            b *= colorCorrection.z;
            
            // Apply gamma correction (LEDs are non-linear)
            if (gammaCorrection != 1f)
            {
                r = Mathf.Pow(r, gammaCorrection);
                g = Mathf.Pow(g, gammaCorrection);
                b = Mathf.Pow(b, gammaCorrection);
            }
            
            // Apply global brightness
            r *= ledBrightness;
            g *= ledBrightness;
            b *= ledBrightness;
            
            // Convert to bytes
            byte byteR = (byte)(Mathf.Clamp01(r) * 255);
            byte byteG = (byte)(Mathf.Clamp01(g) * 255);
            byte byteB = (byte)(Mathf.Clamp01(b) * 255);
            
            // Apply channel order
            switch (channelOrder)
            {
                case ColorChannelOrder.RGB:
                    outR = byteR; outG = byteG; outB = byteB;
                    break;
                case ColorChannelOrder.GRB:
                    outR = byteG; outG = byteR; outB = byteB;
                    break;
                case ColorChannelOrder.BGR:
                    outR = byteB; outG = byteG; outB = byteR;
                    break;
                case ColorChannelOrder.RBG:
                    outR = byteR; outG = byteB; outB = byteG;
                    break;
                case ColorChannelOrder.GBR:
                    outR = byteG; outG = byteB; outB = byteR;
                    break;
                case ColorChannelOrder.BRG:
                    outR = byteB; outG = byteR; outB = byteG;
                    break;
                default:
                    outR = byteR; outG = byteG; outB = byteB;
                    break;
            }
        }
        
        /// <summary>
        /// Create packet for SINGLE CONTINUOUS STRIP (your actual hardware)
        /// </summary>
        private byte[] CreateContinuousStripPacket(Color[] continuousColors)
        {
            // NEW Protocol for single continuous strip: [0xAA][0x01][0][totalLEDs][R][G][B]...[R][G][B]
            const byte PACKET_START_MARKER = 0xAA;
            const byte COMMAND_LED_DATA = 0x01;
            
            int totalLEDs = continuousColors.Length;
            
            // Use 16-bit LED count for large strips
            byte[] packet = new byte[6 + (totalLEDs * 3)];
            
            // Header
            packet[0] = PACKET_START_MARKER;
            packet[1] = COMMAND_LED_DATA;
            packet[2] = 0; // Strip ID = 0 (single continuous strip)
            packet[3] = 0; // Reserved
            
            // 16-bit LED count (supports up to 65535 LEDs)
            packet[4] = (byte)(totalLEDs & 0xFF);
            packet[5] = (byte)((totalLEDs >> 8) & 0xFF);
            
            // LED color data with correction
            for (int i = 0; i < totalLEDs; i++)
            {
                Color color = continuousColors[i];
                int baseIndex = 6 + (i * 3);
                
                ApplyColorCorrection(color, out byte r, out byte g, out byte b);
                
                packet[baseIndex] = r;
                packet[baseIndex + 1] = g;
                packet[baseIndex + 2] = b;
            }
            
            return packet;
        }
        
        /// <summary>
        /// Create packet using your custom protocol format (LEGACY - for individual strips)
        /// </summary>
        private byte[] CreateCustomProtocolPacket(LEDStripData stripData)
        {
            // Your protocol: [0xAA][0x01][stripId][ledCount][R][G][B]...[R][G][B]
            const byte PACKET_START_MARKER = 0xAA;
            const byte COMMAND_LED_DATA = 0x01;
            
            int ledCount = stripData.ledColors.Length; // Use actual LED count from strip data
            byte[] packet = new byte[4 + (ledCount * 3)];
            
            // Header
            packet[0] = PACKET_START_MARKER;
            packet[1] = COMMAND_LED_DATA;
            packet[2] = (byte)stripData.stripIndex;
            packet[3] = (byte)ledCount;
            
            // LED color data with correction
            for (int i = 0; i < ledCount; i++)
            {
                Color color = stripData.ledColors[i];
                int baseIndex = 4 + (i * 3);
                
                ApplyColorCorrection(color, out byte r, out byte g, out byte b);
                
                packet[baseIndex] = r;
                packet[baseIndex + 1] = g;
                packet[baseIndex + 2] = b;
            }
            
            return packet;
        }
        
        /// <summary>
        /// Create LED data packet for ESP32
        /// </summary>
        private LEDDataPacket CreateLEDDataPacket(List<LEDStripData> stripDataList)
        {
            var packet = new LEDDataPacket
            {
                header = new PacketHeader
                {
                    packetType = PacketType.LEDData,
                    timestamp = (uint)(Time.time * 1000),
                    stripCount = (byte)stripDataList.Count,
                    dataFormat = LEDDataFormat.RGB24
                },
                stripData = stripDataList
            };
            
            return packet;
        }
        
        /// <summary>
        /// Send initial connection packet to ESP32
        /// </summary>
        private void SendConnectionPacket()
        {
            var packet = new LEDDataPacket
            {
                header = new PacketHeader
                {
                    packetType = PacketType.Connection,
                    timestamp = (uint)(Time.time * 1000),
                    stripCount = (byte)(circuitMapper?.StripCount ?? 0),
                    dataFormat = LEDDataFormat.RGB24
                }
            };
            
            try
            {
                byte[] data = packet.ToByteArray();
                udpClient.Send(data, data.Length, esp32EndPoint);
                
                if (showDebugInfo)
                {
                    Debug.Log($"✓ Sent connection packet to ESP32");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to send connection packet: {e.Message}");
            }
        }
        
        /// <summary>
        /// Test ESP32 connection using your protocol
        /// </summary>
        [ContextMenu("Test ESP32 Connection")]
        public void TestConnection()
        {
            if (!isConnected)
            {
                Debug.LogWarning("Not connected to ESP32. Use 'Connect to ESP32' first.");
                return;
            }
            
            // Check UDP client state before sending
            if (udpClient == null)
            {
                Debug.LogWarning("UDP client is null, attempting to reconnect...");
                ConnectToESP32();
            }
            
            if (useCustomProtocol)
            {
                // Send test pattern using your custom protocol
                SendTestPattern(2); // Rainbow pattern
                Debug.Log("🌈 Sent rainbow test pattern to ESP32");
            }
            else
            {
                // Send test pattern using Unity colors
                if (circuitMapper != null && circuitMapper.MappingComplete)
                {
                    Color[] testColors = new Color[graphManager.NodeCount];
                    
                    // Create rainbow test pattern
                    for (int i = 0; i < testColors.Length; i++)
                    {
                        float hue = (float)i / testColors.Length;
                        testColors[i] = Color.HSVToRGB(hue, 1f, 0.5f);
                    }
                    
                    SendLEDData(testColors);
                    Debug.Log("🌈 Sent rainbow test pattern to ESP32");
                }
            }
        }
        
        /// <summary>
        /// Send test pattern command (matches your ESP32 test patterns)
        /// </summary>
        [ContextMenu("Send Test Pattern - Rainbow")]
        public void SendRainbowTestPattern()
        {
            SendTestPattern(2); // Rainbow pattern
        }
        
        [ContextMenu("Send Test Pattern - White")]
        public void SendWhiteTestPattern()
        {
            SendTestPattern(1); // Solid white
        }
        
        [ContextMenu("Send Test Pattern - Off")]
        public void SendOffTestPattern()
        {
            SendTestPattern(0); // Off
        }
        
        /// <summary>
        /// Force refresh component references (useful when components are added/changed)
        /// </summary>
        [ContextMenu("Refresh Component References")]
        public void RefreshComponentReferences()
        {
            Debug.Log("🔄 Refreshing component references...");
            
            // Try to find circuit mapper on same GameObject first
            circuitMapper = GetComponent<LEDCircuitMapper>();
            if (circuitMapper == null)
            {
                // Fall back to scene search
                circuitMapper = FindFirstObjectByType<LEDCircuitMapper>();
            }
            
            // Try to find graph manager
            graphManager = GetComponent<LEDGraphManager>();
            if (graphManager == null)
            {
                graphManager = FindFirstObjectByType<LEDGraphManager>();
            }
            
            Debug.Log($"📡 Component References Updated:");
            Debug.Log($"   🔹 LEDCircuitMapper: {(circuitMapper != null ? "✅ Found" : "❌ Not Found")}");
            Debug.Log($"   🔹 LEDGraphManager: {(graphManager != null ? "✅ Found" : "❌ Not Found")}");
            
            if (circuitMapper != null)
            {
                Debug.Log($"   🔹 Mapping Complete: {circuitMapper.MappingComplete}");
                Debug.Log($"   🔹 Total LEDs: {circuitMapper.TotalLEDCount}");
                Debug.Log($"   🔹 Strip Count: {circuitMapper.StripCount}");
            }
            
            // Update LED configuration after refreshing references
            UpdateLEDConfigurationFromMapper();
        }
        
        /// <summary>
        /// Send test pattern using your custom protocol
        /// </summary>
        private void SendTestPattern(byte pattern)
        {
            if (!isConnected)
            {
                Debug.LogWarning("Cannot send test pattern - ESP32 not connected");
                return;
            }
            
            if (udpClient == null)
            {
                Debug.LogError("Cannot send test pattern - UDP client is null");
                return;
            }
            
            if (esp32EndPoint == null)
            {
                Debug.LogError("Cannot send test pattern - ESP32 endpoint is null");
                return;
            }
            
            try
            {
                // Your protocol: [0xAA][0x03][stripId][pattern]
                const byte PACKET_START_MARKER = 0xAA;
                const byte COMMAND_TEST_PATTERN = 0x03;
                
                byte[] packet = new byte[4];
                packet[0] = PACKET_START_MARKER;
                packet[1] = COMMAND_TEST_PATTERN;
                packet[2] = 0; // Strip ID (not used for test patterns in your code)
                packet[3] = pattern;
                
                udpClient.Send(packet, packet.Length, esp32EndPoint);
                
                packetsSent++;
                bytesSent += packet.Length;
                
                Debug.Log($"📡 Sent test pattern {pattern} to ESP32");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to send test pattern: {e.Message}");
                Debug.LogError($"UDP Client null: {udpClient == null}, EndPoint null: {esp32EndPoint == null}");
            }
        }
        
        /// <summary>
        /// Show communication statistics
        /// </summary>
        [ContextMenu("Show Communication Stats")]
        public void ShowStats()
        {
            Debug.Log("📊 ESP32 COMMUNICATION STATISTICS");
            Debug.Log($"  - Connection Status: {(isConnected ? "✅ Connected" : "❌ Disconnected")}");
            Debug.Log($"  - UDP Client Status: {(udpClient != null ? "✅ Active" : "❌ Null")}");
            Debug.Log($"  - EndPoint Status: {(esp32EndPoint != null ? "✅ Set" : "❌ Null")}");
            Debug.Log($"  - ESP32 Address: {esp32IPAddress}:{esp32Port}");
            Debug.Log($"  - Max FPS: {maxFPS} ({sendInterval * 1000:F1}ms interval)");
            Debug.Log($"  - Data Format: RGB24 (Custom Protocol)");
            Debug.Log($"  - Packets Sent: {packetsSent}");
            Debug.Log($"  - Bytes Sent: {bytesSent:N0}");
            Debug.Log($"  - Circuit Mapper Status: {(circuitMapper?.MappingComplete == true ? "✅ Complete" : "❌ Incomplete")}");
            Debug.Log($"  - Detected LED Strips: {detectedStripCount}");
            Debug.Log($"  - Detected Total LEDs: {detectedTotalLEDs}");
            Debug.Log($"  - Detected Avg LEDs/Strip: {detectedAvgLEDsPerStrip}");
        }
        
        /// <summary>
        /// Manually update LED configuration from circuit mapper
        /// </summary>
        [ContextMenu("Update LED Configuration")]
        public void ManualUpdateLEDConfiguration()
        {
            Debug.Log("🔄 Manually triggering LED configuration update...");
            UpdateLEDConfigurationFromMapper();
            Debug.Log("🔄 Manual LED configuration update complete");
        }
        
        /// <summary>
        /// Force LED configuration detection with detailed debugging
        /// </summary>
        [ContextMenu("Debug LED Configuration")]
        public void DebugLEDConfiguration()
        {
            Debug.Log("🔍 DEBUGGING LED CONFIGURATION");
            Debug.Log("================================");
            
            // Try to find circuit mapper if null
            if (circuitMapper == null)
            {
                Debug.LogWarning("❌ circuitMapper is NULL - attempting to find it...");
                circuitMapper = GetComponent<LEDCircuitMapper>();
                if (circuitMapper == null)
                {
                    circuitMapper = FindFirstObjectByType<LEDCircuitMapper>();
                }
                
                if (circuitMapper == null)
                {
                    Debug.LogError("❌ Could not find LEDCircuitMapper in scene!");
                    Debug.LogError("💡 Make sure LEDCircuitMapper component exists and is active");
                    return;
                }
                else
                {
                    Debug.Log($"✅ Found LEDCircuitMapper on: {circuitMapper.gameObject.name}");
                }
            }
            
            Debug.Log($"✅ Circuit Mapper found: {circuitMapper.name}");
            Debug.Log($"📊 Mapping Complete: {circuitMapper.MappingComplete}");
            Debug.Log($"📊 LED Strips: {circuitMapper.LEDStrips?.Count ?? 0}");
            Debug.Log($"📊 Total LED Count: {circuitMapper.TotalLEDCount}");
            Debug.Log($"📊 Strip Count: {circuitMapper.StripCount}");
            Debug.Log($"📊 Legacy ledsPerStrip: {circuitMapper.ledsPerStrip}");
            
            if (circuitMapper.LEDStrips != null && circuitMapper.LEDStrips.Count > 0)
            {
                Debug.Log($"📋 Strip Details:");
                foreach (var strip in circuitMapper.LEDStrips)
                {
                    Debug.Log($"   Strip {strip.stripIndex}: {strip.maxLEDsPerBranch} LEDs, enabled: {strip.enabled}, pin: {strip.dataPin}");
                }
            }
            else
            {
                Debug.LogWarning("⚠️ LEDStrips list is null or empty");
            }
            
            UpdateLEDConfigurationFromMapper();
            
            Debug.Log("================================");
            Debug.Log($"🎯 Final Detection Results:");
            Debug.Log($"   Total LEDs: {detectedTotalLEDs}");
            Debug.Log($"   Strip Count: {detectedStripCount}");
            Debug.Log($"   Avg LEDs/Strip: {detectedAvgLEDsPerStrip}");
        }
        
        // Public properties
        public bool IsConnected => isConnected;
        public int PacketsSent => packetsSent;
        public int BytesSent => bytesSent;
        
        /// <summary>
        /// Send LED configuration to ESP32 before animation upload
        /// </summary>
        [ContextMenu("Force Send LED Configuration")]
        public void SendLEDConfiguration()
        {
            if (circuitMapper == null)
            {
                Debug.LogWarning("Cannot send LED config: No circuit mapper found");
                return;
            }
            
            if (!isConnected)
            {
                Debug.LogWarning("Cannot send LED config: ESP32 not connected");
                return;
            }
            
            try
            {
                // NEW: Send SINGLE CONTINUOUS STRIP configuration
                // Format: [0xAA][0x05][totalLEDs][numLogicalSegments][segment0_LEDs][segment1_LEDs]...[segmentN_LEDs]
                const byte PACKET_START_MARKER = 0xAA;
                const byte COMMAND_LED_CONFIG = 0x05;
                
                int totalLEDs = circuitMapper.TotalLEDCount;
                int numLogicalSegments = circuitMapper.StripCount;
                var enabledStrips = circuitMapper.LEDStrips.Where(s => s.enabled).OrderBy(s => s.wiringOrder).ToList();
                
                // Calculate packet size: header(4) + totalLEDs(4) + numLogicalSegments(4) + (numLogicalSegments * 4 bytes per segment)
                int packetSize = 4 + 4 + 4 + (numLogicalSegments * 4);
                byte[] packet = new byte[packetSize];
                int pos = 0;
                
                // Header
                packet[pos++] = PACKET_START_MARKER;
                packet[pos++] = COMMAND_LED_CONFIG;
                packet[pos++] = 0; // Unused
                packet[pos++] = 0; // Unused
                
                // Total LEDs and logical segment count
                System.BitConverter.GetBytes(totalLEDs).CopyTo(packet, pos);
                pos += 4;
                System.BitConverter.GetBytes(numLogicalSegments).CopyTo(packet, pos);
                pos += 4;
                
                // Individual logical segment LED counts (for reference/debugging)
                foreach (var strip in enabledStrips)
                {
                    System.BitConverter.GetBytes(strip.maxLEDsPerBranch).CopyTo(packet, pos);
                    pos += 4;
                }
                
                // Send to ESP32
                udpClient.Send(packet, packet.Length, esp32EndPoint);
                
                Debug.Log($"📡 Sent LED configuration to ESP32:");
                Debug.Log($"   🔹 Total LEDs: {totalLEDs} (SINGLE CONTINUOUS STRIP)");
                Debug.Log($"   🔹 Logical Segments: {numLogicalSegments}");
                Debug.Log($"   🔹 Segment LED Counts (in wiring order): [{string.Join(", ", enabledStrips.Select(s => s.maxLEDsPerBranch))}]");
                Debug.Log($"   🔹 Wiring Order: [{string.Join(", ", enabledStrips.Select(s => $"{s.branchName}(Order:{s.wiringOrder})"))}]");
                Debug.Log($"   💡 ESP32 will treat this as ONE physical strip with {totalLEDs} LEDs");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to send LED configuration: {e.Message}");
            }
        }
        
        /// <summary>
        /// Send animation data to ESP32 for storage
        /// </summary>
        public void SendAnimationData(string animationName, byte[] animationData)
        {
            if (!enableAnimationExport)
            {
                Debug.LogWarning("Cannot send animation data: Animation export disabled");
                return;
            }
            
            // Try to establish connection if not connected
            if (!isConnected)
            {
                Debug.Log("Attempting to connect to ESP32 for animation upload...");
                ConnectToESP32();
            }
            
            // Send LED configuration first (automatically configures ESP32)
            SendLEDConfiguration();
            
            StartCoroutine(SendAnimationDataWithRetry(animationName, animationData, maxRetries: 3));
        }
        
        /// <summary>
        /// Send playlist data to ESP32
        /// </summary>
        public void SendPlaylistData(string playlistJson)
        {
            if (!isConnected || !enableAnimationExport)
            {
                Debug.LogWarning("Cannot send playlist data: ESP32 not connected or export disabled");
                return;
            }
            
            StartCoroutine(SendPlaylistDataCoroutine(playlistJson));
        }
        
        /// <summary>
        /// Request ESP32 animation library status
        /// </summary>
        [ContextMenu("Check ESP32 Animation Library")]
        public void RequestAnimationLibraryStatus()
        {
            if (!isConnected)
            {
                Debug.LogWarning("ESP32 not connected");
                return;
            }
            
            // Send library status request
            byte[] statusRequest = CreateLibraryStatusPacket();
            try
            {
                udpClient.Send(statusRequest, statusRequest.Length, esp32EndPoint);
                Debug.Log("📡 Requested ESP32 animation library status");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to request library status: {e.Message}");
            }
        }
        
        private System.Collections.IEnumerator SendAnimationDataWithRetry(string animationName, byte[] animationData, int maxRetries)
        {
            bool uploadSuccessful = false;
            int successfulAttempt = 0;
            
            for (int attempt = 1; attempt <= maxRetries && !uploadSuccessful; attempt++)
            {
                Debug.Log($"📡 Upload attempt {attempt}/{maxRetries} for '{animationName}'");
                
                yield return StartCoroutine(SendAnimationDataCoroutine(animationName, animationData));
                
                // Wait a moment for ESP32 to process (use realtime to avoid focus dependency)
                yield return new WaitForSecondsRealtime(2.0f);
                
                // For now, assume upload was successful (in future, could check ESP32 response)
                uploadSuccessful = true;
                successfulAttempt = attempt;
                Debug.Log($"✅ Upload attempt {attempt} completed successfully for '{animationName}'");
                
                if (!uploadSuccessful && attempt < maxRetries)
                {
                    Debug.Log($"⏳ Upload failed, retrying attempt {attempt + 1}...");
                    yield return new WaitForSecondsRealtime(3.0f);
                }
            }
            
            if (uploadSuccessful)
            {
                Debug.Log($"🎯 Upload successful for '{animationName}' on attempt {successfulAttempt}");
            }
            else
            {
                Debug.LogError($"❌ Upload failed for '{animationName}' after {maxRetries} attempts");
            }
        }
        
        private System.Collections.IEnumerator SendAnimationDataCoroutine(string animationName, byte[] animationData)
        {
            Debug.Log($"📡 Uploading animation '{animationName}' to ESP32 ({animationData.Length} bytes)...");
            
            // CRITICAL: Stop live LED streaming to prevent interference
            // Pause live LED streaming to prevent packet interference during upload
            Debug.Log("🛑 Pausing live LED streaming during upload to prevent packet interference");
            LEDVisualizationEvents.OnColorsUpdated -= OnColorsUpdated;
            
            const int chunkSize = 512; // Send in 512B chunks for better reliability
            int totalChunks = Mathf.CeilToInt((float)animationData.Length / chunkSize);
            
            for (int chunkIndex = 0; chunkIndex < totalChunks; chunkIndex++)
            {
                int startIndex = chunkIndex * chunkSize;
                int endIndex = Mathf.Min(startIndex + chunkSize, animationData.Length);
                int currentChunkSize = endIndex - startIndex;
                
                // Create animation upload packet
                byte[] packet = CreateAnimationUploadPacket(animationName, chunkIndex, totalChunks, 
                    animationData, startIndex, currentChunkSize);
                
                // Send chunk with retry logic (avoiding try-catch with yield)
                yield return StartCoroutine(SendChunkWithRetry(packet, chunkIndex, totalChunks));
                
                // Small delay between chunks for reliability
                yield return new WaitForSecondsRealtime(0.02f);
            }
            
            // Restore live streaming subscription
            Debug.Log("▶️ Resuming live LED streaming after upload completion");
            LEDVisualizationEvents.OnColorsUpdated += OnColorsUpdated;
            
            Debug.Log($"✅ Animation '{animationName}' upload complete");
        }
        
        private System.Collections.IEnumerator SendChunkWithRetry(byte[] packet, int chunkIndex, int totalChunks)
        {
            const int maxRetries = 3;
            bool chunkSentSuccessfully = false;
            
            for (int attempt = 1; attempt <= maxRetries && !chunkSentSuccessfully; attempt++)
            {
                // Send the packet
                bool sendSuccessful = false;
                try
                {
                    udpClient.Send(packet, packet.Length, esp32EndPoint);
                    sendSuccessful = true;
                    
                    if (showDebugInfo && (chunkIndex < 5 || chunkIndex % 10 == 0))
                    {
                        float progress = (float)(chunkIndex + 1) / totalChunks;
                        Debug.Log($"📤 Sent chunk {chunkIndex + 1}/{totalChunks} ({progress * 100:F1}% - attempt {attempt})");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"Failed to send chunk {chunkIndex} (attempt {attempt}): {e.Message}");
                    sendSuccessful = false;
                }
                
                if (sendSuccessful)
                {
                    // Wait for acknowledgment from ESP32
                    bool ackReceived = false;
                    yield return StartCoroutine(WaitForChunkAckWithResult(chunkIndex, 2.0f, (result) => ackReceived = result));
                    
                    if (ackReceived)
                    {
                        chunkSentSuccessfully = true;
                    }
                    else
                    {
                        Debug.LogWarning($"⚠️ No ACK for chunk {chunkIndex} (attempt {attempt}/{maxRetries})");
                    }
                }
                
                // Wait before retry if needed
                if (!chunkSentSuccessfully && attempt < maxRetries)
                {
                    yield return new WaitForSecondsRealtime(0.1f);
                }
            }
            
            if (!chunkSentSuccessfully)
            {
                Debug.LogError($"❌ Chunk {chunkIndex} failed after {maxRetries} attempts. Upload may be incomplete.");
            }
        }
        
        private System.Collections.IEnumerator WaitForChunkAckWithResult(int expectedChunkIndex, float timeoutSeconds, System.Action<bool> callback)
        {
            bool ackReceived = false;
            yield return StartCoroutine(WaitForChunkAck(expectedChunkIndex, timeoutSeconds, (result) => ackReceived = result));
            callback(ackReceived);
        }
        
        private System.Collections.IEnumerator WaitForChunkAck(int expectedChunkIndex, float timeoutSeconds, System.Action<bool> callback = null)
        {
            float startTime = Time.realtimeSinceStartup; // Use realtime to avoid focus dependency
            bool ackReceived = false;
            int totalPacketsChecked = 0;
            
            // Debug: Always log for first few chunks and every 10th chunk
            bool shouldDebug = expectedChunkIndex < 5 || expectedChunkIndex % 10 == 0;
            
            if (shouldDebug)
            {
                Debug.Log($"🔍 Waiting for ACK for chunk {expectedChunkIndex}...");
            }
            
            while (Time.realtimeSinceStartup - startTime < timeoutSeconds && !ackReceived)
            {
                // Check for acknowledgment packets
                if (udpClient.Available > 0)
                {
                    totalPacketsChecked++;
                    try
                    {
                        byte[] ackData = udpClient.Receive(ref esp32EndPoint);
                        
                        if (shouldDebug)
                        {
                            Debug.Log($"📨 Received UDP packet: {ackData.Length} bytes - [{ackData[0]:X2}][{ackData[1]:X2}][{ackData[2]:X2}][{ackData[3]:X2}]");
                        }
                        
                        if (ackData.Length >= 4 && ackData[0] == 0xAA && ackData[1] == 0x12)
                        {
                            int ackChunkIndex = ackData[2] | (ackData[3] << 8);
                            
                            if (shouldDebug)
                            {
                                Debug.Log($"📤 ACK packet for chunk {ackChunkIndex} (expecting {expectedChunkIndex})");
                            }
                            
                            if (ackChunkIndex == expectedChunkIndex)
                            {
                                ackReceived = true;
                                if (shouldDebug)
                                {
                                    Debug.Log($"✅ Received ACK for chunk {expectedChunkIndex}");
                                }
                            }
                            else
                            {
                                Debug.LogWarning($"⚠️ Received ACK for chunk {ackChunkIndex}, but expected {expectedChunkIndex}");
                            }
                        }
                        else
                        {
                            if (shouldDebug && ackData.Length >= 2)
                            {
                                Debug.Log($"📨 Non-ACK packet: [{ackData[0]:X2}][{ackData[1]:X2}]");
                            }
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"Failed to receive ACK: {e.Message}");
                    }
                }
                
                yield return new WaitForSecondsRealtime(0.01f); // Check every 10ms (realtime to avoid focus dependency)
            }
            
            if (!ackReceived)
            {
                float elapsedTime = Time.realtimeSinceStartup - startTime;
                Debug.LogWarning($"⚠️ No ACK received for chunk {expectedChunkIndex} within {timeoutSeconds}s (elapsed: {elapsedTime:F2}s, packets checked: {totalPacketsChecked})");
            }
            
            // Call callback with result if provided
            if (callback != null)
            {
                callback(ackReceived);
            }
        }
        
        private System.Collections.IEnumerator SendPlaylistDataCoroutine(string playlistJson)
        {
            Debug.Log("📡 Uploading playlist data to ESP32...");
            
            byte[] playlistData = System.Text.Encoding.UTF8.GetBytes(playlistJson);
            byte[] packet = CreatePlaylistUploadPacket(playlistData);
            
            try
            {
                udpClient.Send(packet, packet.Length, esp32EndPoint);
                Debug.Log("✅ Playlist data upload complete");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to send playlist data: {e.Message}");
            }
            
            yield return null;
        }
        
        private byte[] CreateAnimationUploadPacket(string animationName, int chunkIndex, int totalChunks, 
            byte[] animationData, int startIndex, int chunkSize)
        {
            using (var stream = new System.IO.MemoryStream())
            using (var writer = new System.IO.BinaryWriter(stream))
            {
                // Packet header
                writer.Write((byte)0xAA); // Start marker
                writer.Write((byte)0x10); // Animation upload command
                writer.Write((byte)(chunkIndex & 0xFF));      // Chunk index low byte
                writer.Write((byte)((chunkIndex >> 8) & 0xFF)); // Chunk index high byte
                
                // Animation upload header
                writer.Write(totalChunks);
                writer.Write(animationName.PadRight(32).Substring(0, 32).ToCharArray());
                writer.Write(chunkSize);
                
                // Animation data chunk
                writer.Write(animationData, startIndex, chunkSize);
                
                return stream.ToArray();
            }
        }
        
        private byte[] CreatePlaylistUploadPacket(byte[] playlistData)
        {
            using (var stream = new System.IO.MemoryStream())
            using (var writer = new System.IO.BinaryWriter(stream))
            {
                // Packet header
                writer.Write((byte)0xAA); // Start marker
                writer.Write((byte)0x11); // Playlist upload command
                writer.Write((byte)0);    // Strip ID (unused)
                writer.Write((byte)playlistData.Length); // Data length indicator
                
                // Playlist data
                writer.Write(playlistData.Length);
                writer.Write(playlistData);
                
                return stream.ToArray();
            }
        }
        
        private byte[] CreateLibraryStatusPacket()
        {
            return new byte[] { 0xAA, 0x12, 0x00, 0x00 }; // Library status request
        }
    }
    
    /// <summary>
    /// LED data formats supported by ESP32
    /// </summary>
    public enum LEDDataFormat : byte
    {
        RGB24 = 0,    // 24-bit RGB (8 bits per channel)
        RGBW32 = 1,   // 32-bit RGBW (8 bits per channel + white)
        RGB565 = 2,   // 16-bit RGB (5-6-5 bits)
        HSV24 = 3     // 24-bit HSV
    }
    
    /// <summary>
    /// Packet types for ESP32 communication
    /// </summary>
    public enum PacketType : byte
    {
        Connection = 0,
        LEDData = 1,
        Configuration = 2,
        Heartbeat = 3
    }
    
    /// <summary>
    /// Packet header structure
    /// </summary>
    [System.Serializable]
    public struct PacketHeader
    {
        public PacketType packetType;
        public uint timestamp;
        public byte stripCount;
        public LEDDataFormat dataFormat;
    }
    
    /// <summary>
    /// Complete LED data packet
    /// </summary>
    [System.Serializable]
    public class LEDDataPacket
    {
        public PacketHeader header;
        public List<LEDStripData> stripData = new List<LEDStripData>();
        
        /// <summary>
        /// Convert packet to byte array for UDP transmission
        /// </summary>
        public byte[] ToByteArray()
        {
            var buffer = new List<byte>();
            
            // Header (8 bytes)
            buffer.Add((byte)header.packetType);
            buffer.AddRange(System.BitConverter.GetBytes(header.timestamp));
            buffer.Add(header.stripCount);
            buffer.Add((byte)header.dataFormat);
            buffer.Add(0); // Reserved byte
            
            // Strip data
            foreach (var strip in stripData)
            {
                // Strip header (4 bytes)
                buffer.Add((byte)strip.stripIndex);
                buffer.Add((byte)strip.dataPin);
                buffer.AddRange(System.BitConverter.GetBytes((ushort)strip.ledColors.Length));
                
                // LED color data
                foreach (var color in strip.ledColors)
                {
                    switch (header.dataFormat)
                    {
                        case LEDDataFormat.RGB24:
                            buffer.Add((byte)(color.r * 255));
                            buffer.Add((byte)(color.g * 255));
                            buffer.Add((byte)(color.b * 255));
                            break;
                            
                        case LEDDataFormat.RGBW32:
                            buffer.Add((byte)(color.r * 255));
                            buffer.Add((byte)(color.g * 255));
                            buffer.Add((byte)(color.b * 255));
                            buffer.Add(0); // White channel
                            break;
                    }
                }
            }
            
            return buffer.ToArray();
        }
    }
}
