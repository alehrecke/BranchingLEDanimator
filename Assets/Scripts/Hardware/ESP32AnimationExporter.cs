using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Net;
using System.Linq;
using BranchingLEDAnimator.Core;
using BranchingLEDAnimator.Animation;

namespace BranchingLEDAnimator.Hardware
{
    /// <summary>
    /// Exports Unity animations as frame data for standalone ESP32 playback
    /// Integrates seamlessly with existing ESP32Communicator workflow
    /// </summary>
    public class ESP32AnimationExporter : MonoBehaviour
    {
        [Header("Export Settings")]
        [SerializeField] private int exportFrameRate = 30;
        [SerializeField] private float maxAnimationDuration = 30f;
        [SerializeField] private bool compressColors = false;
        [SerializeField] private bool showExportProgress = true;
        
        [Header("Export Quality")]
        [Range(1, 8)]
        [SerializeField] private int colorBitsPerChannel = 8; // 8 = full quality, 6 = 25% savings, 5 = 37% savings
        [Range(10, 60)]
        [SerializeField] private int targetFrameRate = 30;
        
        [Header("Batch Export")]
        [SerializeField] private List<LEDAnimationType> animationsToExport = new List<LEDAnimationType>();
        [SerializeField] private bool exportCurrentPlaylist = true;
        
        // References
        private LEDAnimationSystem animationSystem;
        private LEDGraphManager graphManager;
        private LEDCircuitMapper circuitMapper;
        private ESP32Communicator esp32Communicator;
        
        // Export state
        private bool isExporting = false;
        private float exportProgress = 0f;
        private string currentExportStatus = "";
        
        void Start()
        {
            // Get component references
            animationSystem = GetComponent<LEDAnimationSystem>();
            graphManager = GetComponent<LEDGraphManager>();
            circuitMapper = GetComponent<LEDCircuitMapper>();
            esp32Communicator = GetComponent<ESP32Communicator>();
            
            if (animationSystem == null) Debug.LogError("ESP32AnimationExporter: LEDAnimationSystem not found!");
            if (graphManager == null) Debug.LogError("ESP32AnimationExporter: LEDGraphManager not found!");
            if (circuitMapper == null) Debug.LogError("ESP32AnimationExporter: LEDCircuitMapper not found!");
        }
        
        /// <summary>
        /// Export currently selected animation
        /// </summary>
        [ContextMenu("Export Current Animation")]
        public void ExportCurrentAnimation()
        {
            // Enhanced debugging
            if (animationSystem == null)
            {
                Debug.LogError("ESP32AnimationExporter: LEDAnimationSystem component not found! Please ensure this component is on the same GameObject.");
                return;
            }
            
            if (animationSystem.availableAnimations == null || animationSystem.availableAnimations.Count == 0)
            {
                Debug.LogError("ESP32AnimationExporter: No animations available in LEDAnimationSystem. Please add some animation assets to the Available Animations list.");
                return;
            }
            
            if (animationSystem.CurrentAnimation == null)
            {
                Debug.LogError("ESP32AnimationExporter: No animation currently selected for export. Current animation index: " + animationSystem.currentAnimationIndex + 
                             ", Playback count: " + animationSystem.AnimationCount + 
                             ". Please set a valid currentAnimationIndex in LEDAnimationSystem (0-" + (animationSystem.AnimationCount - 1) + ").");
                return;
            }
            
            Debug.Log("🎬 Exporting animation: " + animationSystem.CurrentAnimation.animationName);
            
            // Temporarily stop live LED streaming during export to prevent interference
            bool wasPlaying = animationSystem.isPlaying;
            if (wasPlaying)
            {
                Debug.Log("⏸️ Temporarily pausing live animation for export");
                animationSystem.isPlaying = false;
            }
            
            StartCoroutine(ExportSingleAnimationWithRestore(animationSystem.CurrentAnimation, wasPlaying));
        }
        
