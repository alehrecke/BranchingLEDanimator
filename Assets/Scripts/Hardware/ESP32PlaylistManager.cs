using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using BranchingLEDAnimator.Animation;
using BranchingLEDAnimator.Core;

namespace BranchingLEDAnimator.Hardware
{
    /// <summary>
    /// Manages playlists for ESP32 standalone animation playback
    /// Integrates with ESP32AnimationExporter for seamless workflow
    /// </summary>
    public class ESP32PlaylistManager : MonoBehaviour
    {
        [Header("Playlist Configuration")]
        [SerializeField] private string playlistName = "My LED Playlist";
        [SerializeField] private bool loopPlaylist = true;
        [SerializeField] private float transitionDuration = 0.5f;
        [SerializeField] private TransitionType transitionType = TransitionType.Fade;
        
        [Header("Animation Selection")]
        [SerializeField] private List<PlaylistItem> playlistItems = new List<PlaylistItem>();
        [SerializeField] private bool autoPopulateFromAnimationSystem = true;
        
        [Header("Scheduling")]
        [SerializeField] private bool enableScheduling = false;
        [SerializeField] private List<ScheduleRule> scheduleRules = new List<ScheduleRule>();
        
        public enum TransitionType
        {
            Instant,
            Fade,
            Crossfade,
            WipeLeft,
            WipeRight
        }
        
        [System.Serializable]
        public class PlaylistItem
        {
            public LEDAnimationType animation;
            public float customDuration = -1f; // -1 = use animation's default duration
            public bool enabled = true;
            public int priority = 0; // For weighted random selection
            
            [Header("Playback Settings")]
            public float speedMultiplier = 1f;
            public bool overrideColors = false;
            public Color primaryColorOverride = Color.white;
            public Color secondaryColorOverride = Color.black;
            
            public float GetDuration()
            {
                if (customDuration > 0f) return customDuration;
                return animation != null ? animation.duration : 10f;
            }
            
            public string GetDisplayName()
            {
                return animation != null ? animation.animationName : "Empty Slot";
            }
        }
        
        [System.Serializable]
        public class ScheduleRule
        {
            public string name = "Schedule Rule";
            public bool enabled = true;
            
            [Header("Time Conditions")]
            public bool useTimeRange = false;
            public float startHour = 18f; // 6 PM
            public float endHour = 23f;   // 11 PM
            
            [Header("Day Conditions")]
            public bool useWeekdays = false;
            public bool[] activeDays = new bool[7] { true, true, true, true, true, true, true }; // Mon-Sun
            
            [Header("Playlist Override")]
            public List<PlaylistItem> scheduledItems = new List<PlaylistItem>();
            public bool shuffleScheduledItems = false;
        }
        
        // References
        private ESP32AnimationExporter exporter;
        private LEDAnimationSystem animationSystem;
        
        // State
        private int currentItemIndex = 0;
        private bool isPlaying = false;
        private float currentItemStartTime = 0f;
        
        // Properties
        public int PlaylistCount => playlistItems.Count(item => item.enabled);
        public float TotalPlaylistDuration => playlistItems.Where(item => item.enabled).Sum(item => item.GetDuration());
        public string CurrentItemName => GetCurrentItem()?.GetDisplayName() ?? "None";
        
        void Start()
        {
            exporter = GetComponent<ESP32AnimationExporter>();
            animationSystem = GetComponent<LEDAnimationSystem>();
            
            if (autoPopulateFromAnimationSystem && animationSystem != null)
            {
                PopulateFromAnimationSystem();
            }
        }
        
        /// <summary>
        /// Auto-populate playlist from current animation system
        /// </summary>
        [ContextMenu("Populate from Animation System")]
        public void PopulateFromAnimationSystem()
        {
            if (animationSystem == null) return;
            
            Debug.Log("📋 Populating playlist from animation system...");
            
            playlistItems.Clear();
            
            foreach (var animation in animationSystem.availableAnimations)
            {
                if (animation != null)
                {
                    var item = new PlaylistItem
                    {
                        animation = animation,
                        enabled = true,
                        speedMultiplier = 1f,
                        priority = 1
                    };
                    
                    playlistItems.Add(item);
                }
            }
            
            Debug.Log($"✅ Added {playlistItems.Count} animations to playlist");
        }
        
