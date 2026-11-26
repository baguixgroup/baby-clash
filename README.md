# Baby Clash

A family-friendly party brawler for 1–2 players. Two cartoon babies fight over the nursery by flinging their
**diapers** and whatever **toys** drop into the playpen. Every hit fills the other baby's **Cry Meter**; at 100% they
burst into tears and lose the round. First to two rounds wins.

The twist: your diaper is your **armour** *and* your best weapon. Wear it and hits hurt 3× less. Throw it and you're
exposed until someone (maybe your opponent) picks it back up.

Everything you see and hear (characters, nursery, UI art, icons, music, sound effects) is generated from code
inside this project. There are no third-party assets and no licensing to worry about.

---

## Requirements

- **Unity 6.6 (6000.6.3f1)**, the version this project was last opened and verified with. Other Unity 6 versions
  with URP 17.x should work.
- Packages (resolved automatically): Universal Render Pipeline 17.6, Input System 1.20, uGUI, Test Framework.
- macOS or Windows. Keyboard, and any standard gamepad (Xbox, DualShock/DualSense, Switch Pro…).

## Open and play

1. Unity Hub → **Add project from disk** → this folder → open with Unity 6.6.
2. If Unity asks to enable the new Input System backends, click **Yes** (the editor restarts).
3. On first open (and whenever the generated content is out of date) the editor **builds all game content
   automatically**: render pipeline, textures, sculpted characters, props, prefabs, HUD icons and the Arena scene.
   It takes about 1–3 minutes. Watch for `[Baby Clash] Generated game content` in the Console.
4. Press **Play**. The title screen runs a CPU-vs-CPU demo in the background.

Menu **Baby Clash**:
- **Rebuild Game Content**: regenerate everything. Balance assets are kept.
- **Open Arena Scene**.
- **Capture Screenshots**: renders the arena and character close-ups to `Screenshots/`.
- **Build macOS App / Build Windows App**: see *Building* below.

## Controls

| | Move | Grab / Throw | Dash |
|---|---|---|---|
| **Baby 1** | W A S D | Space | Left Shift |
| **Baby 2** | Arrow keys | Enter (or Right Ctrl) | Right Shift (or /) |
| **Gamepad** (1st pad → Baby 1, 2nd → Baby 2) | Left stick / D-pad | A / Cross | B / Circle (or X / Square, R1) |

- **Pause**: Esc, P or Start.
- **Menus**: arrows/WASD or stick to move, Enter/Space/A to confirm, Esc/B to go back. The mouse works too.
- In **1 Player** mode the human baby can use either half of the keyboard.
- `F1` toggles the debug overlay (editor and development builds).

### The one button

**Grab / Throw** does the sensible thing:
1. No diaper on, and a loose diaper within reach → put it on (armour first).
2. Holding a toy → throw the toy.
3. A toy within reach → pick it up (it balances on your head).
4. Otherwise → rip off your diaper and throw it.

**Dash** is a belly slide. Thrown items pass straight through you during the first moment of a dash, and if you slide
into the other baby you **shove** them: a little Cry, a big push, and their toy flies loose.

## Game modes

- **1 Player / 2 Players**: the classic best-of-3 duel (75-second rounds; on time-up the calmer side wins).
- **Story Tour**: 20 levels in 5 rooms, three stars each, unlocked in order, with boss babies and 2v2 levels.
- **Party**: 2-4 babies, any mix of players and CPUs, any room, power-ups on/off, 2v2 teams with 4 babies. Modes:
  Clash / Brawl (last calm side wins), King of the Bottle (hold the ring, first to 20), Survival (endless waves),
  Tiny Tots.
- **Tiny Tots** (toddlers): hits make babies giggle, nobody loses, pickups are automatic, aiming is automatic, the CPU
  is gentle, and a lone player can press any key (or tap anywhere) to throw.
- **Rooms**: Nursery; Kitchen (sticky jam); Bath (slippery); Backyard (sandpits, sprinkler); Daycare (robot vacuum).
- **Power-ups**: Pacifier (absorbs 2 hits), Sippy Cup (speed), Lollipop (mega throws), Bubble Wand (traps the nearest
  rival), Teddy (calms your Cry Meter).
- **Progress**: rattles from every match, daily challenges with a streak, and a Wardrobe of hats, faces and outfits.
- **Touch** (touch screens, including the browser build): drag the left half to move, tap THROW / DASH.

Players 3 and 4 use gamepads. Everything above is built at runtime by `GameFlow.BuildGame` (`Scripts/Runtime/Modes`,
`UI/MenuKit`, `UI/GameMenus`), so it works with the existing generated scene.

