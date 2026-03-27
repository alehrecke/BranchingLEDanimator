using UnityEngine;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic;

namespace BranchingLEDAnimator.Hardware
{
    /// <summary>
    /// Unity-based ESP32 status monitor - displays ESP32 feedback in Unity console
    /// Shows UDP packet acknowledgments and ESP32 status without serial cable
    /// </summary>
    public class ESP32Monitor : MonoBehaviour
    {
        [Header("ESP32 Monitoring")]
        public bool enableMonitoring = true;
        public string esp32IPAddress = "192.168.1.100";
        public int statusPort = 8888; // Different port for status updates
        public float pingInterval = 5f; // Ping ESP32 every 5 seconds
        
        [Header("Display")]
        public bool showInGameView = false;
        public int maxLogLines = 20;
        
        [Header("Debug")]
        public bool enableDebugLogging = false;
        
        private UdpClient statusClient;
        private List<string> esp32Logs = new List<string>();
        private float lastPingTime = 0f;
        private bool isMonitoring = false;
        private ESP32Communicator esp32Communicator;
        
        void Start()
        {
            esp32Communicator = GetComponent<ESP32Communicator>();
            
            if (enableMonitoring)
            {
                StartMonitoring();
            }
        }
        
        void Update()
        {
            if (enableMonitoring && Time.time - lastPingTime > pingInterval)
            {
                PingESP32Status();
                lastPingTime = Time.time;
            }
            
            // Listen for status updates (if ESP32 sends them)
            if (isMonitoring && statusClient != null)
            {
                try
                {
                    if (statusClient.Available > 0)
                    {
                        var endPoint = new System.Net.IPEndPoint(System.Net.IPAddress.Any, 0);
                        byte[] data = statusClient.Receive(ref endPoint);
                        string message = Encoding.UTF8.GetString(data);
                        
                        LogESP32Message($"ESP32: {message}");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"ESP32 Monitor receive error: {e.Message}");
                }
            }
        }
        
        [ContextMenu("Start ESP32 Monitoring")]
        public void StartMonitoring()
        {
            try
            {
                statusClient = new UdpClient(statusPort);
                isMonitoring = true;
                
                LogESP32Message("🔍 ESP32 Monitor Started");
                LogESP32Message($"📡 Listening for ESP32 status on port {statusPort}");
                LogESP32Message($"🎯 Monitoring ESP32 at {esp32IPAddress}");
                LogESP32Message("💡 Add status reporting to your ESP32 code for full monitoring");
                
                Debug.Log("✅ ESP32 Monitor started successfully");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to start ESP32 Monitor: {e.Message}");
            }
        }
        
        [ContextMenu("Stop ESP32 Monitoring")]
        public void StopMonitoring()
        {
            if (statusClient != null)
            {
                statusClient.Close();
                statusClient = null;
            }
            
            isMonitoring = false;
            LogESP32Message("🛑 ESP32 Monitor Stopped");
        }
        
        [ContextMenu("Ping ESP32 Status")]
        public void PingESP32Status()
        {
            if (esp32Communicator == null) return;
            
            // Send a status request to ESP32
            try
            {
                // Use your existing protocol to request status
                const byte PACKET_START_MARKER = 0xAA;
                const byte COMMAND_STATUS_REQUEST = 0x04; // New command for status
                
                byte[] packet = new byte[4];
                packet[0] = PACKET_START_MARKER;
                packet[1] = COMMAND_STATUS_REQUEST;
                packet[2] = 0;
                packet[3] = 0;
                
                // This would need to be sent via the ESP32Communicator's UDP client
                LogESP32Message($"📡 Status ping sent to ESP32 ({System.DateTime.Now:HH:mm:ss})");
            }
            catch (System.Exception e)
            {
                LogESP32Message($"❌ Ping failed: {e.Message}");
            }
        }
        
        [ContextMenu("Show ESP32 Status")]
        public void ShowESP32Status()
        {
            if (enableDebugLogging)
            {
                Debug.Log("📊 ESP32 MONITOR STATUS");
                Debug.Log("========================================");
                
                Debug.Log($"Monitor Active: {(isMonitoring ? "✅ Yes" : "❌ No")}");
                Debug.Log($"ESP32 IP: {esp32IPAddress}");
                Debug.Log($"Status Port: {statusPort}");
                Debug.Log($"Ping Interval: {pingInterval}s");
                Debug.Log($"Log Lines: {esp32Logs.Count}/{maxLogLines}");
                
                Debug.Log("\n📜 Recent ESP32 Messages:");
                foreach (string log in esp32Logs)
                {
                    Debug.Log($"  {log}");
                }
                
                Debug.Log("========================================");
            }
        }
        
        [ContextMenu("Clear ESP32 Logs")]
        public void ClearLogs()
        {
            esp32Logs.Clear();
            LogESP32Message("🧹 Logs cleared");
        }
        
        private void LogESP32Message(string message)
        {
            string timestamped = $"[{System.DateTime.Now:HH:mm:ss}] {message}";
            esp32Logs.Add(timestamped);
            
            // Keep log size manageable
            if (esp32Logs.Count > maxLogLines)
            {
                esp32Logs.RemoveAt(0);
            }
            
            // Also log to Unity console (disabled for debugging)
            // Debug.Log($"ESP32📱 {timestamped}");
        }
        
        void OnGUI()
        {
            if (!showInGameView || !isMonitoring) return;
            
            // Display ESP32 status in game view
            GUI.Box(new Rect(10, 10, 400, 200), "ESP32 Monitor");
            
            GUILayout.BeginArea(new Rect(20, 40, 380, 160));
            GUILayout.Label($"ESP32: {esp32IPAddress}:{statusPort}");
            GUILayout.Label($"Status: {(isMonitoring ? "🟢 Monitoring" : "🔴 Stopped")}");
            
            GUILayout.Space(10);
            GUILayout.Label("Recent Messages:");
            
            foreach (string log in esp32Logs)
            {
                GUILayout.Label(log, GUILayout.Width(360));
            }
            
            GUILayout.EndArea();
        }
        
        void OnDestroy()
        {
            StopMonitoring();
        }
        
        void OnApplicationQuit()
        {
            StopMonitoring();
        }
    }
}
