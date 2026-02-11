**Bind TD**

**What is Bind TD**

BindTD is a top-down rogue-like tower defense game focused on bind turrets with different types of bullets, the goal is managing resources, unlocking upgrades, and utilizing towers to prevent losing lives

**Genre:**

Procedural Tower Defense, Rogue-like.

**Platform:**

PC (keyboard \+ mouse).

**Target Length (for a Demo):**

5-10 minutes per run.

**Core Experience:**

**Augment**: *A selectable card-style modifier that provides temporary or permanent effect, such as stat buffs, new bullet types, additional turret options or turret’s behavior, inspired by TFT-style augment*

The player feels smart for choosing the right **augment** or right **combinations** to make the towers overpowered and adapting based on the unpredictable terrain.

**Design Intent:**

BindTD is designed to emphasize decision-making over mechanical execution. Instead of fast inputs or micro-management, the player’s success depends on understanding terrain, enemy composition, and how different turret–bullet combinations interact. The roguelike structure is used to encourage experimentation rather than long-term progression.

**Core loop**

1. Generate a first map chunk with a valid enemy path and player base.  
2. Player inspects and buys towers with the current money amount and place on valid build cells.  
3. The game has no automatic wave timer. Each wave begins only when the player expands the map by creating a new chunk. Chunk expansion is available only at the end of a wave and does not cost money.  
4. Enemies spawn from the end point of the next chunk and traverse the path.  
5. Turrets automatically target enemies.  
6. Player observes effectiveness.  
7. Wave ends \-\> next chunk/wave.  
8. After winning the 7th waves we will receive 3 cards to choose to be stronger.  
9. Boss spawn at the 10th wave.

By tying wave progression directly to chunk expansion, the player controls the game’s pacing. This allows time for planning, observation, and adjustment between waves, reinforcing BindTD’s focus on strategic decision-making rather than reaction speed.

**Procedural generation**

1. Generation Goals  
* Always generate a valid path  
* Every grid cell that is not part of the enemy path and not occupied by terrain is buildable.  
* No soft locks  
2. Generator types

| Generator | Purpose |
| :---- | :---- |
| Path Generator | Enemy traversal |
| Terrain Generator | Visual / constraint / Turret placement |

Terrain is generated procedurally using rule-based tile placement to ensure valid paths and buildable areas.

Procedural generation in BindTD prioritizes gameplay reliability over visual variety. All generation rules are designed to prevent unwinnable states, such as blocked paths or unreachable build areas. Visual variation is secondary to ensuring consistent strategic readability.

**Player controls & Camera**

| Action | Input | Notes |
| :---- | :---- | :---- |
| Move | WASD / Edge Scroll / Hold Click Drag | Move camera on XZ plane |
| Rotate | Q / E | Fixed speed |
| Zoom | Mouse Wheel | Change FOV or camera position |

Camera scroll / Zoom mode

* FOV Zoom – Change camera FOV  
* Move forward – Move camera toward the floor  
* Height Adjustment (Y-axis) \- Lowering-raising camera

The camera system is inspired by classic strategy games and is designed to give players full spatial awareness of the battlefield. Adjustable height and FOV allow players to read terrain elevation and enemy flow without introducing manual camera complexity during combat.

**Turret System (Core mechanic)**

1. **Turret Concept**

Design Intent:

- Turrets are modular units defined by firing behavior and bullet behavior, allowing different gameplay variety.

  Turrets in BindTD are intentionally simple in isolation but gain depth through combination. A single turret is rarely optimal on its own. Effectiveness emerges from how firing behavior, bullet effects, and placement interact with enemy types and terrain.

2. **Turret Properties (SO)**

   Key stats:

* Damage  
* Fire rate  
* Bullets per Trigger  
* Fire Mode

  Fire Modes

| Mode | Description |
| :---- | :---- |
| Single | Fires one projectile per fire event. High fire rate can simulate automatic weapons |
| Multi-shot | Fires multiple pellets in a spread |
| Burst | Fires a fixed number of shots per trigger |
| Pulse | Emits a radial AoE effect |
| Arc | Fires projectiles in a high arc trajectory |
| Beam | Continuous or instant hitscan fire |