Automated runs: `-smoketest <dir>` plus `-mode <Mode> -room <Room> -players N [-teams 1]`, `-level <id>`, `-screens 1`
(photographs the menus) or `-touch 1` (simulated finger). They use a throwaway profile and never touch the save.

## Online play

**PLAY ONLINE** on the title screen: **Quick Match** (joins any public room with space, or opens one that starts by
itself 20 seconds after a second player arrives), **Make a room for friends** (private, joined with the 6-letter
code shown in the room), or **Join a friend's room** (type the code with the on-screen keypad, a keyboard or a
gamepad). Up to 4 babies; the host picks the mode, room, power-ups, teams and CPU babies for free slots. Players who
join mid-match watch it and play the next one; if someone leaves mid-match, a CPU takes over their baby.

Kid-safe by design: anonymous sign-in, no names (players show as YOU / FRIEND), no chat, only four emotes
(HI!, HEE HEE!, WAAAH!, GOOD GAME! on the 1-4 keys, the gamepad d-pad or the buttons on the right).

How it works (`Scripts/Runtime/Net`):
- `NetSession`: Unity Gaming Services sign-in and the Lobby + Relay **session** (`com.unity.services.multiplayer`),
  which starts Netcode for GameObjects (`com.unity.netcode.gameobjects`) as host or client. A direct IP mode is
  there for tests and local networks.
- `NetGame`: the host runs the real match. 30 times a second it sends guests a snapshot (babies, items, power-ups,
  bottle, vacuum, round clock and scores) plus the events that happened (hits, throws, tears...), stamped with the
  host clock. Guests show the match 0.11 s behind, smoothing between snapshots and replaying events on time, so
  animation, sound and HUD work as offline. Babies and items on guests are *puppets* (`IsPuppet`): no physics or rules.
  A guest's stick and buttons go to the host (press counters, so a lost packet never loses a throw).
- `OnlineMenus`: the online screens and emote buttons. Messages and formats: `NetWire` (bump `Version` on changes).
- The project is linked to Unity Cloud (`ProjectSettings` → Services) with Relay and Lobby; free tiers apply.

Test two copies against each other: start one with `-netTest host -netOut DIR -netDirect 7777` (or `-netRelay`,
which writes the room code to `DIR/code.txt`) and another with `-netTest join -netOut DIR2 -netDirect 127.0.0.1:7777`
(or `-netCodeFile DIR/code.txt`). Add `-netProfile <name>` to give each copy its own anonymous player. Options for the
host: `-netGuests N -netMode Survival -netRoom Bath -netCpus 1 -netSeconds 120`. Each writes `net.log` and screenshots.

## Architecture

```
Assets/BabyClash/
  Scripts/Core/            BabyClash.Core: pure C# rules, no UnityEngine, unit tested
    CryMeter, HitResolver       damage, knockback and stun from attack x defence (diaper on/off)
    BabyStatus                  Ready / Throwing / Dashing / Stunned / Crying, cooldowns, dodge window
    ItemStatus                  Loose / Held / Flying ownership, thrower immunity, one hit per throw
    RoundStateMachine           countdown -> play (clock) -> result
    MatchStateMachine, RoundRules   best-of-N, time-up decision
  Scripts/Runtime/         BabyClash.Runtime: MonoBehaviours
    Baby/        BabyController (hub), BabyMovement (run/dash/knockback), BabyDiaperSlot, BabyHands,
                 BabyAnimator (procedural animation), BabyFace (expressions)
    Items/       Throwable (projectile base), Diaper, Toy, ToySpawner (pooled toy drops)
    AI/          BabyBrain (CPU player)
    Input/       BabyInputHandler (Input System, per-player keyboard half + gamepad slot)
    Round/       RoundManager (rounds, matches, resets)
    Arena/       ArenaBounds (returns escaped items and babies)
    Camera/      ArenaCamera (framing, zoom, shake)
    Audio/       AudioDirector, SoundBank + Synth (procedural music and SFX), GameSettings
    Feedback/    ImpactFeedback (particles, shake, hit-stop, slow-mo, sounds), TimeController
    UI/          GameFlow (menus/pause/results), StartupWarmup (loading screen), HUDController, BabyHudPanel,
                 MenuButtonFx, TitleLogo, DebugOverlay, CommandLineSmokeTest + SmokeTestProfiler (build QA)
    Config/      BabyConfig, ThrowableConfig -> DiaperConfig / ToyConfig, MatchConfig, CpuConfig
  Shaders/Toon.shader      URP cel shader: two-tone ramp, real shadows, SSAO, rim light, accent colour, outlines
  Editor/                  content pipeline (runs in the editor, produces normal Unity assets)
    Meshing/     SDF sculpting (smooth-blended shapes + surface nets), analytic shapes, MeshBuilder
    Art/         BabyFactory, ItemFactory, RoomFactory, UiFactory, TextureFactory, MaterialFactory,
                 IconBaker, RenderSetup (URP + post-processing)
    BabyClashContentBuilder, BabyClashAutoSetup, BabyClashRelease, ScreenshotCapture
  Input/BabyClashControls.inputactions
  Tests/EditMode (rules)   Tests/PlayMode (physics, gameplay, CPU, showcase capture)
  (generated) Balance/  Generated/  Prefabs/  Scenes/
```

