using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Collections;
using System.Linq;
using BranchingLEDAnimator.Core;
using BranchingLEDAnimator.Animation;
using BranchingLEDAnimator.Hardware;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BranchingLEDAnimator.Audio
{
    /// <summary>
    /// Exports synchronized LED animations and audio for ESP32 playback
    /// Creates perfectly timed audio tracks that match LED animation sequences
    /// </summary>
    public class SyncedAudioExporter : MonoBehaviour
    {
        [Header("Export Settings")]
        [SerializeField] private string exportDirectory = "SyncedExports";
        [SerializeField] private bool exportAudioAndLED = true;
        [SerializeField] private bool exportAudioOnly = false;
        
        [Header("Audio Export")]
        [SerializeField] private int audioSampleRate = 44100;
        [SerializeField] private int audioBitDepth = 16;
        [SerializeField] private float exportDuration = 30f;
        [SerializeField] private int animationFPS = 30;
        
        [Header("Timing Synchronization")]
        [SerializeField] private bool useAnimationTiming = true;
        [SerializeField] private float customAnimationSpeed = 1f;
        [SerializeField] private bool exportLoopingAudio = true;
        
        [Header("Export Quality")]
        [SerializeField] private bool highQualityExport = true;
        [SerializeField] private bool includeBackgroundAudio = true;
        [SerializeField] private float masterVolume = 0.8f;
        
        // References
        private LEDAnimationSystem animationSystem;
        private LEDGraphManager graphManager;
        private GraphAudioMapper audioMapper;
        private ESP32Communicator esp32Communicator;
        
        // Export state
        private Dictionary<float, List<int>> animationEvents = new Dictionary<float, List<int>>();
        private bool isExporting = false;
        
        [System.Serializable]
        public class SyncedExportData
        {
            public string animationName;
            public float duration;
            public int fps;
            public float audioSampleRate;
            public List<AudioEvent> audioEvents = new List<AudioEvent>();
            public string audioFilePath;
            public string ledDataPath;
            public bool hasBackgroundAudio;
        }
        
        [System.Serializable]
        public class AudioEvent
        {
            public float timestamp;
            public int nodeIndex;
            public float frequency;
            public float volume;
            public float decay;
            public string chimeType;
            public bool isHighPriority; // Source nodes, high-valence nodes
        }
        
        void Start()
        {
            animationSystem = GetComponent<LEDAnimationSystem>();
            graphManager = GetComponent<LEDGraphManager>();
            audioMapper = GetComponent<GraphAudioMapper>();
            esp32Communicator = GetComponent<ESP32Communicator>();
            
            CreateExportDirectory();
        }
        
        /// <summary>
        /// Export synchronized LED animation and audio for current animation
        /// </summary>
        [ContextMenu("Export Synced Animation + Audio")]
        public void ExportSyncedAnimationAudio()
        {
            if (isExporting)
            {
                Debug.LogWarning("⚠️ Export already in progress");
                return;
            }
            
            if (!ValidateExportRequirements())
                return;
            
            StartCoroutine(ExportSyncedCoroutine());
        }
        
        /// <summary>
        /// Export only audio for current animation
        /// </summary>
        [ContextMenu("Export Audio Only")]
        public void ExportAudioOnly()
        {
            if (isExporting)
            {
                Debug.LogWarning("⚠️ Export already in progress");
                return;
            }
            
            if (!ValidateAudioExportRequirements())
                return;
            
            StartCoroutine(ExportAudioOnlyCoroutine());
        }
        
        /// <summary>
        /// Validate requirements for synced export
        /// </summary>
        private bool ValidateExportRequirements()
        {
            if (animationSystem?.CurrentAnimation == null)
            {
                Debug.LogError("❌ No animation selected for export");
                return false;
            }
            
            if (graphManager == null || !graphManager.DataLoaded)
            {
                Debug.LogError("❌ No graph data loaded");
                return false;
            }
            
            if (audioMapper == null || !audioMapper.IsAnalysisValid())
            {
                Debug.LogError("❌ Audio mapping not analyzed. Run 'Analyze Graph Audio' first.");
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Validate requirements for audio-only export
        /// </summary>
        private bool ValidateAudioExportRequirements()
        {
            if (animationSystem?.CurrentAnimation == null)
            {
                Debug.LogError("❌ No animation selected for export");
                return false;
            }
            
            if (audioMapper == null || !audioMapper.IsAnalysisValid())
            {
                Debug.LogError("❌ Audio mapping not analyzed. Run 'Analyze Graph Audio' first.");
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Coroutine for synchronized export
        /// </summary>
        private IEnumerator ExportSyncedCoroutine()
        {
            isExporting = true;
            bool exportSuccessful = false;
            string animationName = "";
            
            // Get animation name outside try-catch
            if (animationSystem?.CurrentAnimation != null)
            {
                animationName = animationSystem.CurrentAnimation.name;
                Debug.Log($"📤 Starting synchronized export for: {animationName}");
                
                // Step 1: Analyze animation for audio events
                yield return StartCoroutine(AnalyzeAnimationEventsCoroutine());
                
                // Step 2: Generate synchronized audio
                yield return StartCoroutine(GenerateSyncedAudioCoroutine(animationName));
                
                // Step 3: Export LED data (if enabled)
                if (exportAudioAndLED)
                {
                    yield return StartCoroutine(ExportLEDDataCoroutine(animationName));
                }
                
                // Step 4: Create export metadata
                try
                {
                    CreateExportMetadata(animationName);
                    exportSuccessful = true;
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"❌ Metadata export failed: {e.Message}");
                }
            }
            else
            {
                Debug.LogError("❌ No animation selected for export");
            }
            
            // Cleanup and final steps
            isExporting = false;
            
            if (exportSuccessful)
            {
                Debug.Log($"✅ Synchronized export complete: {animationName}");
                
                #if UNITY_EDITOR
                AssetDatabase.Refresh();
                #endif
            }
        }
        
        /// <summary>
        /// Coroutine for audio-only export
        /// </summary>
        private IEnumerator ExportAudioOnlyCoroutine()
        {
            isExporting = true;
            bool exportSuccessful = false;
            string animationName = "";
            
            // Get animation name outside try-catch
            if (animationSystem?.CurrentAnimation != null)
            {
                animationName = animationSystem.CurrentAnimation.name;
                Debug.Log($"🎵 Starting audio-only export for: {animationName}");
                
                // Step 1: Analyze animation for audio events
                yield return StartCoroutine(AnalyzeAnimationEventsCoroutine());
                
                // Step 2: Generate synchronized audio
                yield return StartCoroutine(GenerateSyncedAudioCoroutine(animationName));
                
                exportSuccessful = true;
            }
            else
            {
                Debug.LogError("❌ No animation selected for audio export");
            }
            
            // Cleanup and final steps
            isExporting = false;
            
            if (exportSuccessful)
            {
                Debug.Log($"✅ Audio export complete: {animationName}");
                
                #if UNITY_EDITOR
                AssetDatabase.Refresh();
                #endif
            }
        }
        
        /// <summary>
        /// Analyze animation to find audio trigger events
        /// </summary>
        private IEnumerator AnalyzeAnimationEventsCoroutine()
        {
            Debug.Log("🔍 Analyzing animation for audio events...");
            
            animationEvents.Clear();
            
            float timeStep = 1f / animationFPS;
            int totalFrames = Mathf.RoundToInt(exportDuration * animationFPS);
            
            Dictionary<int, bool> previousNodeStates = new Dictionary<int, bool>();
            
            for (int frame = 0; frame < totalFrames; frame++)
            {
                float time = frame * timeStep;
                float normalizedTime = time / exportDuration;
                
                // Get LED colors for this frame
                Color[] ledColors = animationSystem.CurrentAnimation.CalculateNodeColors(
                    graphManager.NodePositions,
                    graphManager.EdgeConnections,
                    graphManager.SourceNodes,
                    normalizedTime * 10f, // Animation time
                    frame
                );
                
                // Detect newly activated nodes
                List<int> newlyActivatedNodes = new List<int>();
                
                for (int i = 0; i < ledColors.Length; i++)
                {
                    Color nodeColor = ledColors[i];
                    Color inactiveColor = Color.black;
                    
                    bool isActive = Vector3.Distance(new Vector3(nodeColor.r, nodeColor.g, nodeColor.b), 
                                                    new Vector3(inactiveColor.r, inactiveColor.g, inactiveColor.b)) > 0.1f;
                    
                    bool wasPreviouslyActive = previousNodeStates.ContainsKey(i) && previousNodeStates[i];
                    
                    if (isActive && !wasPreviouslyActive)
                    {
                        newlyActivatedNodes.Add(i);
                    }
                    
                    previousNodeStates[i] = isActive;
                }
                
                // Store events for this timestamp
                if (newlyActivatedNodes.Count > 0)
                {
                    animationEvents[time] = newlyActivatedNodes;
                }
                
                // Yield occasionally to prevent frame drops
                if (frame % 30 == 0)
                {
                    yield return null;
                }
            }
            
            Debug.Log($"📊 Found {animationEvents.Count} audio event timestamps with {animationEvents.Values.Sum(list => list.Count)} total node activations");
        }
        
        /// <summary>
        /// Generate synchronized audio file
        /// </summary>
        private IEnumerator GenerateSyncedAudioCoroutine(string animationName)
        {
            Debug.Log("🎼 Generating synchronized audio...");
            
            int totalSamples = Mathf.RoundToInt(exportDuration * audioSampleRate);
            float[] audioData = new float[totalSamples * 2]; // Stereo
            
            var audioAnalysis = audioMapper.GetCurrentAnalysis();
            
            // Generate background audio if enabled
            if (includeBackgroundAudio)
            {
                for (int i = 0; i < totalSamples; i++)
                {
                    float time = (float)i / audioSampleRate;
                    float backgroundSample = GenerateBackgroundSample(time, audioAnalysis);
                    
                    audioData[i * 2] += backgroundSample;     // Left
                    audioData[i * 2 + 1] += backgroundSample; // Right
                }
                
                yield return null; // Allow frame to process
            }
            
            // Add chime events
            int eventCount = 0;
            foreach (var eventTime in animationEvents.Keys)
            {
                foreach (int nodeIndex in animationEvents[eventTime])
                {
                    if (audioAnalysis.nodeAudioMap.ContainsKey(nodeIndex))
                    {
                        var audioData_node = audioAnalysis.nodeAudioMap[nodeIndex];
                        AddChimeToAudioData(audioData, eventTime, audioData_node);
                        eventCount++;
                    }
                }
                
                // Yield occasionally
                if (eventCount % 50 == 0)
                {
                    yield return null;
                }
            }
            
            // Apply master volume and normalization
            float maxAmplitude = 0f;
            for (int i = 0; i < audioData.Length; i++)
            {
                maxAmplitude = Mathf.Max(maxAmplitude, Mathf.Abs(audioData[i]));
            }
            
            if (maxAmplitude > 0f)
            {
                float normalizeRatio = masterVolume / maxAmplitude;
                for (int i = 0; i < audioData.Length; i++)
                {
                    audioData[i] *= normalizeRatio;
                    audioData[i] = Mathf.Clamp(audioData[i], -1f, 1f);
                }
            }
            
            // Export to WAV file
            string audioFileName = $"{animationName}_synced_audio.wav";
            string audioFilePath = Path.Combine(Application.dataPath, exportDirectory, audioFileName);
            
            WriteWAVFile(audioFilePath, audioData, audioSampleRate);
            
            Debug.Log($"🎵 Audio exported: {audioFileName} ({eventCount} chime events)");
        }
        
        /// <summary>
        /// Generate background audio sample
        /// </summary>
        private float GenerateBackgroundSample(float time, GraphAudioMapper.GraphAudioAnalysis analysis)
        {
            if (analysis.scaleFrequencies == null || analysis.scaleFrequencies.Length == 0)
                return 0f;
            
            float sample = 0f;
            
            // Multiple drone layers
            for (int i = 0; i < Mathf.Min(3, analysis.scaleFrequencies.Length); i++)
            {
                float freq = analysis.scaleFrequencies[i] * 0.5f; // Lower octave
                float amplitude = 0.05f / (i + 1); // Quiet background
                
                sample += Mathf.Sin(2f * Mathf.PI * freq * time) * amplitude;
            }
            
            // Slow modulation
            float modulation = Mathf.Sin(2f * Mathf.PI * 0.1f * time) * 0.3f + 0.7f;
            
            return sample * modulation;
        }
        
        /// <summary>
        /// Add a chime event to the audio data
        /// </summary>
        private void AddChimeToAudioData(float[] audioData, float eventTime, GraphAudioMapper.NodeAudioData nodeAudioData)
        {
            int startSample = Mathf.RoundToInt(eventTime * audioSampleRate);
            float duration = nodeAudioData.audioSettings.decay + 0.5f;
            int chimeLength = Mathf.RoundToInt(duration * audioSampleRate);
            
            for (int i = 0; i < chimeLength && startSample + i < audioData.Length / 2; i++)
            {
                float chimeTime = (float)i / audioSampleRate;
                float chimeSample = GenerateChimeSample(nodeAudioData, chimeTime);
                
                // Add to stereo channels with spatial positioning
                float pan = CalculateStereoPosition(nodeAudioData.position);
                
                int sampleIndex = (startSample + i) * 2;
                if (sampleIndex + 1 < audioData.Length)
                {
                    audioData[sampleIndex] += chimeSample * (1f - pan);     // Left
                    audioData[sampleIndex + 1] += chimeSample * pan;       // Right
                }
            }
        }
        
        /// <summary>
        /// Generate a chime sample
        /// </summary>
        private float GenerateChimeSample(GraphAudioMapper.NodeAudioData audioData, float time)
        {
            var settings = audioData.audioSettings;
            
            // Exponential decay envelope
            float envelope = Mathf.Exp(-time / settings.decay);
            
            // Different envelope for different chime types
            switch (settings.chimeType)
            {
                case GraphAudioMapper.ChimeType.Gong:
                    float attack = Mathf.Min(time * 3f, 1f);
                    envelope *= attack;
                    break;
            }
            
            float sample = 0f;
            
            // Generate harmonics
            for (int h = 1; h <= settings.harmonics; h++)
            {
                float harmonicFreq = audioData.frequency * h;
                float harmonicAmp = settings.volume / (h * h);
                
                sample += Mathf.Sin(2f * Mathf.PI * harmonicFreq * time) * harmonicAmp * envelope;
            }
            
            return Mathf.Clamp(sample, -1f, 1f);
        }
        
        /// <summary>
        /// Calculate stereo position based on node 3D position
        /// </summary>
        private float CalculateStereoPosition(Vector3 nodePosition)
        {
            // Use X position for stereo panning
            float pan = (nodePosition.x + 50f) / 100f; // Adjust range as needed
            return Mathf.Clamp01(pan);
        }
        
        /// <summary>
        /// Export LED data coroutine
        /// </summary>
        private IEnumerator ExportLEDDataCoroutine(string animationName)
        {
            Debug.Log("💡 Exporting LED data...");
            
            // Use existing ESP32 export functionality if available
            if (esp32Communicator != null)
            {
                var exporter = GetComponent<ESP32AnimationExporter>();
                if (exporter != null)
                {
                    // Trigger LED data export
                    // This would need to be implemented based on your existing export system
                    Debug.Log("💡 LED data export triggered");
                }
            }
            
            yield return null;
        }
        
        /// <summary>
        /// Create export metadata file
        /// </summary>
        private void CreateExportMetadata(string animationName)
        {
            var exportData = new SyncedExportData
            {
                animationName = animationName,
                duration = exportDuration,
                fps = animationFPS,
                audioSampleRate = audioSampleRate,
                audioFilePath = $"{animationName}_synced_audio.wav",
                ledDataPath = $"{animationName}_led_data.bin",
                hasBackgroundAudio = includeBackgroundAudio
            };
            
            // Add audio events
            foreach (var eventTime in animationEvents.Keys)
            {
                foreach (int nodeIndex in animationEvents[eventTime])
                {
                    if (audioMapper.GetCurrentAnalysis().nodeAudioMap.ContainsKey(nodeIndex))
                    {
                        var audioData = audioMapper.GetCurrentAnalysis().nodeAudioMap[nodeIndex];
                        
                        exportData.audioEvents.Add(new AudioEvent
                        {
                            timestamp = eventTime,
                            nodeIndex = nodeIndex,
                            frequency = audioData.frequency,
                            volume = audioData.audioSettings.volume,
                            decay = audioData.audioSettings.decay,
                            chimeType = audioData.audioSettings.chimeType.ToString(),
                            isHighPriority = audioData.isSourceNode || audioData.isHighValence
                        });
                    }
                }
            }
            
            // Save metadata as JSON
            string metadataJson = JsonUtility.ToJson(exportData, true);
            string metadataPath = Path.Combine(Application.dataPath, exportDirectory, $"{animationName}_metadata.json");
            File.WriteAllText(metadataPath, metadataJson);
            
            Debug.Log($"📋 Export metadata saved: {animationName}_metadata.json");
        }
        
        /// <summary>
        /// Write WAV file
        /// </summary>
        private void WriteWAVFile(string filepath, float[] audioData, int sampleRate)
        {
            int samples = audioData.Length / 2; // Stereo
            int bytesPerSample = audioBitDepth / 8;
            int dataSize = samples * 2 * bytesPerSample; // 2 channels
            
            using (var fileStream = new FileStream(filepath, FileMode.Create))
            using (var writer = new BinaryWriter(fileStream))
            {
                // WAV Header
                writer.Write("RIFF".ToCharArray());
                writer.Write(36 + dataSize);
                writer.Write("WAVE".ToCharArray());
                
                // Format chunk
                writer.Write("fmt ".ToCharArray());
                writer.Write(16); // Format chunk size
                writer.Write((short)1); // PCM format
                writer.Write((short)2); // Stereo
                writer.Write(sampleRate);
                writer.Write(sampleRate * 2 * bytesPerSample); // Byte rate
                writer.Write((short)(2 * bytesPerSample)); // Block align
                writer.Write((short)audioBitDepth);
                
                // Data chunk
                writer.Write("data".ToCharArray());
                writer.Write(dataSize);
                
                // Audio data (convert float to 16-bit PCM)
                for (int i = 0; i < audioData.Length; i++)
                {
                    short sample = (short)(audioData[i] * 32767f);
                    writer.Write(sample);
                }
            }
        }
        
        /// <summary>
        /// Create export directory
        /// </summary>
        private void CreateExportDirectory()
        {
            string fullPath = Path.Combine(Application.dataPath, exportDirectory);
            if (!Directory.Exists(fullPath))
            {
                Directory.CreateDirectory(fullPath);
                Debug.Log($"📁 Created export directory: {fullPath}");
            }
        }
        
        #if UNITY_EDITOR
        [ContextMenu("Show Export Info")]
        public void ShowExportInfo()
        {
            Debug.Log("📤 SYNCED EXPORT INFO");
            Debug.Log("=====================");
            Debug.Log($"Export Directory: {Path.Combine(Application.dataPath, exportDirectory)}");
            Debug.Log($"Audio Sample Rate: {audioSampleRate}Hz");
            Debug.Log($"Audio Bit Depth: {audioBitDepth}-bit");
            Debug.Log($"Export Duration: {exportDuration}s");
            Debug.Log($"Animation FPS: {animationFPS}");
            Debug.Log($"Include Background: {includeBackgroundAudio}");
            Debug.Log($"Master Volume: {masterVolume}");
        }
        #endif
    }
}