        /// <summary>
        /// Export all animations in the current playlist
        /// </summary>
        [ContextMenu("Export Animation Playlist")]
        public void ExportAnimationPlaylist()
        {
            if (exportCurrentPlaylist && animationSystem != null)
            {
                animationsToExport.Clear();
                foreach (var a in animationSystem.GetPlaybackAnimations())
                {
                    if (a != null)
                        animationsToExport.Add(a);
                }
            }
            
            if (animationsToExport.Count == 0)
            {
                Debug.LogError("No animations selected for export");
                return;
            }
            
            StartCoroutine(ExportAnimationBatch());
        }
        
        /// <summary>
        /// Export single animation with state restoration
        /// </summary>
        private System.Collections.IEnumerator ExportSingleAnimationWithRestore(LEDAnimationType animation, bool restorePlayState)
        {
            yield return StartCoroutine(ExportSingleAnimation(animation));
            
            // Restore animation state after export
            if (restorePlayState && animationSystem != null)
            {
                Debug.Log("▶️ Restoring live animation playback");
                animationSystem.isPlaying = true;
            }
        }
        
        /// <summary>
        /// Export single animation to frame data
        /// </summary>
        private System.Collections.IEnumerator ExportSingleAnimation(LEDAnimationType animation)
        {
            if (isExporting)
            {
                Debug.LogWarning("Export already in progress");
                yield break;
            }
            
            isExporting = true;
            exportProgress = 0f;
            currentExportStatus = $"Exporting '{animation.animationName}'...";
            
            Debug.Log($"🎬 Starting export of animation: {animation.animationName}");
            
            // Ensure graph data is loaded
            if (!graphManager.DataLoaded)
            {
                Debug.LogError("Graph data not loaded. Cannot export animation.");
                isExporting = false;
                yield break;
            }
            
            // Ensure circuit mapping is complete
            if (!circuitMapper.MappingComplete)
            {
                Debug.LogError("❌ Circuit mapping not complete! Follow these steps:");
                Debug.LogError("   1. Import polylines using '📐 Import Polylines from Geometry'");
                Debug.LogError("   2. Configure LED counts and directions for each strip");
                Debug.LogError("   3. Click '⚡ Compile Final LED Order' to complete the mapping");
                Debug.LogError("   4. Then try exporting animations");
                isExporting = false;
                yield break;
            }
            
            // Calculate export parameters
            float animationDuration = Mathf.Min(animation.duration, maxAnimationDuration);
            int totalFrames = Mathf.RoundToInt(animationDuration * targetFrameRate);
            float timeStep = animationDuration / totalFrames;
            
            Debug.Log($"📊 Export parameters: {totalFrames} frames, {animationDuration}s duration, {targetFrameRate} FPS");
            Debug.Log($"🔍 DEBUG: Export starting with detailed LED debugging enabled");
            Debug.Log($"🔍 DEBUG: Total LED count from circuit mapper: {circuitMapper.TotalLEDCount}");
            
            // Create animation data structure
            var animationData = new ExportedAnimation
            {
                name = animation.animationName,
                duration = animationDuration,
                frameRate = targetFrameRate,
                ledCount = circuitMapper.TotalLEDCount,
                frames = new List<ExportedFrame>()
            };
            
            // Capture frames
            for (int frame = 0; frame < totalFrames; frame++)
            {
                float animationTime = frame * timeStep;
                exportProgress = (float)frame / totalFrames;
                
                // Calculate colors for this frame
                Color[] nodeColors = animation.CalculateNodeColors(
                    graphManager.NodePositions,
                    graphManager.EdgeConnections,
                    graphManager.SourceNodes,
                    animationTime,
                    frame
                );
                
                // DEBUG: Quick node color analysis
                if (frame == 0 || frame % 30 == 0) // Log every 30 frames
                {
                    int blackNodes = 0;
                    int activeNodes = 0;
                    for (int i = 0; i < nodeColors.Length; i++)
                    {
                        if (nodeColors[i].r < 0.01f && nodeColors[i].g < 0.01f && nodeColors[i].b < 0.01f)
                            blackNodes++;
                        else
                            activeNodes++;
                    }
                    
                    Debug.Log($"📊 Export Frame {frame}: {activeNodes} active nodes, {blackNodes} black nodes (time: {animationTime:F2}s)");
                }
                
                // Convert to LED data using circuit mapper
                var stripDataList = circuitMapper.ConvertColorsToLEDData(nodeColors);
                
                // Create frame data
                var frameData = new ExportedFrame
                {
                    timestamp = animationTime,
                    ledColors = ConvertStripDataToLEDArray(stripDataList)
                };
                
                // Apply color compression if enabled
                if (compressColors)
                {
                    ApplyColorCompression(frameData.ledColors);
                }
                
                animationData.frames.Add(frameData);
                
                // Yield periodically to keep Unity responsive
                if (frame % 10 == 0)
                {
                    yield return null;
                }
            }
            
            // Generate export files
            yield return StartCoroutine(GenerateExportFiles(animationData));
            
            isExporting = false;
            exportProgress = 1f;
            currentExportStatus = "Export complete! (Check ESP32 Serial for upload confirmation)";
            
            Debug.Log($"✅ Export complete: {animation.animationName} ({animationData.frames.Count} frames)");
        }
        