Principles:
- **Gameplay never references input, visuals, UI or audio.** `BabyController` exposes `SetMoveInput`, `PressAction`
  and `PressDash`, plus events. The keyboard/gamepad handler and the CPU brain are interchangeable drivers, and
  animation, HUD and feedback only listen.
- **Physics:** thrown items ignore babies' bodies (`Physics.IgnoreCollision`). Hits come from a trigger, pickups from an
  overlap query. So items never get stuck in babies, shoved around, or clip their thrower.
- **Persistent diapers:** exactly two exist, forever. Toys come from a fixed pool (no runtime instantiation).
- **Characters** are one smooth skinned mesh per body, sculpted from blended signed-distance shapes with painted
  vertex colours: skin, blush, a shirt that takes the player colour, a white collar. Faces (eyes, brows, three
  mouths), hair and effects are separate parts. Animation is procedural, so a hand-made model and Animator can
  replace `Visual/` later without touching gameplay.

## Balance

All in `Assets/BabyClash/Balance/`. Edit them in the Inspector, even during Play Mode.

| Asset | Highlights (defaults) |
|---|---|
| **BabyConfig** | move 5.5 m/s · dash 11 m/s, 0.3 s, 0.9 s cooldown, 0.2 s dodge window · shove 6 Cry / 8 m/s · protected defence Cry ×0.5, knockback ×0.6 · diaperless Cry ×1.5, knockback ×1.4 · Max Diapers 1 (2 = carry a spare) · aim assist 25° |
| **DiaperConfig** | 15 m/s throw, arc 3.5 · direct hit 16 Cry / 8 m/s · bounce hit 10 / 5 · soft landing 0.35 |
| **ToyBlock / ToyDuck / ToyBottle** | block 12 Cry heavy-ish · duck 8 Cry, fast, bouncy, squeaks · bottle 14 Cry lob, heaviest push · vanish 2.5 s after landing · spawn weights |
| **MatchConfig** | best of 3 · 2.4 s countdown · 75 s clock · first toy after 3 s, then every 3.5–6.5 s, max 3 in play |
| **CpuConfig** | per difficulty: reaction time, aim error, dodge chance, diaper-throw willingness, shove chance, speed |

Feel knobs live on scene components: `Main Camera → ArenaCamera` (framing/shake), `Game → ImpactFeedback`
(particles, hit-stop, slow-mo), `Game → AudioDirector` (mix), `Baby prefab → Visual → BabyAnimator` (motion).

## Tests

**Window → General → Test Runner**:
- **EditMode, 63 tests**: Cry Meter, damage/knockback, item ownership (throw → hit once → land → opponent picks
  up the same item → throws it back), dash and dodge windows, stun, crying, rounds, the round clock, best-of-3,
  time-up decisions.
- **PlayMode, 19 tests** with real physics: babies start protected; throwing removes protection and the diaper
  lands; it hits the opponent; diaperless babies re-equip and throw back; a wearing baby can't grab a second diaper;
  toys are grabbed without losing the diaper, hit, and vanish; dashing dodges throws; dashing into a baby shoves it
  and knocks its toy loose; diaper-before-toy priority; knockouts end rounds and the next round resets everything;
  first to two wins the match; time-up goes to the calmest baby; escaped items and babies return; the CPU hits,
  re-equips its diaper, and stops when the round is over.
- **ShowcaseCapture** (explicit, run it on purpose) plays the real scene through its menus with two CPUs and saves
  screenshots, including close-ups of animations.

## Building

- In the editor: **Baby Clash → Build macOS App** (universal Intel + Apple Silicon) or **Build Windows App**
  (needs the Windows Build Support module from Unity Hub). Output goes to `Builds/`.
- Command line:
  `Unity -batchmode -quit -projectPath . -executeMethod BabyClash.EditorTools.BabyClashRelease.BuildMacOS -buildPath "Builds/macOS/Baby Clash.app"`