        /// <summary>
        /// Export current playlist to ESP32
        /// </summary>
        [ContextMenu("Export Playlist to ESP32")]
        public void ExportPlaylistToESP32()
        {
            if (exporter == null)
            {
                Debug.LogError("ESP32AnimationExporter not found!");
                return;
            }
            
            // Prepare export list
            var animationsToExport = new List<LEDAnimationType>();
            
            foreach (var item in playlistItems)
            {
                if (item.enabled && item.animation != null)
                {
                    animationsToExport.Add(item.animation);
                }
            }
            
            if (animationsToExport.Count == 0)
            {
                Debug.LogError("No enabled animations in playlist!");
                return;
            }
            
            Debug.Log($"🚀 Exporting playlist '{playlistName}' with {animationsToExport.Count} animations...");
            
            // Export via animation exporter
            // This would integrate with the exporter's batch export functionality
            StartCoroutine(ExportPlaylistData());
        }
        
        /// <summary>
        /// Generate playlist metadata for ESP32
        /// </summary>
        private System.Collections.IEnumerator ExportPlaylistData()
        {
            var playlistData = new ESP32PlaylistData
            {
                name = playlistName,
                loopPlaylist = loopPlaylist,
                transitionType = (int)transitionType,
                transitionDuration = transitionDuration,
                items = new List<ESP32PlaylistData.PlaylistItemData>()
            };
            
            foreach (var item in playlistItems)
            {
                if (!item.enabled || item.animation == null) continue;
                
                var itemData = new ESP32PlaylistData.PlaylistItemData
                {
                    animationName = item.animation.animationName,
                    duration = item.GetDuration(),
                    speedMultiplier = item.speedMultiplier,
                    priority = item.priority,
                    overrideColors = item.overrideColors
                };
                
                if (item.overrideColors)
                {
                    itemData.primaryColor = item.primaryColorOverride;
                    itemData.secondaryColor = item.secondaryColorOverride;
                }
                
                playlistData.items.Add(itemData);
            }
            
            // Generate playlist JSON for ESP32
            string playlistJson = JsonUtility.ToJson(playlistData, true);
            Debug.Log($"📄 Generated playlist data:\n{playlistJson}");
            
            // This would be sent to ESP32 along with the animation files
            yield return null;
        }
        
        /// <summary>
        /// Calculate total storage requirements for current playlist
        /// </summary>
        [ContextMenu("Calculate Playlist Storage")]
        public void CalculatePlaylistStorage()
        {
            if (exporter == null) return;
            
            Debug.Log("📊 PLAYLIST STORAGE ANALYSIS:");
            Debug.Log("============================");
            Debug.Log($"Playlist: '{playlistName}'");
            Debug.Log($"Items: {PlaylistCount} enabled / {playlistItems.Count} total");
            Debug.Log($"Total Duration: {TotalPlaylistDuration:F1} seconds");
            Debug.Log($"Loop: {(loopPlaylist ? "Yes" : "No")}");
            Debug.Log($"Transition: {transitionType} ({transitionDuration}s)");
            Debug.Log("============================");
            
            float totalStorage = 0f;
            int itemCount = 0;
            
            foreach (var item in playlistItems)
            {
                if (!item.enabled || item.animation == null) continue;
                
                float duration = item.GetDuration();
                int frames = Mathf.RoundToInt(duration * 30f); // Assume 30 FPS
                float storageKB = (frames * 99 * 3) / 1024f; // 99 LEDs × RGB
                
                totalStorage += storageKB;
                itemCount++;
                
                Debug.Log($"  {itemCount:D2}. {item.GetDisplayName()}: {storageKB:F1} KB ({duration:F1}s)");
            }
            
            Debug.Log("============================");
            Debug.Log($"Total Storage: {totalStorage:F1} KB");
            Debug.Log($"ESP32 Capacity: ~2800 KB");
            Debug.Log($"Utilization: {(totalStorage / 2800f) * 100f:F1}%");
            
            if (totalStorage > 2800f)
            {
                Debug.LogWarning("⚠️ Playlist exceeds ESP32 storage capacity!");
            }
            else
            {
                Debug.Log("✅ Playlist fits within ESP32 storage limits");
            }
        }
        
        /// <summary>
        /// Optimize playlist for storage efficiency
        /// </summary>
        [ContextMenu("Optimize Playlist")]
        public void OptimizePlaylist()
        {
            Debug.Log("🔧 Optimizing playlist for storage efficiency...");
            
            // Sort by priority (higher priority items first)
            playlistItems = playlistItems.OrderByDescending(item => item.priority).ToList();
            
            // Disable low-priority items if storage is tight
            float currentStorage = CalculateCurrentStorageUsage();
            if (currentStorage > 2500f) // Leave some buffer
            {
                Debug.Log("📦 Storage tight - disabling low priority items...");
                
                for (int i = playlistItems.Count - 1; i >= 0; i--)
                {
                    if (currentStorage <= 2500f) break;
                    
                    var item = playlistItems[i];
                    if (item.enabled && item.priority <= 0)
                    {
                        item.enabled = false;
                        currentStorage = CalculateCurrentStorageUsage();
                        Debug.Log($"  - Disabled: {item.GetDisplayName()}");
                    }
                }
            }
            
            Debug.Log("✅ Playlist optimization complete");
        }
        