        /// <summary>
        /// Export multiple animations as a batch
        /// </summary>
        private System.Collections.IEnumerator ExportAnimationBatch()
        {
            Debug.Log($"🎬 Starting batch export of {animationsToExport.Count} animations");
            
            var playlistData = new ExportedPlaylist
            {
                name = "Unity Animation Playlist",
                animations = new List<ExportedAnimation>()
            };
            
            for (int i = 0; i < animationsToExport.Count; i++)
            {
                var animation = animationsToExport[i];
                currentExportStatus = $"Exporting {i + 1}/{animationsToExport.Count}: {animation.animationName}";
                
                // Export individual animation
                yield return StartCoroutine(ExportSingleAnimationData(animation));
                
                // Add to playlist (assuming successful export)
                // This would be connected to the actual export result
            }
            
            Debug.Log("✅ Batch export complete!");
        }
        
        /// <summary>
        /// Export animation data without file generation (for batch processing)
        /// </summary>
        private System.Collections.IEnumerator ExportSingleAnimationData(LEDAnimationType animation)
        {
            // Similar to ExportSingleAnimation but returns data instead of files
            // Implementation would be extracted from ExportSingleAnimation
            yield return null; // Placeholder
        }
        
        /// <summary>
        /// Convert strip data to continuous LED array matching physical layout
        /// Uses the same logic as ESP32Communicator for consistency
        /// </summary>
        private Color[] ConvertStripDataToLEDArray(List<LEDStripData> stripDataList)
        {
            // NEW: Combine all logical strips into one continuous physical strip
            // This matches the ESP32Communicator.SendCustomProtocolData logic
            var continuousColors = new List<Color>();
            var sortedStrips = stripDataList.OrderBy(s => s.stripIndex).ToList();
            
            foreach (var stripData in sortedStrips)
            {
                continuousColors.AddRange(stripData.ledColors);
            }
            
            // DEBUG: Log problematic LEDs during export
            Debug.Log($"🔍 Export: Converting {stripDataList.Count} strips to {continuousColors.Count} continuous LEDs");
            for (int i = 0; i < Mathf.Min(50, continuousColors.Count); i++)
            {
                var color = continuousColors[i];
                bool isBlack = color.r < 0.01f && color.g < 0.01f && color.b < 0.01f;
                
                // Check if this is one of the reported problematic LEDs
                bool isProblematicLED = (i >= 1 && i <= 2) ||     // P1 range
                                       (i >= 17 && i <= 36) ||    // P2-P7 range (expanded)
                                       (i >= 306);                // P9 last 4
                
                if (isProblematicLED)
                {
                    string status = isBlack ? "❌ BLACK" : "✅ COLOR";
                    Debug.Log($"🔍 Export LED {i}: {status} (r:{color.r:F3}, g:{color.g:F3}, b:{color.b:F3})");
                }
            }
            
            return continuousColors.ToArray();
        }
        