- Smoke test a build: `"Baby Clash.app/Contents/MacOS/Baby Clash" -smoketest /some/folder` plays a CPU-vs-CPU match on
  its own, saves screenshots and a log to the folder, then quits. The log includes frame rate, CPU/GPU frame times,
  garbage collections and every frame slower than 250 ms. Extra flags:
  - `-perfTest` switches off SSAO, MSAA, resolution and shadows one step at a time and logs the cost of each.
  - `-profileMarkers` (needs a development build: add `-development` to the build command line) logs the most
    expensive engine and script profiler markers, overall and for each slow frame.
  - The window must stay visible: macOS stops presenting frames for a hidden window or a sleeping display, and the
    player waits for it.
- **iPad / iPhone**: needs Unity's iOS Build Support module and Xcode. **Baby Clash → Build iPad Xcode Project**, or
  `Unity -batchmode -quit -projectPath . -buildTarget iOS -executeMethod BabyClash.EditorTools.BabyClashRelease.BuildIOS -buildPath Builds/iOS`
  (add `-simulator` for the iOS Simulator). Open `Builds/iOS/Unity-iPhone.xcodeproj` in Xcode, choose your team under
  **Signing & Capabilities** (a free Apple ID works for your own devices; the App Store needs the paid Apple Developer
  Program), plug in the iPad and press Run. Landscape only, iPad and iPhone, iOS 15+.
- **Android tablets and phones**: needs Unity's Android Build Support with the SDK, NDK and OpenJDK.
  **Baby Clash → Build Android APK**, or
  `Unity -batchmode -quit -projectPath . -buildTarget Android -executeMethod BabyClash.EditorTools.BabyClashRelease.BuildAndroid -buildPath Builds/Android/BabyClash.apk`
  (add `-aab` for a Google Play bundle). Install with `adb install Builds/Android/BabyClash.apk`, or copy the file to the
  tablet and open it (allow installs from unknown sources). ARM64, Android 8+, landscape. Signed with Unity's debug key:
  set a release keystore in **Player Settings → Publishing Settings** before uploading to Google Play.
- On tablets the menus scale to the screen's shape (`AspectMatch`), keyboard hints are hidden, the game runs at 60 fps and
  the screen stays awake during matches. Touch buttons react to finger-down events, so even very quick taps count.
- **Windows installer**: after **Build Windows App**, run `Installer/build-installer.sh`. It packages
  `Builds/Windows` into `Builds/Installer/BabyClash-<version>-Setup.exe`, a setup wizard (Welcome → install folder →
  Finish with "Play now" and "Create a desktop shortcut") that installs to `Program Files`, adds a Start menu entry and
  registers an uninstaller under Settings → Apps. The version comes from **Player Settings → Version**. Needs Docker:
  the script runs NSIS and ImageMagick (which draws the wizard art from the generated icons) in a container, so it
  works from macOS. The script and the NSIS source are in `Installer/`.
- **Raspberry Pi Web build**: install Unity's Web Build Support module, then run
  `Unity -batchmode -quit -projectPath . -buildTarget WebGL -executeMethod BabyClash.EditorTools.BabyClashRelease.BuildRaspberryPi`.
  Copy `Installer/play-on-pi.py` into `Builds/RaspberryPi/` and transfer that folder to the Pi. On the Pi desktop,
  open a terminal in the folder and run `python3 play-on-pi.py`. Keep the terminal open while Chromium runs.
  The launcher serves the game on localhost. To run the automated match, add `?smoketest=1` to its URL and
  open Chromium's developer console for the results.
- At startup a short **loading screen** draws every toy, effect and font size once so shaders compile up front
  (`StartupWarmup`). On an older Intel Mac this takes several seconds; afterwards there are no compile stalls.
- Burst ahead-of-time compilation is off for macOS (`ProjectSettings/BurstAotSettings_StandaloneOSX.json`). The game
  has no Burst code of its own, and the Unity 6.6 Intel-Mac editor ships its `llvm-lipo` tool without the executable
  bit, which breaks universal builds. To turn Burst back on, run
  `chmod +x "<Unity.app>/Contents/Resources/Burst/Client/bcl/hostmac/llvm-lipo"` and tick **Project Settings → Burst
  AOT Settings → Enable Burst Compilation** for macOS.

## Known limitations

- Babies differ only in looks (no character traits yet).
- The UI font is Unity's built-in one. A rounder, playful font (e.g. an OFL-licensed one) would suit the game
  better and is a one-line swap in `UiFactory`.
- No rebinding screen, localisation or achievements.
- Online: guests see the match about 0.1 s plus their connection's delay behind (no prediction of their own baby),
  so very laggy connections feel sluggish. Host migration isn't set up: if the host leaves, the room closes.
- Gamepads are assigned by connection order.
- The scene and prefabs are generated: hand edits to them are lost on **Rebuild Game Content**. Balance assets are
  safe. To hand-author content, stop regenerating (or change the factories).
- The Intel-Mac Unity editor is deprecated by Unity after 6.7 LTS; building still produces universal apps.
