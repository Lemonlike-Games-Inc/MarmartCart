**Game Design Document**

\-

**Marmart Carts**

Liang Mei

LemonLike Games Inc.

Version 4.0

Last Edited: 2026/10/07

A DOC for Vision / Production Alignment

[**0.0 Purpose of Version 4.0	3**](#0.0-purpose-of-version-4.0)

[**1.0 Game Vision	5**](#1.0-game-vision)

[**2.0 Design Pillars	6**](#2.0-design-pillars)

[**3.0 Gameplay Loops	7**](#3.0-gameplay-loops)

[3.1 Moment-to-Moment Loop	7](#3.1-moment-to-moment-loop)

[3.2 Round Loop	7](#3.2-round-loop)

[3.3 Match Opening Loop	7](#3.3-match-opening-loop)

[3.4 Between-Round Loop	7](#3.4-between-round-loop)

[3.5 Full Match Loop	8](#3.5-full-match-loop)

[**4.0 Resource Flow — One Page	9**](#4.0-resource-flow)

[4.1 Main Value Conversion	9](#4.1-main-value-conversion)

[4.2 Recovery Flow	9](#4.2-recovery-flow)

[4.3 Important Direction	9](#4.3-important-direction)

[**5.0 Core Match Direction	10**](#5.0-core-match-direction)

[5.1 Leading Cart, Drift, and Hype	10](#5.1-leading-cart,-drift,-and-hype)

[5.2 Chain, Capacity, and Overload	10](#5.2-chain,-capacity,-and-overload)

[5.3 Snake Battle	10](#5.3-snake-battle)

[5.4 Checkout	10](#5.4-checkout)

[5.5 Hypermarket Arena and Sale Events	11](#5.5-hypermarket-arena-and-sale-events)

[5.6 Powerups	11](#5.6-powerups)

[5.7 Chain Snowball Protection	11](#5.7-chain-snowball-protection)

[**6.0 Match & Meta-Game Direction	12**](#6.0-match-&-meta-game-direction)

[6.1 Simple Win Structure	12](#6.1-simple-win-structure)

[6.2 The Hypermarket Builds Itself	12](#6.2-the-hypermarket-builds-itself)

[6.3 Ability Samples	12](#6.3-ability-samples)

[6.4 Ability Sample Rarity	13](#6.4-ability-sample-rarity)

[6.5 Marmart Policies	13](#6.5-marmart-policies)

[6.6 Match Escalation Curve	14](#6.6-match-escalation-curve)

[6.7 Version 4.0 Simplification from Version 3.0	14](#6.7-version-4.0-simplification-from-version-3.0)

[**7.0 World & Art Direction	15**](#7.0-world-&-art-direction)

[7.1 World Premise	15](#7.1-world-premise)

[7.2 Tone	15](#7.2-tone)

[7.3 Player Characters — Aliens	15](#7.3-player-characters-—-aliens)

[7.4 NPC Shoppers	15](#7.4-npc-shoppers)

[7.5 The Hypermarket as a Misunderstood Human Archive	16](#7.5-the-hypermarket-as-a-misunderstood-human-archive)

[7.6 Visual Hierarchy for Gameplay	16](#7.6-visual-hierarchy-for-gameplay)

[7.7 Art Direction Guardrail	16](#7.7-art-direction-guardrail)

[**8.0 Content Design Rules	17**](#8.0-content-design-rules)

[8.1 Store Section Template	17](#8.1-store-section-template)

[8.2 Powerup Rule	17](#8.2-powerup-rule)

[8.3 Ability Sample / Ability Rule	17](#8.3-ability-sample-/-ability-rule)

[8.4 Marmart Policy Rule	17](#8.4-marmart-policy-rule)

[8.5 Sale Event Rule	18](#8.5-sale-event-rule)

# 0.0 Purpose of Version 4.0 {#0.0-purpose-of-version-4.0}

Version 4.0 is intentionally much shorter than the previous GDDs. Its purpose is not to record every rule, number, edge case, or implementation detail. It is the shared vision document for deciding what Marmart Carts should become, what should be built next, and what collaborators need to understand before contributing work.  
The first four sections follow a compact structure: Game Vision, Design Pillars, Gameplay Loops, and Resource Flow. Detailed feature specs should be written separately only when a system is ready to build.

| How to read this document Core Direction \= part of the intended product identity. Current Direction \= the design we are building or testing toward. Open Question \= deliberately not locked yet and should be resolved through prototype or playtest evidence. |
| :---- |

**Version 4.0 Design Reset**

The game has moved away from the Earth-Shopping Tour / alien trading structure in Version 3.0. The new direction keeps the strongest part of the project — shopping-cart driving, chained-cart battles, loot, checkout, and social chaos — and builds a lighter session meta-game directly around that core.  
• One evolving arena replaces travelling between separate Tour Stops.  
• Round wins become the simple session score: first to X points wins.  
• Store sections are modular content that open during the session instead of requiring full new maps.  
• Temporary Alien Tech replaces persistent stat-heavy augment builds.  
• Global Match Events replace most separate mini-game / Action Upgrade ideas by changing the rules of the same core game.  
• The fiction shifts toward aliens reconstructing and misunderstanding human shopping culture.

# 1.0 Game Vision {#1.0-game-vision}

**Game Title:** Marmart Carts  
**Genre / Tags**: 2–4 Player Party Game \| Local Multiplayer \| Competitive \| Shopping Cart \| Drift \| Snake-Like \| Chaotic \| Funny \| 3D Isometric  
**Platform Direction:** PC first. Local multiplayer is the current core format; broader multiplayer support is a future product decision.  
**Logline:**  
Marmart Carts is a chaotic 2–4 player party game where you and your friends are future alien visitors who mistake human shopping for a competitive sport. Drift carts through a hypermarket, grow snake-like cart chains, fight over groceries, block rivals to break their chains and rob their goods, weaponize everyday shopping items, and do whatever it takes to check out more than your friends\! Between rounds, new sections open, players gain new abilities, and even the match rules can change, turning every match into its own escalating shopping disaster.  
**Essence Statement:**  
A simple shopping-cart snake battle that escalates into a ridiculous, chaotic mess as the hypermarket layout, player abilities, and rules change round after round.  
**Signature Features**

* **Shopping-cart driving and drifting:** Players directly control a constantly moving shopping cart, using drifting and positioning to navigate, chase, escape, and fight.  
* **Snake-like cart chains:** Collecting carts grows a visible chain behind the player, increasing cargo capacity and turning the player's own body into a larger blocking weapon and target.  
* **Block, break, and rob:** Players attack primarily through movement. Cutting off a rival can break their cart chain and spill groceries back into the Hypermarket for anyone to take.  
* **Shopping items as ridiculous tools:** Tomatoes, bananas, ice cubes, Cola \+ Mentos, and other familiar store objects become Powerups and inspire increasingly absurd Abilities and Match Rules.  
* **An evolving Hypermarket:** The arena changes across a Match as new Sections open, temporary Abilities enter play, and Match Rules twist how later Rounds work.

**Audience & Positioning**  
Marmart Carts targets a social party-game audience, especially mixed-skill groups who want immediate laughs while still having room to improve at driving, drifting, blocking, and using different Powerups and Abilities. The game should be easy to understand in the first Round, fun in the first Match, and become more expressive and unpredictable over repeated play.  
**Non-Goals**  
\- Not a realistic supermarket or driving simulation.  
\- Not a stat-heavy roguelike where players must calculate builds to compete.  
\- Not a collection of unrelated mini-games. The shopping-cart battle remains the main game.  
\- Not random chaos without readable cause and effect. Players should usually understand what happened, why it happened, and who caused it.

# 2.0 Design Pillars {#2.0-design-pillars}

**Expressive Shopping-Cart Control:** Driving, drifting, positioning, and blocking should remain the game's primary forms of skill expression. New systems should enhance the importance of controlling the cart rather than bypass it.

**Growth Should Feel Powerful and Dangerous:** Players should want to grow longer cart chains. A longer chain should create clear advantages while introducing meaningful risks, but those risks should never make growth feel undesirable. Balance changes should protect the fantasy and visual appeal of building a ridiculous cart snake.

**Player-to-Player Interaction First:** Game systems should continually create reasons for players to enter shared spaces, contest the same resources, interfere with one another, and make decisions based on other players. Emergent interactions between players should take priority over isolated or purely system-driven challenges.

**Weaponize the Ordinary:** Familiar groceries, shopping objects, and supermarket behaviours should be the primary source of Powerups, Abilities, Match Rules, hazards, and comedy. Alien technology can exaggerate the experience, but the shopping fantasy should remain recognizable underneath it.

**Every Match Should Escalate Differently:** A Match should begin readable and become increasingly strange as new Sections open, players gain new Abilities, and Match Rules alter later Rounds. Escalation should create memorable stories without making the core cart battle difficult to understand.

| Pillar Test A new feature does not need to support every pillar, but it should clearly strengthen at least one without seriously damaging another. If a feature is funny but makes driving irrelevant, creates long inactive states, or makes the store stop reading as shopping, it is probably the wrong feature. |
| :---- |

# 3.0 Gameplay Loops {#3.0-gameplay-loops}

### 3.1 Moment-to-Moment Loop {#3.1-moment-to-moment-loop}

| DRIVE / DRIFT | GROW CHAIN | COLLECT | FIGHT / BLOCK | CHECK OUT | RE-ENTER |
| :---- | :---- | :---- | :---- | :---- | :---- |

Players constantly move between these actions. Combat is not a separate mode: it happens naturally because players want the same carts, groceries, Powerups, routes, Sale Events, and Checkout space.  
The core loop should remain recognizable even when later systems add more chaos around it.

### 3.2 Round Loop {#3.2-round-loop}

* A Round begins with the current Hypermarket Arena layout, active Marmart Policies, and each player’s current Ability if one has been granted.  
* Sale Events activate important Sections and concentrate players around carts, groceries, Powerups, or other valuable resources.  
* Players grow their chains, collect groceries, fight and block one another, use Powerups and Abilities, and repeatedly check out to secure value.  
* The Round ends when the timer runs out.  
* The player with the highest secured Checkout value wins the Round and gains 1 Match Point.

### 3.3 Match Opening Loop {#3.3-match-opening-loop}

* Before the first Round, the Match generates its starting global conditions.  
* The game rolls player count \+ 1 Opening Policy slots.  
* Each slot randomly reveals an Opening-eligible Marmart Policy.  
* Business as Usual may appear and apply no additional change.  
* All other revealed Opening Policies remain active for the entire Match.  
* Players do not vote on Opening Policies.  
* Players begin the first Round without an Ability Sample by default, keeping the opening Round closer to the baseline cart battle.  
* The purpose of Opening Policies is to give each Match its own identity before the Hypermarket begins escalating further.

### 3.4 Between-Round Loop {#3.4-between-round-loop}

After each Round:

* Show the Round result and current first-to-X Match score.  
* The Hypermarket may open a new Section in an available surrounding slot.  
* A Policy Update rolls one random Marmart Policy for the next Round.  
* By default, this Policy lasts for one Round.  
* Business as Usual means no temporary Policy change.  
* Players do not vote by default.  
* Sample Time begins.  
* Each player receives 2 random Ability Samples.  
* Each player chooses 1 simultaneously.  
* The selected Ability lasts for the next Round only and replaces the previous one.  
* Once all players have chosen, the selected Abilities are revealed to the lobby before play resumes.  
* The next Round begins with a changed combination of space, player capabilities, and shared rules.

| Ability Sample Principle The choice should be quick and playful rather than a deep build-planning phase. Players adapt to a small random selection instead of constructing long-term stat builds. |
| :---- |

Visual Exploration Note: The two-option Ability Sample screen can explore an original Marmart take on the familiar red-versus-blue choice composition: two exaggerated samples presented through Marmart’s own alien, hypermarket, and sample-booth visual language rather than directly reproducing existing film imagery.

### 3.5 Full Match Loop {#3.5-full-match-loop}

A Match begins close to the baseline shopping-cart battle but already carries its own identity through Opening Policies.  
As the Match continues:

* New Sections gradually expand and reshape the Hypermarket.  
* Each Round gives players new temporary Abilities through Ability Samples.  
* Policy Updates temporarily twist how individual Rounds behave.  
* Rare or unusual Policies and Abilities create stronger surprises and memorable stories.  
* The first player to reach the required number of Match Points wins the Match.

| Core Principle The Match meta-game should change the context around the cart battle, not replace the cart battle. Even in the final Round, players should still be driving, drifting, growing chains, collecting, blocking, and checking out. |
| :---- |

# 4.0 Resource Flow {#4.0-resource-flow}

Marmart Carts should use a small number of highly visible resources. Each one should create movement, conflict, growth, or a clear risk-and-reward decision, rather than exist only as a menu number.

| Resource | How It Is Gained | How It Is Used / Lost | Gameplay Role |
| :---- | :---- | :---- | :---- |
| Empty Carts | Cart Restocks, dropped carts, special sources, Emergency Cart Delivery | Attach to the chain; lost through crashes, chain breaks, or other effects | Physical growth: adds Capacity and blocking reach while increasing exposure |
| Load / Capacity | Each chained cart adds Capacity; groceries add Load | Overloading beyond Capacity reduces movement performance until Load is reduced or checked out | Makes chain growth valuable and creates a readable limit on greed |
| Groceries / Goods | Active Sale Events and Section-based spawns | Carried as potential value; secured through Checkout; can be dropped before being secured | Main objective resource that pulls players into contested Sections |
| Hype | Drifting, hitting NPCs/environment, and other stylish or chaotic actions | Spent to Speed Up; longer chains burn Hype faster while Speeding Up | Rewards expressive driving and provides burst movement |
| Powerups | Contested pickups or baskets linked to Sections and Sale Events | Consumed on use or after a short-duration effect | Immediate tactical tools for disruption, openings, and funny reversals |
| Ability Sample | Between Rounds, each player gets 2 random Ability Samples and chooses 1 | Grants its Ability for the next Round; replaced during the next Sample Time | Temporary personal twist without creating a long-term stat build |
| Gold Cart | Rare special spawn or other limited source | Protects part of a chain from Complete Chain Loss; remains contestable | Rare insurance object that creates protection, attention, and social conflict |
| Match Point | Win a Round by securing the highest Checkout value | Accumulates until a player reaches the required amount | Keeps the full-Match victory condition simple and readable |

### 4.1 Main Value Conversion {#4.1-main-value-conversion}

**Sale Event → Goods / Carts / Powerups → Carried Potential → Checkout → Secured Round Value → Round Win → 1 Match Point**  
Not every resource follows this path directly. Carts, Capacity, Hype, Powerups, and Abilities mainly help players compete over the value moving through it.

### 4.2 Recovery Flow {#4.2-recovery-flow}

**Lose Chain → Emergency Cart Delivery becomes available/Collect Carts on the map → Re-enter the active fight**  
Emergency Cart Delivery is not a normal resource to optimize. It is a participation safety floor available only when a player has zero or very few chained carts.

### 4.3 Important Direction {#4.3-important-direction}

• Growing the chain should remain desirable.   
• Normal maximum speed should remain broadly consistent between players.   
• Potential value should remain physically at risk until Checkout.   
• Comeback systems restore participation, not victory.   
• The long-term score is simply Round wins → Match Points → Match victory.  
• Avoid hidden economies. Ability Samples, Powerups, and Marmart Policies should preferably create visible rule changes, physical effects, or new actions rather than require players to track small numerical modifiers.

# 5.0 Core Match Direction {#5.0-core-match-direction}

### 5.1 Leading Cart, Drift, and Hype {#5.1-leading-cart,-drift,-and-hype}

The player directly controls the leading cart while the rest of the chain follows its movement trail. The cart is continuously moving and should feel playful rather than realistic. Drift is the main expressive movement mechanic, and Hype converts stylish or chaotic play into temporary Speed Up.  
• Normal top speed should remain broadly shared between players unless a clearly communicated special effect changes it.  
• Hype is gained through active play such as drifting, hitting NPC shoppers, and interacting with the environment.  
• Holding Speed Up spends Hype. Longer chains burn Hype faster, creating a soft anti-snowball cost without making large chains permanently slow.  
• Failed drift execution can lose potential Hype reward and should keep drifting risky enough that blocking remains meaningful.

### 5.2 Chain, Capacity, and Overload {#5.2-chain,-capacity,-and-overload}

Every empty chained cart increases the player’s carrying Capacity. Groceries consume Capacity based on their Load value. A player may exceed Capacity, but Overload adds a movement penalty that grows as the excess increases.  
• Longer chain \= more Capacity and more blocking surface.  
• Longer chain \= harder routing, more self-collision risk, and higher Hype cost while Speeding Up.  
• Overload is the main direct speed penalty. A large but correctly loaded chain should still keep up at regular speed.  
• Balance should preserve the fantasy that growing a ridiculous cart chain is desirable, not something experienced players learn to avoid.

### 5.3 Snake Battle {#5.3-snake-battle}

Blocking remains the main direct attack. Players win fights primarily through movement and positioning: cutting routes, forcing bad angles, trapping opponents, or making an opponent’s leading cart collide with a hostile cart chain.  
• A major collision can cause Complete Chain Loss, dropping carts and carried groceries back into the Hypermarket Arena as contestable resources.  
• Self-collision remains dangerous. A long chain is powerful because it controls space, but the owner must also survive their own geometry.  
• Powerups and Abilities should create openings or visible rule-breaking moments around this system, not make the snake battle irrelevant.

### 5.4 Checkout {#5.4-checkout}

Checkout is the banking action of the Round. Carried groceries are only potential value until the player successfully reaches a Checkout Station. Once secured, that value cannot be robbed by a later crash. The more players check out at once, the system may reward more free value. This creates the main greed decision: stay out longer for a bigger haul, or check out before somebody breaks the chain. 

### 5.5 Hypermarket Arena and Sale Events {#5.5-hypermarket-arena-and-sale-events}

The Hypermarket Arena centers on a stable central zone, with modular Store Sections surrounding it. Sale Events temporarily make one Section especially important by producing carts, groceries, Powerups, or another valuable opportunity. Their job is to focus the fight and stop players from quietly farming separate corners of the map.  
• The center remains the stable navigation anchor across every Match.  
• Only a limited number of Sections should be high-value at the same time so players naturally collide.  
• Sale Events should tell players where the important opportunity is right now; they are not global rule changes.  
• Announcements and strong visual telegraphing should communicate active opportunities without requiring players to read small UI while driving.

### 5.6 Powerups {#5.6-powerups}

Powerups are map-contestable, usually grocery- or shopping-themed tools. They are common enough to become part of the standard fight and should be understandable from a single visual gag or short description.  
• Projectile Powerups use an aim direction and readable landing/hit presentation.  
• Non-aim Powerups apply immediately or for a short duration.  
• Sections can own different Powerup pools, making the current Hypermarket layout affect how the Round plays.  
• Current prototype families include Tomatoes, Ice, Cola \+ Mentos, Strong Fan, and Banana-style hazards.

### 5.7 Chain Snowball Protection {#5.7-chain-snowball-protection}

Losing every chained cart creates the game's most dangerous dead state: the player loses blocking reach, cargo Capacity, and meaningful access to the player-to-player interaction and Sale Event at the same time. Version 4.0 treats this as a participation problem, not simply a balance problem.  
**Emergency UFO Cart Delivery — Current Direction**  
• Available only when a player has zero chained carts, or another very low threshold if testing proves necessary.  
• Calls a small fixed delivery of approximately 2–3 normal carts directly near the player.  
• Uses eligibility plus cooldown rather than a harsh per-Match lifetime limit by default.  
• The goal is not to make the losing player strong. The goal is to let them meaningfully play again immediately.  
**Gold Cart — Current Direction**  
• Gold Carts appear rarely and are never removed by normal Checkout.  
• If Complete Chain Loss reaches a Gold Cart, the loss stops at that point; the Gold Cart itself is released and remains contestable rather than disappearing from the Match.  
• Gold Cart protection is equally available to all players and should create a memorable shared object, not function as hidden rubber-banding.

| Version 4.0 Avoids No default “loser gets a much higher top speed” rule. A short chain may naturally be easier to maneuver, but obvious speed advantages can make interaction impossible for the slower side and risk the core balance. |
| :---- |

# 6.0 Match & Meta-Game Direction {#6.0-match-&-meta-game-direction}

### 6.1 Simple Win Structure {#6.1-simple-win-structure}

A Match uses a simple first-to-X format. Each Round produces one winner, and that player gains 1 Match Point. The exact target can change by mode or demo format, but players should always understand who is winning without learning a second economy.

### 6.2 The Hypermarket Builds Itself {#6.2-the-hypermarket-builds-itself}

The long-term Hypermarket Arena centers on 1 central zone plus up to 8 surrounding Section slots. A Match begins with only part of the Hypermarket active. As Rounds finish, new Sections can open in currently unused slots.  
• Section choice is system-generated by default rather than player-voted. This preserves variety and avoids repeatedly selecting only favourite content.  
• Once the content pool is large enough, use anti-repeat / shuffled-bag logic rather than pure independent randomness.  
• Placement can also be randomized among valid empty slots so the same Section does not always occupy the same route relationship.  
• Opening more Sections expands the available groceries, Powerups, Sale Events, and visual identity without requiring a completely new full map.  
• A Section should follow a shared footprint and gameplay contract so new content stays affordable for a small team.

### 6.3 Ability Samples {#6.3-ability-samples}

Starting after the first Round, each player receives two random Ability Samples and chooses one Ability for the next Round. The choice is personal and simultaneous: there is no shared draft order, no pick priority, and no long-term build stack.  
• Each player receives 2 random Ability Samples and chooses 1\.  
• The selected Ability lasts for the next Round only and replaces the previous Ability at the next Sample Time.  
• After all players choose, selected Abilities should be revealed to the whole lobby before play resumes. This can be postponed in the prototype if presentation scope is limited.  
• The first Round begins without Abilities by default, keeping the opening closer to the baseline cart battle.  
• Abilities should be highly visible and allowed to break normal rules in funny ways: Tomato King, XL Cola, Super Slippy Banana, Stasis, Time Control, telegraphed Laser, short Flight / Jump Engine, etc.  
• A separate passive-augment system is postponed. Interesting passive ideas can be folded into an Ability until there is enough content to justify another layer.

| Sample Time Presentation Note The two-choice screen can explore an original Marmart “pick one of two samples” visual gag. A supermarket sample counter is the primary reference; the familiar two-choice meme composition can inspire staging without directly copying existing film imagery. |
| :---- |

### 6.4 Ability Sample Rarity {#6.4-ability-sample-rarity}

| Tier | Meaning | Visual Direction |
| :---- | :---- | :---- |
| Gold / Normal | Baseline tier. Every Ability should already feel useful and fun; there is no trash tier. | Warm gold, clean premium frame. |
| Prismatic / Rare | Less common, more spectacular, specialized, or rule-breaking — not simply double the raw power. | Diamond-like / colourful reflective treatment. |
| Unique | Special Abilities with Match-level identity. A specific Unique can appear only once during the whole Match. | Near-black or dark body, deep red energy, thin gold detail. |

• Some Ability Samples may trade certainty for rarity, such as “replace this with a random Ability one tier higher.” Unique results still obey the once-per-Match rule.  
• A Mystery Ability Sample can hide the name, effect, and even rarity until selected. This should be treated as a playful gamble, not the default for every choice.

### 6.5 Marmart Policies {#6.5-marmart-policies}

Marmart Policies are global modifiers issued by the Hypermarket. They change how one or more Rounds are played and affect every player. Policies are system-generated by default rather than voted on, letting the whole lobby react to the same shared problem instead of repeatedly optimizing a menu choice.  
**Opening Policies**  
• Before Round 1, the game rolls player count \+ 1 Opening Policy slots.  
• Each slot reveals a random Opening-eligible Marmart Policy.  
• Business as Usual may appear and creates no additional change.  
• Every other revealed Opening Policy remains active for the entire Match.  
• Only Policies designed to stay fun across a full Match may be Opening-eligible; not every Policy needs a permanent version.  
• The Opening Policy combination gives each Match a baseline identity before the Hypermarket begins escalating further.  
**Policy Updates**  
• After each Round, one random Policy Update is rolled for the next Round.  
• By default, the Policy lasts for one Round only.  
• Business as Usual means no temporary Policy change for that Round.  
• Players do not vote by default. Voting can exist later as a special Policy or mode if it proves fun.  
• Policies should preferably preserve the same controls and core verbs. They bend Marmart Carts instead of replacing it with a separate mini-game.  
**Example Marmart Policies**

| Policy | Tier Direction | What Changes |
| :---- | :---- | :---- |
| Business as Usual | Gold / baseline slot | No additional Policy effect. Used to create naturally calmer Matches or Rounds. |
| Blackout | Gold / Prismatic | Visibility drops for short windows while driving and fighting continue normally. |
| Tomato Rain | Gold | Tomato impacts randomly threaten the Hypermarket and can blind leading carts. |
| Private Checkout | Prismatic / Unique candidate | Checkout Stations belong to players; using another player’s station secures value for that owner. |
| Hot Potato / Infection | Prismatic / Unique | A status transfers through blocking or contact and creates a chase problem inside the normal cart battle. |
| Absolute Chaos | Unique | For the rest of the Match, future compatible Policy Updates no longer expire and begin stacking. |

• Internally, Policies should have compatibility categories so stacking effects such as Absolute Chaos remain readable and do not create contradictory scoring rules.

### 6.6 Match Escalation Curve {#6.6-match-escalation-curve}

The early Match should teach the baseline game. Later Rounds should become visually and mechanically louder. The player should feel the same Hypermarket gradually turning into a ridiculous version of itself rather than being thrown into maximum complexity from the first minute.  
• Opening: Opening Policies establish the Match identity; fewer Sections are active; Round 1 has no Ability by default.  
• Early / Middle: more Sections open, Gold Abilities dominate, occasional Prismatic Abilities and stronger Policies enter play.  
• Late: the Hypermarket is fuller, Prismatic and rare Unique moments become more likely, and the Match has developed a story specific to that lobby.

### 6.7 Version 4.0 Simplification from Version 3.0 {#6.7-version-4.0-simplification-from-version-3.0}

| Version 3.0 System | Version 4.0 Direction |
| :---- | :---- |
| Earth-Shopping Tour across separate stops | One evolving Hypermarket Arena across a multi-Round Match |
| Earth Cash \+ alien trade economy | Removed from the core Match structure |
| Persistent augment build stacking | One temporary Ability chosen from 2 Ability Samples and replaced each Round |
| Super Alien Assist as a separate ultimate economy | Fold spectacular ideas into Abilities, Powerups, or Marmart Policies when useful |
| Mini Tour Activities | Not a core layer; preserve only ideas that bend the main cart battle |
| Action Upgrades | Reframed as shared Marmart Policies |
| Match Events | Replaced by Marmart Policies / Policy Updates; Sale Events remain the in-Round location-focusing system |
| Gold Cart as victory currency | Rare in-Round protection / contest resource |

# 7.0 World & Art Direction {#7.0-world-&-art-direction}

### 7.1 World Premise {#7.1-world-premise}

Long after the human world is no longer around to explain itself, a group of aliens discovers broken records of Earth shopping culture. They understand that humans gathered in enormous stores, pushed carts, chased sales, filled them with objects, and rushed through Checkout, but they completely misunderstand why.  
The aliens conclude that shopping was a competitive entertainment activity. Using technology that can construct physical spaces from incomplete cultural data, they rebuild the “human shopping experience” as the Hypermarket Arena and keep “improving” it after every Round: opening new Sections, issuing Marmart Policies, and handing out bizarre Ability Samples like free samples.  
This is the main fiction the game needs. The exact historical reason humans disappeared, whether old AI played a role, or how the archive survived can exist as optional deeper lore later, but it should not be required to understand the game or carry a dark tone into the main experience.

### 7.2 Tone {#7.2-tone}

• Delightful, dumb, colourful, toy-like, physical, and slightly wrong.  
• Humour comes from ordinary shopping objects and retail behaviour being treated with absurd seriousness.  
• Alien technology should feel like over-engineering applied to mundane shopping problems.  
• Destruction and collisions should read as slapstick, not injury or cruelty.  
• The game can be chaotic without becoming visually dirty, gritty, or cynical.

### 7.3 Player Characters — Aliens {#7.3-player-characters-—-aliens}

The player characters should be simple enough to animate clearly from a high isometric camera but distinctive enough that somebody can recognize “the Marmart Carts alien” from a short video. The current inspiration direction is the memorable toy-like absurdity of Katamari combined with the simple, readable character appeal of Minions — used as reference points, not as a target to copy.  
• Strong silhouette before costume detail.  
• Large, readable head/body relationship and exaggerated reactions.  
• Simple arms or disconnected bubble-like hands are acceptable if they improve animation readability and production scope.  
• Four-player identity must remain readable through player colour, face/accessory variation, or another high-level cue.

### 7.4 NPC Shoppers {#7.4-npc-shoppers}

NPC shoppers are supporting comedy and traffic, not hero characters. The current preferred direction is cute roly-poly / toy-like animal shoppers, or another similarly soft chibi design that makes being knocked over feel harmless and funny. The fiction does not need to explain them heavily; they can simply be another imperfect reconstruction created by the aliens.  
• NPC silhouettes should be simpler and less visually dominant than the player aliens.  
• They should wobble, roll, bounce, or recover in ways that communicate toy-like slapstick.  
• Their colours should avoid competing with critical player-colour readability.

### 7.5 The Hypermarket as a Misunderstood Human Archive {#7.5-the-hypermarket-as-a-misunderstood-human-archive}

The Hypermarket Arena is not a perfectly realistic supermarket. It is an alien-built reconstruction assembled from fragments of human culture. This allows normal departments, country-inspired markets, holiday Sections, seasonal displays, electronics, toys, seafood, and other retail ideas to coexist without forcing a completely new map for every theme.  
The inconsistency is intentional: the aliens are reconstructing shopping from incomplete information. A Korean market beside Frozen Foods beside a Halloween aisle isn't a lore problem—it's the world's identity.

### 7.6 Visual Hierarchy for Gameplay {#7.6-visual-hierarchy-for-gameplay}

**1\. Players / Leading Carts:** Highest priority. Direction, ownership, drift state, danger, and active Ability must read immediately.  
**2\. Cart Chain:** Chain length, Capacity state, dropped carts, and high-value cargo should remain legible in motion.  
**3\. Powerups / Hazards / Sale Events:** Gameplay objects and active opportunities need strong silhouettes and effects that survive split-screen scale.  
**4\. Environment / NPC Shoppers**: The Hypermarket should feel alive and charming but must visually support the first three layers rather than compete with them.

### 7.7 Art Direction Guardrail {#7.7-art-direction-guardrail}

**Shopping object first; alien misunderstanding second.** A giant tomato summoned by absurd alien technology still reads as Marmart Carts. A generic plasma rifle does not. Sci-fi should amplify the shopping joke instead of replacing the shopping identity.

# 8.0 Content Design Rules {#8.0-content-design-rules}

### 8.1 Store Section Template {#8.1-store-section-template}

A new Store Section is the main scalable environment-content unit of Version 4.0. It should fit a shared Section slot and add personality without requiring the whole Hypermarket Arena to be rebuilt.

| Section Question | Required Answer |
| :---- | :---- |
| Theme / visual hook | What shopping culture, department, food category, country, or festival does this Section represent? |
| Top-down layout hook | What makes navigation through this slot different while still respecting the standard footprint and entrances? |
| Goods | What objects naturally appear here and what Capacity / value roles do they support? |
| Signature Powerup(s) | What 1–2 items from this Section can become funny combat tools? |
| Optional local behaviour | Is there one readable hazard, prop interaction, or Sale Event twist worth adding? |
| Production kit | Which props, materials, signs, shelves, and VFX can be modular and reused? |

### 8.2 Powerup Rule {#8.2-powerup-rule}

• Prefer familiar grocery and shopping objects over abstract weapons.  
• The effect should be explainable in one sentence and readable from the visual gag.  
• Powerups should create openings, movement problems, or positioning decisions rather than direct one-button victory.  
• Examples: Tomato Packs, Ice Cubes, Cola \+ Mentos, Strong Fan, Banana Peels.

### 8.3 Ability Sample / Ability Rule {#8.3-ability-sample-/-ability-rule}

• An Ability should break or exaggerate one normal rule in a visible way.  
• The Ability Sample choice should be quick: 2 random options, choose 1 for the next Round.  
• Use cooldowns and clear telegraphing when an effect is strong.  
• Avoid invisible stat math. If the effect is numerical, translate it into something physical, oversized, or funny where possible.  
• Abilities may enhance familiar shopping items or introduce limited alien nonsense, but the shopping identity should remain dominant.  
• Examples: Tomato King, XL Cola, Super Slippy Banana, Stasis, Time Control, telegraphed Laser, short Flight / Jump Engine.

### 8.4 Marmart Policy Rule {#8.4-marmart-policy-rule}

• A Marmart Policy is global, easy to announce, and changes how one or more Rounds behave for everyone.  
• Opening Policies may last the whole Match; Policy Updates normally last one Round.  
• Business as Usual is a valid result and creates no additional rule change.  
• Prefer effects that use the same controls and core verbs, creating a new social problem rather than a standalone mini-game.  
• Policies that can stack must define compatibility so combinations remain readable.  
• Examples: Blackout, Tomato Rain, Private Checkout, Hot Potato, Golden Burger, Absolute Chaos.

### 8.5 Sale Event Rule {#8.5-sale-event-rule}

• A Sale Event is temporary and location-focused: it tells players what area or resource matters right now.

• Its main purpose is to concentrate players into shared spaces and create conflict over carts, groceries, Powerups, or another opportunity.

• Sale Events should be strongly announced and visually telegraphed.

• Unlike Marmart Policies, Sale Events do not redefine the global rules of the Round.