        /// <summary>
        /// Apply color compression to reduce storage requirements
        /// </summary>
        private void ApplyColorCompression(Color[] colors)
        {
            if (colorBitsPerChannel >= 8) return; // No compression needed
            
            float quantizationStep = 255f / ((1 << colorBitsPerChannel) - 1);
            
            for (int i = 0; i < colors.Length; i++)
            {
                // Quantize each color channel
                int r = Mathf.RoundToInt(colors[i].r * 255 / quantizationStep) * Mathf.RoundToInt(quantizationStep);
                int g = Mathf.RoundToInt(colors[i].g * 255 / quantizationStep) * Mathf.RoundToInt(quantizationStep);
                int b = Mathf.RoundToInt(colors[i].b * 255 / quantizationStep) * Mathf.RoundToInt(quantizationStep);
                
                colors[i] = new Color(r / 255f, g / 255f, b / 255f, 1f);
            }
        }
        
        /// <summary>
        /// Generate export files and optionally upload to ESP32
        /// </summary>
        private System.Collections.IEnumerator GenerateExportFiles(ExportedAnimation animationData)
        {
            currentExportStatus = "Generating binary data...";
            
            // Generate binary animation file
            byte[] binaryData = SerializeAnimationToBinary(animationData);
            
            // Calculate storage requirements
            float storageKB = binaryData.Length / 1024f;
            Debug.Log($"📦 Animation '{animationData.name}': {storageKB:F1} KB ({binaryData.Length} bytes)");
            
            // Save to local file for debugging/backup
            string localPath = Path.Combine(Application.persistentDataPath, $"{animationData.name}.anim");
            File.WriteAllBytes(localPath, binaryData);
            Debug.Log($"💾 Saved animation to: {localPath}");
            
            // Upload to ESP32 if connected
            if (esp32Communicator != null && esp32Communicator.IsConnected)
            {
                currentExportStatus = "Uploading to ESP32...";
                yield return StartCoroutine(UploadAnimationToESP32(animationData.name, binaryData));
            }
            else
            {
                Debug.LogWarning("ESP32 not connected. Animation saved locally only.");
            }
            
            yield return null;
        }
        
        /// <summary>
        /// Serialize animation data to binary format for ESP32
        /// </summary>
        private byte[] SerializeAnimationToBinary(ExportedAnimation animationData)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                // Header
                writer.Write("ANIM".ToCharArray()); // Magic number
                writer.Write((byte)1); // Version
                writer.Write(animationData.name.PadRight(32).Substring(0, 32).ToCharArray()); // Name (32 chars)
                writer.Write(animationData.duration); // Duration (float)
                writer.Write(animationData.frameRate); // Frame rate (int)
                writer.Write(animationData.ledCount); // LED count (int)
                writer.Write(animationData.frames.Count); // Frame count (int)
                
                // Frame data
                foreach (var frame in animationData.frames)
                {
                    writer.Write(frame.timestamp); // Timestamp (float)
                    
                    // LED color data
                    foreach (var color in frame.ledColors)
                    {
                        writer.Write((byte)(color.r * 255));
                        writer.Write((byte)(color.g * 255));
                        writer.Write((byte)(color.b * 255));
                    }
                }
                
