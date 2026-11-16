# CW Skate — a skateboard for Content Warning (BepInEx)

Ride, do tricks, grind, chain combos and farm **aura**.
Install: put `CWSkate.dll` into `BepInEx\plugins\CWSkate\`.

## Getting a board

The skateboard is a **real game item**, so **everyone in the lobby needs the mod**.

1. In the shop open **Misc** and add the **Skateboard** ($400) to the cart, like any other item.
2. Press **Order**. A delivery drone brings it, the board drops out of the box.
3. Pick it up with **E** — it takes an inventory slot.

- Board slot **not selected** → the board hangs **on your back**.
- Slot **selected** → you hold the board **in your hands**. **LMB = smack with the board**: players fall over, monsters get knocked back (and give aura). Settings: F8 → Board smack.
- At the top aura rank (**AURA GOD**) a smack sends a monster flying and you do a victory dance. But if that monster kills you afterwards (within 3 minutes), you lose **all your aura**, the screen says "HUMBLED" and the comments roast you (off switch: `HumbleOnDeath`, F8 → Board smack).
- **B** — get on / off the board (from any slot). Your hands are free while riding: you can film.
- You can drop, throw or give the board to a friend like any item. **You can't carry more than one.**

**Board upgrades.** The shop (Misc) sells parts. Buy one, take it in your hands and press **LMB**: it goes onto your board (the board has to be in your inventory):

| Part | Price | What it does |
|---|---|---|
| **Speed Wheels** (orange) | $35 | top speed +25%, pushing +20%, but you turn worse (-20%) |
| **Soft Wheels** (green) | $25 | you trip on small curbs and steps much less, quieter, but a bit slower (-8%) |
| **Maple Deck** (light edge) | $45 | wears out half as fast |
| **Rocket Booster** (red rocket under the tail) | $120 | hold **Alt**: fire, smoke, a roar and speed way past the normal top (110+ km/h). The faster you go, the less you can turn. Burns fuel (a full tank is ~20 s); a crash on the rocket hurts more (up to x6 damage) and wears the board more (x1.5) |
| **Rocket Fuel** (jerry can) | $10 | take it in your hands and press LMB: the rocket's tank is full again |

One set of wheels at a time: put on others and the old ones drop at your feet. The parts live in the board itself: give it to a friend and the parts go too, if it snaps they're gone. What's on your board shows at the bottom left while riding.

**Skate sponsors.** The three monitors in the house (they open after the first quota) now also offer deals from skate brands. Like the game's own deals they are checked on the uploaded video, and they pay like the game's (money, meta coins or items):

| Brand | Task | The drone brings |
|---|---|---|
| **Ride to Hell** (skate shop) | film 3 / 5 / 8 / 12 skate tricks of any kind | **the Ride to Hell board**: fire and RIDE TO HELL on it (its skin can't be changed, it looks the same for everyone). As fast as with speed wheels (+25% top speed, +20% pushing) but with normal turning, wears out 2.5x slower, leaves a fire track behind the wheels |
| **Grind Kings** | 1-4 grinds or wallrides (rails, walls, a monster's back) | - |
| **Monster Wheels** | 1-4 moments with monsters: crash into one, smack one, ride away from a chase, ride on its back | - |
| **Bail Insurance Co.** | 1-4 falls off your board on camera | **an insurance policy**: works like Minecraft's totem of undying. Keep it in your inventory: when you would die, you don't (30 HP, a golden burst), you're **invulnerable for 15 seconds** (a timer at the top of the screen) and the policy is used up. It's the size of a real A4 sheet |
| **Boom Rockets** (a rocket company run by a certain billionaire) | 1-3 rocket rides | the rocket booster and fuel |

The game counts the same trick from the same player only a few times per video, so film different tricks.

**After every bail** the board flies out of your inventory and lands nearby as a normal item — pick it up with **E** (can be turned off: F8 → Bails).

Before you have a board the mod shows nothing on screen. Aura and records are kept in the save file (can be made global in the settings).

## Controls

| Key | Action |
|---|---|
| **B** | get on / off the board |
| **W** | push (hold to keep pushing) |
| **A / D** | turn, the camera follows the board |
| **S** | brake, at speed — powerslide |
| **Space (hold → release)** | ollie, the longer you hold the higher |
| **Ctrl (hold)** | manual (back wheels) |
| **Ctrl + W** | nose manual (front wheels, more points) |
| **Ctrl + A / D** (slow) | kickturn — pivot on the back wheels, nose up |
| **Ctrl + tap jump** | boneless — plant a foot, higher than an ollie |
| **Shift right after landing** | revert — 180 on the ground, the combo goes on |
| **S + Space** (rolling) | hippie jump: you jump, the board rolls on by itself (under a bar, under a monster...) and you land back on it. Miss it and you bail. Something under you = "Hippie Jump Over", worth more |
| **F (hold)** | skitch — next to a monster or player (up to ~3 m) you grab on and get towed. Works any time: standing or running, on the ground or in the air. Let go and you keep its speed. Fall back more than ~4 m and you lose your grip. In the air with nobody near, F is the kickflip |
| **Y (hold)** | skate train: a player right in front of you (up to ~2 m) - both hands on their shoulders, you ride behind them facing forward. The next skater can grab on to you the same way, making a whole train (a longer train is a better video) |
| **T** (the game's emote key) | dance on the board while riding: you keep rolling and can turn, your feet stay on the board. The dance counts in the combo and on video |
| **G** (off the board) | throw the board ahead — run after it and hop on (not T, the emote key, and not V, the game's push-to-talk) |
| **F5** | first / third person |
| **F8** | settings menu |
| **H** | controls list |
| **Tab (hold)** | records: best combos and stats |

In the air:

| Key | Trick |
|---|---|
| **A / D** | spin 180 / 360 / 540... |
| **F / G / X / C / Z** | kickflip / heelflip / shove-it / tre flip / impossible (again mid-flip = double) |
| **Shift** | Indy grab. **Shift+W** nosegrab, **Shift+S** tailgrab, **Shift+Ctrl** melon, **Shift+Ctrl+W** crail, **Shift+Ctrl+S** stalefish. Let go before you land! |
| **Shift → Space** | while grabbing: **Space** = airwalk, **W+Space** = superman |
| **W+Space / S+Space** | frontflip / backflip together with the board. Again mid-flip = double (then triple). Not round before landing = bail. Speed: `SaltoSpeed` in F8 → Air |

**Wallride:** ollie into a wall at an angle (you have to be going into it a bit, not just past it) — ride along it for up to ~2.5 s, slowly sliding down, **Space** to jump off.
**Wallplant:** flying straight at a wall — hold **Ctrl** to kick off it and turn around.
**Drop-in:** roll off a ledge (without jumping) — that's a trick. Holding **Ctrl** you can take almost twice the height.
**Grinds:** ollie (a real jump — rolling off a curb doesn't start a grind) onto a rail or a ledge edge (50-50, or a boardslide if the board is sideways). **Space** to pop off.
**Monster grind:** ollie down onto a monster — you ride on its back wherever it runs (A/D to turn around). **Space** to pop off with its speed. If it falls over, dies or shakes you off, you drop. Off switch: F8 → Grinds (`MonsterGrinds`).

## Video and comments

Tricks caught on camera give **views** and **viewer comments about skating**.
Bigger combos give more views (+20% per trick in the combo, up to x3). Overall multiplier: F8 → Video & comments.

**Escape:** when a monster is actually running after you and you ride away faster than 5 m/s, the chase goes into the video (closer monster and higher speed = more). Get away (chased for 2+ s, it got closer than 12 m) — "ESCAPED!", +300 aura (`EscapeAura`) and a big moment for the video. On-screen chase hints are off by default (`ShowChaseHUD`).

**Board wear** (`BoardWear`, speed `WearRate`): bails, hard landings, grinds, wallrides, monster grinds and smacks wear the board out (about 10-12 hard bails). The percentage is on the HUD (`BOARD 63%`). Below 75% the wheels squeak and the deck creaks; below 50% it's slower and takes landings worse; below 30% it shakes. At zero it snaps in half and is gone from your inventory — buy a new one. The wear is stored in the board itself: it stays when you drop it, give it away or save.

**Bails hurt:** falling off the board does damage (`BailDamage`, 10 at a normal speed, up to x2.5 at high speed). If something else knocked you off (a shot, a hit, a monster grab), that already did the damage — it isn't taken twice.

View bonuses (switch: `ContentBonuses`):
- **difficulty:** double flip x1.5, triple x2.2, tre flip / impossible x1.3, fakie x1.15, boneless x1.4, nose manual x1.4, boardslide x1.3, revert x1.2, nosegrab / tailgrab x1.2, melon x1.1, stalefish / crail x1.25, airwalk x1.5, superman x1.7, frontflip / backflip x1.8, spin 540 x1.3 / 720 x1.7 / 900+ x2.2, kickturn 360+ x1.3;
- **size:** long air up to x2.5, high drop-in up to x2 (Big Drop In another x1.3), long grind up to x2.2, long manual up to x2, long wallride up to x1.8, high-aura combo up to x2.5, a fast bail or monster bonk up to x2 (and a bail mid-combo +20% per lost trick);
- **situation:** a trick during a chase x1.3 (dancing during a chase x2), a trick on a board under 30% x1.2, a trick in the air right above a monster x1.5, hippie jump over something x1.6, over a monster x2.4, the board snapping at speed up to x2, a long monster grind up to x2.5, skitching a monster x1.8 (longer / faster up to another x2);
- **surroundings:** a monster within 12 m x1.5, within 5 m x2, skating together x1.2 per skater nearby (up to x1.6), a selfie on the board x1.5, under 35% health x1.3, aura rank up to x1.4 (AURA GOD).
All together at most x10.
Comment texts are in `BepInEx\plugins\CWSkate\SkateComments_ru.txt`, `SkateComments_en.txt`, `SkateComments_fr.txt` and `SkateComments_zh.txt` — add your own lines, no restart needed. The comment language is `CommentsLanguage` (F8 → HUD & sound, Auto = the game's language).

## Skins

Folder `BepInEx\plugins\CWSkate\SkateSkins` (created on the first start), with `default.png` (bottom of the board) and `default_grip.png` (top).
Copy them, rename (e.g. `fire.png` and `fire_grip.png`) and draw your own — top of the picture = nose of the board, 1:4 works best.
In the game: **F8 → Skin** → pick with the arrows, Refresh picks up new files. Players with the mod see your skin. A dropped board keeps the skin of whoever had it last.

Other modded players' aura rank floats above their heads (off switch: F8 → Aura).

A glowing track follows your wheels in your aura rank's colour, a rainbow for AURA GOD (`WheelTrails`, F8 → Aura). Grinding a rail throws sparks from under the board (`GrindSparks`, F8 → Grinds). Other modded players see both.

## Difficulty

F8 → Bails → `Difficulty`:
- **Easy**: the board hops over bumps, curbs and steps by itself; walls only knock you off a bit above pushing speed; your body getting stuck only counts if it lasts; landings forgive mistakes (sideways - only fully sideways fails, an unfinished flip / frontflip / backflip counts with more left to go, the legs take 1.5x the drop, a grab you still hold doesn't matter).
- **Normal** (default): the board hops over bumps and curbs only at pushing speed (faster - downhill or on the rocket - a curb throws you); walls knock you off above 5 m/s (18 km/h); getting your body stuck or landing sideways throws you off; unfinished flips and drops are a bit more forgiving than on Hard; you still have to let go of grabs.
- **Hard**: like Skate 3 - all of that throws you off.

## Explosions

- **Rocket fuel and rocket boosters lying on the floor** blow up when you smack them with the board (LMB) or when they hit something hard (e.g. fall from a height). The ones next to them go off too. Off switch: F8 → Board smack → `ExplosiveFuel`.
- **The rocket on your board** can blow up (half the time) if you crash while it's burning, and it's gone from the board. Off switch: F8 → Bails → `RocketCanExplode`.
- An explosion throws and hurts everyone close (the closer, the worse), knocks monsters around and shakes the camera; everyone sees and hears it. On video it's its own moment with comments ("KABOOM", "michael bay approves").

## Skate 3 style physics

- You speed up with separate pushes, lean into turns, stand sideways (regular or goofy).
- Curbs, steps and boxes at the wheels → you fly over the board. Walls and people at body height → you bounce back.
- Bails: landing sideways, not finishing a flip, still grabbing, too big a drop.
- Trampoline: roll or land on it and you ride over it. Ollie on it to fly high. Hold jump when landing to bounce.
- No riding in water, in bed or sitting. You slide off small props.
- Crash into a monster at speed — you fly off the board but get **+500 aura**.
- Black holes, shots, hits and explosions push you on the board, strong ones knock you off.
- For ~1 s after getting up tricks don't work — so you don't bail again by accident.
- Some emotes are blocked on the board (`BlockedEmotes`, by default Workout 2 and Backflip p1).

## Settings

**F8** opens the menu in game: tabs, sliders, key rebinding, reset. Languages (F8 → HUD & sound): `Language` is the whole mod (the F8 menu, the descriptions in the settings file, the board's name, the words above a smacked monster), `CommentsLanguage` is just the viewer comments. The languages are Russian, English, French and Simplified Chinese (a game set to Traditional Chinese gets Simplified too). Auto in both = whatever the game's own menu is set to. `Language` covers everything on screen too (pop-ups, tips, the controls list, records); trick names (Kickflip, Manual...) and aura ranks (Sigma, AURA GOD...) stay English.

**First-ride tips:** the very first time you get on a board, 7 tips show up one by one at the top of the screen (pushing, jumping, tricks, combos and aura). They never come back; to see them again set `TutorialDone = false` in the config file.
You can also reset the aura of the current save there.

**For developers:** F8 → HUD & sound → `DevTools` adds a Developer tab: give any of the mod's items, start any skate sponsor deal (pick the difficulty) and complete it at once, add aura or go straight to AURA GOD, repair or nearly break the board, fill the rocket, remove the parts; plus debug text on screen (`ShowDebugInfo`), endless fuel (`InfiniteRocketFuel`) and god mode (`NoDamage`).
All settings are also in `BepInEx\config\com.ddk.cwskate.cfg`, progress in `BepInEx\config\cwskate_progress.txt`.

## Building

```
cd "CWSkate"
dotnet build -c Release
```

The DLL is copied to `BepInEx\plugins\CWSkate` by itself.
After a game update, regenerate the public copy of the game code:

```
dotnet run --project tools\publicizer -- "..\Content Warning_Data\Managed\Assembly-CSharp.dll" "lib\Assembly-CSharp.publicized.dll"
```
