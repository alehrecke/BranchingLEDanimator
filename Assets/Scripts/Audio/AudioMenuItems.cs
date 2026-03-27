using UnityEngine;
using BranchingLEDAnimator.Audio;
using BranchingLEDAnimator.Core;
using BranchingLEDAnimator.Animation;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BranchingLEDAnimator.Audio
{
    /// <summary>
    /// Unity Editor menu items for audio generation and management
    /// Provides easy access to audio features from the Unity menu bar
    /// </summary>
    public static class AudioMenuItems
    {
        #if UNITY_EDITOR
        
        [MenuItem("Tools/LED Animation System/Audio/Generate Audio for Current Animation")]
        public static void GenerateAudioForCurrentAnimation()
        {
            var audioGenerator = FindAudioGenerator();
            if (audioGenerator != null)
            {
                audioGenerator.GenerateAudioForCurrentAnimation();
            }
            else
            {
                Debug.LogWarning("❌ LEDAudioGenerator not found. Create LED Animation System first.");
            }
        }
        
        [MenuItem("Tools/LED Animation System/Audio/Generate 5-Second Test Audio")]
        public static void GenerateTestAudio()
        {
            var audioGenerator = FindAudioGenerator();
            if (audioGenerator != null)
            {
                audioGenerator.EditorGenerateTestAudio();
            }
            else
            {
                Debug.LogWarning("❌ LEDAudioGenerator not found. Create LED Animation System first.");
            }
        }
        
        [MenuItem("Tools/LED Animation System/Audio/Upload All Audio Files to ESP32")]
        public static void UploadAllAudioFiles()
        {
            var audioUploader = FindAudioUploader();
            if (audioUploader != null)
            {
                audioUploader.UploadAllAudioFiles();
            }
            else
            {
                Debug.LogWarning("❌ ESP32AudioUploader not found. Create LED Animation System first.");
            }
        }
        
        [MenuItem("Tools/LED Animation System/Audio/Test Audio Playback on ESP32")]
        public static void TestAudioPlayback()
        {
            var audioUploader = FindAudioUploader();
            if (audioUploader != null)
            {
                audioUploader.EditorTestPlayback();
            }
            else
            {
                Debug.LogWarning("❌ ESP32AudioUploader not found. Create LED Animation System first.");
            }
        }
        
        [MenuItem("Tools/LED Animation System/Audio/Stop Audio Playback on ESP32")]
        public static void StopAudioPlayback()
        {
            var audioUploader = FindAudioUploader();
            if (audioUploader != null)
            {
                audioUploader.StopAudioOnESP32();
            }
            else
            {
                Debug.LogWarning("❌ ESP32AudioUploader not found. Create LED Animation System first.");
            }
        }
        
        [MenuItem("Tools/LED Animation System/Audio/Enable Real-time Audio")]
        public static void EnableRealtimeAudio()
        {
            var realtimePlayer = FindRealtimeAudioPlayer();
            if (realtimePlayer != null)
            {
                realtimePlayer.StartRealtimeAudio();
            }
            else
            {
                Debug.LogWarning("❌ RealtimeAudioPlayer not found. Create LED Animation System first.");
            }
        }
        
        [MenuItem("Tools/LED Animation System/Audio/Disable Real-time Audio")]
        public static void DisableRealtimeAudio()
        {
            var realtimePlayer = FindRealtimeAudioPlayer();
            if (realtimePlayer != null)
            {
                realtimePlayer.StopRealtimeAudio();
            }
            else
            {
                Debug.LogWarning("❌ RealtimeAudioPlayer not found. Create LED Animation System first.");
            }
        }
        
        [MenuItem("Tools/LED Animation System/Audio/Open Audio Export Folder")]
        public static void OpenAudioExportFolder()
        {
            string audioExportPath = System.IO.Path.Combine(Application.dataPath, "AudioExports");
            
            if (System.IO.Directory.Exists(audioExportPath))
            {
                EditorUtility.RevealInFinder(audioExportPath);
            }
            else
            {
                Debug.LogWarning("❌ Audio export folder not found. Generate audio first to create the folder.");
            }
        }
        
        [MenuItem("Tools/LED Animation System/Audio/Show Audio Integration Help")]
        public static void ShowAudioHelp()
        {
            string helpMessage = @"🎵 LED Animation Audio Integration

WORKFLOW:
1. Create LED Animation System (includes audio components)
2. Import Grasshopper data and create animations
3. Generate Audio: Tools → LED Animation System → Audio → Generate Audio for Current Animation
4. Upload to ESP32: Tools → LED Animation System → Audio → Upload All Audio Files to ESP32
5. Test Playback: Tools → LED Animation System → Audio → Test Audio Playback on ESP32

AUDIO GENERATION MODES:
• Harmonic: Musical tones based on LED patterns
• Ambient: Atmospheric soundscapes  
• Rhythmic: Beat-based patterns
• Reactive: Responds to LED color changes
• Cinematic: Dramatic audio for presentations

ESP32 REQUIREMENTS:
• SD card properly mounted (test with 'sdinfo' command)
• I2S audio working (test with 'tone' command)
• ESP32 connected to Unity (same network)

OUTPUT FORMAT:
• 44.1kHz, 16-bit stereo WAV files
• Compatible with ESP32 I2S playback
• Stored in Assets/AudioExports/

TROUBLESHOOTING:
• Check ESP32 IP address in ESP32Communicator
• Ensure SD card has free space
• Test ESP32 audio hardware first
• Check Unity console for detailed error messages";

            EditorUtility.DisplayDialog("Audio Integration Help", helpMessage, "Got it!");
        }
        
        // Helper methods
        private static LEDAudioGenerator FindAudioGenerator()
        {
            return Object.FindFirstObjectByType<LEDAudioGenerator>();
        }
        
        private static ESP32AudioUploader FindAudioUploader()
        {
            return Object.FindFirstObjectByType<ESP32AudioUploader>();
        }
        
        private static RealtimeAudioPlayer FindRealtimeAudioPlayer()
        {
            return Object.FindFirstObjectByType<RealtimeAudioPlayer>();
        }
        
        // Menu validation (enable/disable menu items based on availability)
        [MenuItem("Tools/LED Animation System/Audio/Generate Audio for Current Animation", true)]
        public static bool ValidateGenerateAudio()
        {
            return FindAudioGenerator() != null;
        }
        
        [MenuItem("Tools/LED Animation System/Audio/Upload All Audio Files to ESP32", true)]
        public static bool ValidateUploadAudio()
        {
            return FindAudioUploader() != null;
        }
        
        [MenuItem("Tools/LED Animation System/Audio/Test Audio Playback on ESP32", true)]
        public static bool ValidateTestPlayback()
        {
            return FindAudioUploader() != null;
        }
        
        [MenuItem("Tools/LED Animation System/Audio/Stop Audio Playback on ESP32", true)]
        public static bool ValidateStopPlayback()
        {
            return FindAudioUploader() != null;
        }
        
        #endif
    }
}
