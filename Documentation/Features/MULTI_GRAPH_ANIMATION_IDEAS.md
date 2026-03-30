# Multi-Graph Animation Ideas

Cross-sculpture animation concepts for the gallery installation. These ideas leverage multiple branching LED sculptures coexisting in a shared physical space, enabling interactions between them.

## Spatial / Proximity-Based

### Arc Jump
When a pulse reaches an endpoint on one sculpture that is physically near an endpoint on another, the energy leaps across the gap like a spark and continues flowing through the second sculpture. The gap distance could determine the brightness or probability of the jump.

### Gravitational Pull
Light accumulates on the sides of sculptures that face each other, as if neighboring sculptures exert an attractive force. Endpoints nearest to another sculpture glow warmer or brighter, creating a sense of invisible tension between the structures.

### Tidal Wave
A wavefront sweeps across the entire gallery space. Each sculpture lights up as the wave reaches it, creating a single phenomenon passing through all three structures with physical delay based on their actual positions in the room.

---

## Ecosystem / Biological

### Mycorrhizal Network
Inspired by how trees share nutrients through underground fungal networks. Energy deposited at any sculpture's endpoint flows "underground" and can emerge at endpoints on other sculptures. The gallery floor is the invisible shared root system connecting all three.

### Symbiosis
Each sculpture represents a different organism in an ecosystem. Feeding one through touch nourishes another. Sculpture A's activity causes Sculpture B to bloom, which causes Sculpture C to fruit, creating a visible chain of dependency across the gallery.

### Color Ecosystem
Each sculpture has a signature hue. When visitors interact with multiple sculptures simultaneously, their colors cross-pollinate. A blue sculpture temporarily picks up warm tones from a red one being touched at the same time, blending identities.

---

## Musical / Rhythmic

### Shared Heartbeat
All three sculptures pulse with a synchronized rhythm. The heartbeat rate responds to total gallery activity — more people interacting means a faster collective pulse. Silence lets it slow to a calm resting state.

### Call and Response
One sculpture sends a pulse outward. When it reaches the endpoint nearest another sculpture, that sculpture answers with its own pulse in a complementary color or pattern. Creates a visible conversation across the room.

### Conductor
Whichever sculpture has the most audience interaction becomes the conductor, setting the tempo and color palette for the others. Leadership shifts dynamically as the crowd moves between sculptures.

---

## Social / Audience-Driven

### Emotional Contagion
Each sculpture carries a mood (color temperature, speed, intensity). When a visitor walks from one sculpture to another, they carry traces of the first sculpture's mood with them, gradually shifting the second sculpture's state. Visitors become vectors of influence between sculptures.

### Tug of War
A shared pool of light energy that sculptures compete for. More audience attention on one sculpture draws energy away from the others, dimming them. Creates a visible tension and encourages visitors to spread out across the gallery.

### Bridge Builder
When visitors simultaneously touch endpoints on two different sculptures, a visible bridge animation forms — pulses travel back and forth between those specific endpoints as long as both are held. Multiple visitors can build multiple bridges, creating a web of connections across the room.

---

## Pattern / Mathematical

### Interference
Each sculpture radiates an invisible wave through the gallery. Where two sculptures' waves overlap constructively, both sculptures brighten at their nearest points. Destructive interference dims them. The physical placement of sculptures determines the interference pattern.

### Shadow Echo
Actions on one sculpture create time-delayed echoes on the others. Touch Sculpture A, and a few seconds later the same topological position (same branch depth, same relative branch) lights up on B and C. Like a ripple through parallel structures.

### Phase Sync
Each sculpture runs the same animation but at slightly different phases. Over time, they drift in and out of synchronization. Audience interaction on any sculpture nudges it toward sync with the others — achieving full sync triggers a special collective moment across all three.

---

## Narrative

### Migration
Light creatures that live on the sculptures occasionally leave one, travel through the air (represented by dimming on the departure endpoint and brightening on the arrival endpoint of another sculpture), and settle on a new sculpture. Touch can disrupt or attract them.

### Seasons
A slow global cycle affects all sculptures. Spring: growth pulses from roots to tips. Summer: full brightness, responsive to touch. Autumn: colors warm and slowly cascade downward. Winter: minimal light, only responding to direct interaction. Each sculpture is in the same season but expresses it differently based on its unique topology.

---

## Implementation Notes

All of these concepts can build on the existing multi-graph architecture:
- `LEDVisualizationEvents` with source-scoped events for per-sculpture state
- `LEDAnimationType.OwnerGraphManager` for graph-specific logic
- `GraphPlayerController` multi-graph endpoint discovery for cross-sculpture interaction detection
- `GalleryAudienceSimulator` for testing without a physical audience

Cross-sculpture communication would require a new shared state layer (e.g., a `GalleryAnimationCoordinator` component on the Gallery root) that animations can read from to learn about the state of other sculptures.