        /// <summary>
        /// Create a randomized version of the current playlist
        /// </summary>
        [ContextMenu("Shuffle Playlist")]
        public void ShufflePlaylist()
        {
            var enabledItems = playlistItems.Where(item => item.enabled).ToList();
            
            // Weighted shuffle based on priority
            var shuffledItems = new List<PlaylistItem>();
            var sourceItems = new List<PlaylistItem>(enabledItems);
            
            while (sourceItems.Count > 0)
            {
                // Calculate total weight
                int totalWeight = sourceItems.Sum(item => Mathf.Max(item.priority, 1));
                int randomWeight = UnityEngine.Random.Range(0, totalWeight);
                
                // Select item based on weight
                int currentWeight = 0;
                PlaylistItem selectedItem = null;
                
                foreach (var item in sourceItems)
                {
                    currentWeight += Mathf.Max(item.priority, 1);
                    if (currentWeight > randomWeight)
                    {
                        selectedItem = item;
                        break;
                    }
                }
                
                if (selectedItem != null)
                {
                    shuffledItems.Add(selectedItem);
                    sourceItems.Remove(selectedItem);
                }
                else
                {
                    // Fallback: take first item
                    shuffledItems.Add(sourceItems[0]);
                    sourceItems.RemoveAt(0);
                }
            }
            
            // Replace enabled items with shuffled order
            int shuffleIndex = 0;
            for (int i = 0; i < playlistItems.Count; i++)
            {
                if (playlistItems[i].enabled)
                {
                    playlistItems[i] = shuffledItems[shuffleIndex];
                    shuffleIndex++;
                }
            }
            
            Debug.Log($"🔀 Shuffled playlist ({enabledItems.Count} items)");
        }
        
        private float CalculateCurrentStorageUsage()
        {
            float total = 0f;
            foreach (var item in playlistItems)
            {
                if (item.enabled && item.animation != null)
                {
                    float duration = item.GetDuration();
                    total += (duration * 30f * 99 * 3) / 1024f; // Rough estimate
                }
            }
            return total;
        }
        
        private PlaylistItem GetCurrentItem()
        {
            var enabledItems = playlistItems.Where(item => item.enabled).ToList();
            if (enabledItems.Count == 0 || currentItemIndex >= enabledItems.Count)
                return null;
            
            return enabledItems[currentItemIndex];
        }
        
        /// <summary>
        /// Preview playlist in Unity (for testing)
        /// </summary>
        [ContextMenu("Preview Playlist")]
        public void PreviewPlaylist()
        {
            if (animationSystem == null) return;
            
            Debug.Log($"🎭 Starting playlist preview: {playlistName}");
            StartCoroutine(PlaylistPreviewCoroutine());
        }
        
        private System.Collections.IEnumerator PlaylistPreviewCoroutine()
        {
            var enabledItems = playlistItems.Where(item => item.enabled && item.animation != null).ToList();
            
            foreach (var item in enabledItems)
            {
                Debug.Log($"🎬 Playing: {item.GetDisplayName()} ({item.GetDuration()}s)");
                
                // Set animation in animation system
                int animationIndex = animationSystem.IndexOfAnimation(item.animation);
                if (animationIndex >= 0)
                {
                    animationSystem.currentAnimationIndex = animationIndex;
                    yield return new WaitForSeconds(Mathf.Min(item.GetDuration(), 5f)); // Cap preview at 5s
                }
            }
            
            Debug.Log("✅ Playlist preview complete");
        }
        
        // Data structure for ESP32 playlist export
        [System.Serializable]
        public class ESP32PlaylistData
        {
            public string name;
            public bool loopPlaylist;
            public int transitionType;
            public float transitionDuration;
            public List<PlaylistItemData> items;
            
            [System.Serializable]
            public class PlaylistItemData
            {
                public string animationName;
                public float duration;
                public float speedMultiplier;
                public int priority;
                public bool overrideColors;
                public Color primaryColor;
                public Color secondaryColor;
            }
        }
    }
}