Projectile-based

*Projectile-based fire modes emphasize spatial interaction and timing. Travel time, spread, and trajectory allow terrain and enemy movement to influence effectiveness, rewarding placement and prediction.*

* Single  
* Multi-shot  
* Burst  
* Arc  
* Arc projectiles are always projectile-based  (no-hitscan), lower accuracy, slower fire rate and longer minimum travel time and are ineffective against fast enemies  
* If elemental/ debuff bullets will create a small zone for that effects (smaller than Pulse)

  *Fires projectiles in a high trajectory that land on the ground, allowing attacks over obstacles and barrier. Arc projectiles are slower and have limited accuracy.*

* Pulse (if projectile-based AoE)  
* Pulse effects do not stack with themselves.

  *Pulse fire modes emphasize area control and synergy rather than burst damage. They are intended to support positioning-based strategies and tower clustering rather than replace single-target damage sources.*

  Hitscan-based

  *Hitscan fire modes provide consistent and immediate damage, trading spatial interaction for reliability. These modes are effective against fast or evasive enemies but are less flexible against terrain-based defenses.*

* Beam (continuous)


3. **Bullet Properties (SO)**  
   *Bullet types define what happens when an attack succeeds, while fire modes define “how” that attack is delivered. This separation allows new gameplay interactions without introducing new turret types.*

| Bullet Type | Effect |
| :---- | :---- |
| Normal | Flat damage |
| Explosive | AoE damage |
| Electric | Chain/stun |
| Fire | Damage over time |
| Ice | Slow / freeze |
| Buff | Temporary stat increase (damage / fire rate) |
| Utility Debuffs | Stat reduction (armor / speed) |

   

   When used with Pulse fire mode, bullet effects are applied within an area instead of on direct hit. Damage values are normalized to avoid overshadowing direct-fire elemental bullets.

   Buff bullets never deal damage and never apply elemental effects.

   Utility Debuff bullets apply utility effects only and never deal damage.

   Allowed Utility Debuffs

* Slow (weaker than ice)  
* Vulnerability (increases damage to normal health only and has no special interaction with Shield HP)  
* Shield Shred (Reduce Shield HP effectiveness or rapidly deplete Shield HP without increasing damage to normal health

  Comparison rule

* Ice: slow \+ damage  
* Electric: stun \+ chain damage  
* Utility Debuff: utility only, no damage  
4. **Turret Construction flow**  
   When placed, turrets spawn with default ammunition and are immediately active.  
   Bullet types modify behavior post-placement.

**Enemy System**

*Enemies are designed to test specific aspects of the player’s build rather than overwhelm through numbers alone. Barrier enemies test positioning, Shield HP tests damage typing, and elite enemies test sustained effectiveness.*

Enemy Properties

* Health  
* Speed  
* Path-following only

Demo Scope

* 1 basic enemy  
* 1 tougher variant  
* 1 boss/elite enemy  
* 1 barrier enemy

Some enemies reduce damage from frontal direct-fire attacks, indirect effects such as Arc or ground-based AoE can bypass this mitigation. Some enemies possess Shield HP, represented by a separate health bar. Shield HP absorbs incoming damage before normal health. Shield Shred debuffs are particularly effective against Shield HP, while Vulnerability increases damage to normal health only.

Barrier and Shield HP are separate mechanics. Barrier affects attack direction, while Shield HP acts as temporary health.

**Progression**

*Progression is intentionally minimal in the demo to keep focus on moment-to-moment decision-making and system interaction rather than long-term optimization.*

Demo progression

* Wave-based difficulty increase  
* Chunk-based map extension

**Cut list & out of Scope**

*These features are intentionally excluded to ensure the demo can be completed, polished, and balanced within the available development time.*

- Out of scope  
* Story / Lore.  
* Meta progression.  
* Multiple biomes.  
* Advanced UI animation.  
* Audio polish beyond placeholders.  
* Complex Enemy AI behaviors

**Risk & Mitigations**

| Risk | Mitigation |
| :---- | :---- |
| Procedural bugs | Hard path validation |
| Over-complex turrets | Limit fire modes |
| Time pressure | Feature freeze after Week 2 |

