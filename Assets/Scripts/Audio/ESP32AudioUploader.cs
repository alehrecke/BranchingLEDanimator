using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using BranchingLEDAnimator.Hardware;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BranchingLEDAnimator.Audio
{
    /// <summary>
    /// Uploads audio files to ESP32 SD card via UDP for synchronized LED+audio playback
    /// Integrates with existing ESP32Communicator for seamless hardware communication
    /// </summary>
    public class ESP32AudioUploader : MonoBehaviour
    {
        [Header("ESP32 Connection")]
        [SerializeField] private string esp32IPAddress = "192.168.0.66";
        [SerializeField] private int esp32Port = 8888;
        [SerializeField] private int uploadTimeoutSeconds = 30;
        
        [Header("Upload Settings")]
        [SerializeField] private int chunkSize = 512; // Bytes per UDP packet
        [SerializeField] private bool showUploadProgress = true;
        [SerializeField] private float retryDelaySeconds = 0.1f;
        
        [Header("Audio Files")]
        [SerializeField] private List<string> audioFilesToUpload = new List<string>();
        
        // References
        private ESP32Communicator esp32Communicator;
        private UdpClient udpClient;
        
        // Upload state
        private bool isUploading = false;
        private string currentUploadFile = "";
        private int totalChunks = 0;
        private int uploadedChunks = 0;
        
        // Protocol constants (matching ESP32 firmware)
        private const byte PACKET_START_MARKER = 0xAA;
        private const byte COMMAND_AUDIO_UPLOAD = 0x20;
        private const byte COMMAND_AUDIO_PLAY = 0x21;
        private const byte COMMAND_AUDIO_STOP = 0x22;
        private const byte COMMAND_AUDIO_LIST = 0x23;
        
        void Start()
        {
            // Get reference to existing ESP32 communicator
            esp32Communicator = GetComponent<ESP32Communicator>();
            if (esp32Communicator != null)
            {
                // Sync IP address with existing communicator
                esp32IPAddress = esp32Communicator.esp32IPAddress;
                Debug.Log($"🔗 Synced with ESP32Communicator: {esp32IPAddress}:{esp32Port}");
            }
            
            Debug.Log("📤 ESP32AudioUploader ready for audio file uploads");
        }
        
        /// <summary>
        /// Upload a single audio file to ESP32 SD card
        /// </summary>
        public void UploadAudioFile(string filePath)
        {
            if (isUploading)
            {
                Debug.LogWarning("⚠️ Upload already in progress");
                return;
            }
            
            if (!File.Exists(filePath))
            {
                Debug.LogError($"❌ Audio file not found: {filePath}");
                return;
            }
            
            StartCoroutine(UploadFileCoroutine(filePath));
        }
        
        /// <summary>
        /// Upload all audio files from the AudioExports directory
        /// </summary>
        public void UploadAllAudioFiles()
        {
            string audioDirectory = Path.Combine(Application.dataPath, "AudioExports");
            
            if (!Directory.Exists(audioDirectory))
            {
                Debug.LogWarning($"❌ Audio export directory not found: {audioDirectory}");
                return;
            }
            
            string[] wavFiles = Directory.GetFiles(audioDirectory, "*.wav");
            
            if (wavFiles.Length == 0)
            {
                Debug.LogWarning("❌ No WAV files found in AudioExports directory");
                return;
            }
            
            Debug.Log($"📤 Found {wavFiles.Length} audio files to upload");
            StartCoroutine(UploadMultipleFilesCoroutine(wavFiles));
        }
        
        /// <summary>
        /// Upload files from the configured list
        /// </summary>
        public void UploadConfiguredFiles()
        {
            if (audioFilesToUpload.Count == 0)
            {
                Debug.LogWarning("❌ No audio files configured for upload");
                return;
            }
            
            List<string> validFiles = new List<string>();
            foreach (string relativePath in audioFilesToUpload)
            {
                string fullPath = Path.Combine(Application.dataPath, relativePath);
                if (File.Exists(fullPath))
                {
                    validFiles.Add(fullPath);
                }
                else
                {
                    Debug.LogWarning($"⚠️ Configured file not found: {fullPath}");
                }
            }
            
            if (validFiles.Count > 0)
            {
                StartCoroutine(UploadMultipleFilesCoroutine(validFiles.ToArray()));
            }
        }
        
        /// <summary>
        /// Coroutine to upload a single file
        /// </summary>
        private IEnumerator UploadFileCoroutine(string filePath)
        {
            isUploading = true;
            currentUploadFile = Path.GetFileName(filePath);
            
            Debug.Log($"📤 Starting upload: {currentUploadFile}");
            
            byte[] fileData = null;
            bool uploadSuccessful = false;
            
            // Read file data (outside try-catch to avoid yield issues)
            try
            {
                fileData = File.ReadAllBytes(filePath);
                Debug.Log($"📊 File size: {fileData.Length} bytes ({fileData.Length / 1024}KB)");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"❌ Failed to read file: {e.Message}");
                isUploading = false;
                currentUploadFile = "";
                yield break;
            }
            
            // Calculate chunks
            totalChunks = Mathf.CeilToInt((float)fileData.Length / chunkSize);
            uploadedChunks = 0;
            
            Debug.Log($"📦 Uploading {totalChunks} chunks of {chunkSize} bytes each");
            
            // Initialize UDP client
            try
            {
                udpClient = new UdpClient();
                udpClient.Connect(esp32IPAddress, esp32Port);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"❌ Failed to initialize UDP client: {e.Message}");
                isUploading = false;
                currentUploadFile = "";
                yield break;
            }
            
            // Upload file in chunks
            for (int chunkIndex = 0; chunkIndex < totalChunks; chunkIndex++)
            {
                // Calculate chunk data
                int startOffset = chunkIndex * chunkSize;
                int currentChunkSize = Mathf.Min(chunkSize, fileData.Length - startOffset);
                
                // Create chunk packet
                byte[] chunkData = new byte[currentChunkSize];
                System.Array.Copy(fileData, startOffset, chunkData, 0, currentChunkSize);
                
                // Create UDP packet
                byte[] packet = CreateAudioUploadPacket(currentUploadFile, chunkIndex, totalChunks, chunkData);
                
                // Send packet
                yield return StartCoroutine(SendPacketWithRetry(packet, chunkIndex));
                
                uploadedChunks++;
                
                if (showUploadProgress && chunkIndex % 10 == 0) // Show progress every 10 chunks
                {
                    float progress = (float)uploadedChunks / totalChunks * 100f;
                    Debug.Log($"📈 Upload progress: {progress:F1}% ({uploadedChunks}/{totalChunks} chunks)");
                }
                
                // Small delay between chunks
                yield return new WaitForSeconds(retryDelaySeconds);
            }
            
            uploadSuccessful = true;
            Debug.Log($"✅ Upload complete: {currentUploadFile} ({uploadedChunks}/{totalChunks} chunks)");
            
            // Cleanup
            if (udpClient != null)
            {
                udpClient.Close();
                udpClient = null;
            }
            
            isUploading = false;
            currentUploadFile = "";
            
            if (!uploadSuccessful)
            {
                Debug.LogError($"❌ Upload failed for: {currentUploadFile}");
            }
        }
        
        /// <summary>
        /// Coroutine to upload multiple files sequentially
        /// </summary>
        private IEnumerator UploadMultipleFilesCoroutine(string[] filePaths)
        {
            Debug.Log($"📤 Starting batch upload of {filePaths.Length} files");
            
            for (int i = 0; i < filePaths.Length; i++)
            {
                Debug.Log($"📂 Uploading file {i + 1}/{filePaths.Length}: {Path.GetFileName(filePaths[i])}");
                
                yield return StartCoroutine(UploadFileCoroutine(filePaths[i]));
                
                // Wait between files
                if (i < filePaths.Length - 1)
                {
                    yield return new WaitForSeconds(1f);
                }
            }
            
            Debug.Log("🎉 Batch upload complete!");
        }
        
        /// <summary>
        /// Send a UDP packet with retry logic
        /// </summary>
        private IEnumerator SendPacketWithRetry(byte[] packet, int chunkIndex)
        {
            int maxRetries = 3;
            int retryCount = 0;
            bool sendSuccessful = false;
            
            while (retryCount < maxRetries && !sendSuccessful)
            {
                try
                {
                    udpClient.Send(packet, packet.Length);
                    sendSuccessful = true;
                }
                catch (System.Exception e)
                {
                    retryCount++;
                    Debug.LogWarning($"⚠️ Chunk {chunkIndex} send failed (attempt {retryCount}/{maxRetries}): {e.Message}");
                    
                    if (retryCount >= maxRetries)
                    {
                        Debug.LogError($"❌ Failed to send chunk {chunkIndex} after {maxRetries} attempts");
                        yield break; // Exit instead of throwing
                    }
                }
                
                if (sendSuccessful)
                {
                    // Wait for potential acknowledgment (outside try-catch)
                    yield return new WaitForSeconds(0.01f);
                }
                else if (retryCount < maxRetries)
                {
                    // Wait before retry (outside try-catch)
                    yield return new WaitForSeconds(retryDelaySeconds);
                }
            }
        }
        
        /// <summary>
        /// Create audio upload packet in ESP32-compatible format
        /// </summary>
        private byte[] CreateAudioUploadPacket(string filename, int chunkIndex, int totalChunks, byte[] chunkData)
        {
            // Packet format: [START][COMMAND][CHUNK_INDEX_LOW][CHUNK_INDEX_HIGH][TOTAL_CHUNKS][FILENAME(32)][CHUNK_SIZE][DATA...]
            
            List<byte> packet = new List<byte>();
            
            // Header
            packet.Add(PACKET_START_MARKER);
            packet.Add(COMMAND_AUDIO_UPLOAD);
            packet.Add((byte)(chunkIndex & 0xFF));        // Chunk index low byte
            packet.Add((byte)((chunkIndex >> 8) & 0xFF)); // Chunk index high byte
            
            // Total chunks (4 bytes)
            packet.AddRange(System.BitConverter.GetBytes(totalChunks));
            
            // Filename (32 bytes, padded with zeros)
            byte[] filenameBytes = new byte[32];
            byte[] nameBytes = System.Text.Encoding.UTF8.GetBytes(filename);
            System.Array.Copy(nameBytes, 0, filenameBytes, 0, Mathf.Min(nameBytes.Length, 32));
            packet.AddRange(filenameBytes);
            
            // Chunk size (4 bytes)
            packet.AddRange(System.BitConverter.GetBytes(chunkData.Length));
            
            // Chunk data
            packet.AddRange(chunkData);
            
            return packet.ToArray();
        }
        
        /// <summary>
        /// Send command to play audio file on ESP32
        /// </summary>
        public void PlayAudioOnESP32(string filename)
        {
            if (string.IsNullOrEmpty(filename))
            {
                Debug.LogWarning("❌ No filename provided for audio playback");
                return;
            }
            
            StartCoroutine(SendAudioCommandCoroutine(COMMAND_AUDIO_PLAY, filename));
        }
        
        /// <summary>
        /// Send command to stop audio playback on ESP32
        /// </summary>
        public void StopAudioOnESP32()
        {
            StartCoroutine(SendAudioCommandCoroutine(COMMAND_AUDIO_STOP, ""));
        }
        
        /// <summary>
        /// Request list of audio files on ESP32
        /// </summary>
        public void RequestAudioListFromESP32()
        {
            StartCoroutine(SendAudioCommandCoroutine(COMMAND_AUDIO_LIST, ""));
        }
        
        /// <summary>
        /// Send simple audio command to ESP32
        /// </summary>
        private IEnumerator SendAudioCommandCoroutine(byte command, string filename)
        {
            try
            {
                using (var udpClient = new UdpClient())
                {
                    udpClient.Connect(esp32IPAddress, esp32Port);
                    
                    // Create command packet
                    List<byte> packet = new List<byte>();
                    packet.Add(PACKET_START_MARKER);
                    packet.Add(command);
                    packet.Add(0); // Unused
                    packet.Add(0); // Unused
                    
                    // Add filename if provided
                    if (!string.IsNullOrEmpty(filename))
                    {
                        byte[] filenameBytes = new byte[32];
                        byte[] nameBytes = System.Text.Encoding.UTF8.GetBytes(filename);
                        System.Array.Copy(nameBytes, 0, filenameBytes, 0, Mathf.Min(nameBytes.Length, 32));
                        packet.AddRange(filenameBytes);
                    }
                    
                    // Send packet
                    byte[] packetData = packet.ToArray();
                    udpClient.Send(packetData, packetData.Length);
                    
                    string commandName = command switch
                    {
                        COMMAND_AUDIO_PLAY => "Play",
                        COMMAND_AUDIO_STOP => "Stop",
                        COMMAND_AUDIO_LIST => "List",
                        _ => "Unknown"
                    };
                    
                    Debug.Log($"📡 Sent audio command: {commandName} {filename}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"❌ Failed to send audio command: {e.Message}");
            }
            
            yield return null;
        }
        
        /// <summary>
        /// Get upload progress (0-1)
        /// </summary>
        public float GetUploadProgress()
        {
            if (!isUploading || totalChunks == 0)
                return 0f;
                
            return (float)uploadedChunks / totalChunks;
        }
        
        /// <summary>
        /// Check if upload is in progress
        /// </summary>
        public bool IsUploading()
        {
            return isUploading;
        }
        
        /// <summary>
        /// Get current upload status
        /// </summary>
        public string GetUploadStatus()
        {
            if (!isUploading)
                return "Ready";
                
            return $"Uploading {currentUploadFile}: {uploadedChunks}/{totalChunks} chunks ({GetUploadProgress() * 100:F1}%)";
        }
        
        #if UNITY_EDITOR
        /// <summary>
        /// Editor-only: Upload all generated audio files
        /// </summary>
        [ContextMenu("Upload All Audio Files")]
        public void EditorUploadAllFiles()
        {
            UploadAllAudioFiles();
        }
        
        /// <summary>
        /// Editor-only: Test audio playback
        /// </summary>
        [ContextMenu("Test Audio Playback")]
        public void EditorTestPlayback()
        {
            // Play first available audio file
            string audioDirectory = Path.Combine(Application.dataPath, "AudioExports");
            if (Directory.Exists(audioDirectory))
            {
                string[] wavFiles = Directory.GetFiles(audioDirectory, "*.wav");
                if (wavFiles.Length > 0)
                {
                    string filename = Path.GetFileName(wavFiles[0]);
                    PlayAudioOnESP32(filename);
                }
                else
                {
                    Debug.LogWarning("❌ No audio files found for testing");
                }
            }
        }
        #endif
    }
}