                return stream.ToArray();
            }
        }
        
        /// <summary>
        /// Upload animation binary data to ESP32 via UDP
        /// </summary>
        private System.Collections.IEnumerator UploadAnimationToESP32(string animationName, byte[] binaryData)
        {
            Debug.Log($"📡 Uploading '{animationName}' to ESP32 ({binaryData.Length} bytes)...");
            
            if (esp32Communicator != null && esp32Communicator.IsConnected)
            {
                // Use the ESP32Communicator's animation upload method
                esp32Communicator.SendAnimationData(animationName, binaryData);
                
                // Show realistic upload feedback
                currentExportStatus = "Uploading to ESP32... (Check Console & ESP32 Serial for real progress)";
                exportProgress = 0.8f; // Set progress to indicate upload in progress
                
                // Note: Real upload progress is now tracked in Unity Console via ACK system
                // and ESP32 Serial Monitor shows actual file writing progress
                Debug.Log("📊 Upload Progress: Monitor Unity Console for chunk-by-chunk ACK confirmations");
                Debug.Log("🔍 ESP32 Progress: Monitor ESP32 Serial for actual flash writing and completion");
                Debug.Log("⚠️ Note: Upload will take longer now due to improved reliability measures");
                
                // Wait for the upload to complete (ESP32Communicator handles the actual upload)
                yield return new WaitForSecondsRealtime(binaryData.Length / 30000f); // More realistic timing
                
                Debug.Log($"✅ Upload initiated: {animationName} - Check ESP32 Serial for final confirmation");
            }
            else
            {
                Debug.LogWarning("ESP32 not connected - animation saved locally only");
            }
            
            yield return null;
        }
        
        /// <summary>
        /// Calculate estimated storage requirements
        /// </summary>
        [ContextMenu("Calculate Storage Requirements")]
        public void CalculateStorageRequirements()
        {
            if (animationSystem == null) 
            {
                Debug.LogError("ESP32AnimationExporter: LEDAnimationSystem component not found! Please ensure this component is on the same GameObject.");
                return;
            }
            
            if (animationSystem.availableAnimations == null || animationSystem.availableAnimations.Count == 0)
            {
                Debug.LogWarning("ESP32AnimationExporter: No animations available for storage calculation. Please add some animation assets to the Available Animations list in LEDAnimationSystem.");
                return;
            }
            
            float totalStorage = 0f;
            int totalAnimations = exportCurrentPlaylist ? animationSystem.AnimationCount : animationsToExport.Count;
            
            var animations = exportCurrentPlaylist ? animationSystem.GetPlaybackAnimations() : animationsToExport;
            
            Debug.Log("📊 ANIMATION STORAGE REQUIREMENTS:");
            Debug.Log("=================================");
            
            foreach (var animation in animations)
            {
                if (animation == null) continue;
                
                float duration = Mathf.Min(animation.duration, maxAnimationDuration);
                int frames = Mathf.RoundToInt(duration * targetFrameRate);
                int bytesPerFrame = circuitMapper != null ? circuitMapper.TotalLEDCount * 3 : 99 * 3; // RGB
                
                // Apply compression factor
                float compressionFactor = colorBitsPerChannel / 8f;
                float frameSize = bytesPerFrame * compressionFactor;
                float animationSize = frames * frameSize;
                
                totalStorage += animationSize;
                
                Debug.Log($"  - {animation.animationName}: {animationSize / 1024f:F1} KB ({frames} frames)");
            }
            
            Debug.Log("=================================");
            Debug.Log($"Total Storage Required: {totalStorage / 1024f:F1} KB");
            Debug.Log($"ESP32 Available Storage: ~2800 KB");
            Debug.Log($"Storage Utilization: {(totalStorage / 1024f) / 2800f * 100f:F1}%");
            
            if (totalStorage / 1024f > 2800f)
            {
                Debug.LogWarning("⚠️ Total storage exceeds ESP32 capacity!");
                Debug.Log("💡 Suggestions: Reduce frame rate, enable compression, or shorten durations");
            }
        }
        
        // Data structures for export
        [System.Serializable]
        public class ExportedAnimation
        {
            public string name;
            public float duration;
            public int frameRate;
            public int ledCount;
            public List<ExportedFrame> frames;
        }
        
        [System.Serializable]
        public class ExportedFrame
        {
            public float timestamp;
            public Color[] ledColors;
        }
        
        [System.Serializable]
        public class ExportedPlaylist
        {
            public string name;
            public List<ExportedAnimation> animations;
        }
        
        // GUI for export progress
        void OnGUI()
        {
            if (!isExporting || !showExportProgress) return;
            
            // Simple progress display
            var rect = new Rect(Screen.width - 300, 10, 280, 60);
            GUI.Box(rect, "");
            
            var progressRect = new Rect(rect.x + 10, rect.y + 10, rect.width - 20, 20);
            GUI.Box(progressRect, "");
            GUI.Box(new Rect(progressRect.x, progressRect.y, progressRect.width * exportProgress, progressRect.height), "");
            
            var statusRect = new Rect(rect.x + 10, rect.y + 35, rect.width - 20, 20);
            GUI.Label(statusRect, currentExportStatus);
        }
    }
}
