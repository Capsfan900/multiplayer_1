# Wildstone menus and game flow

User requirement: minimalist, good-looking menus matching the soft modern FPS.
This is the UI owner; simulation/security/performance remain in the foundation,
and weapons/match rules remain in the combat spec. Menus, local settings and
offline Practice are authored. Photon-only host/join,
team/ready/load, pause/results and retained-session reconnect are composed with
the networking adapter. No account sign-in is required (D023). Missing Photon app configuration keeps online
actions unavailable; guest reconnect lasts only while the game remains open.
Packaged keyboard interaction, moving visual/feel and external-network acceptance remain open;
[QA](../../QA.md) owns current local service evidence.

## Presentation

Use warm off-white text/panels, graphite accents and restrained team/objective color.
Large deliberate headings, compact secondary labels, generous spacing and a small
number of strong actions should make the interface feel finished. Render a quiet
arena view behind the title and lobby; do not use concept art as fake gameplay.
Subtle short fades/hover transitions are permitted; no mandatory animated intro,
motion blur or busy glass/neon treatment. Use Unity UI Toolkit with a small shared
stylesheet and state controller, not a new UI framework.

Scale from 1280x720 through 1080p/1440p/ultrawide. Keep text readable at low graphics
settings. Keyboard focus is visible, Escape consistently backs out/cancels, and input
never stays trapped after leaving a session. Team symbols accompany team colors.
Editable slider values and dropdown selections use ivory text on graphite fields,
with visible hover and keyboard-focus borders. `ArenaMenu.uss` owns these native
UI Toolkit input styles. The menu scrollbar uses a restrained graphite track,
ivory thumb and visible focus border. Static packaged captures now verify Settings
contrast and layout; [QA](../../QA.md) records the images and interaction limits.
Transitions must be interruptible; reduce-motion preference disables decorative motion.
Same-screen status and roster refreshes preserve the focused logical control,
including focus inside a text field. A new Settings screen focuses sensitivity
before the controls list. Scheduled focus belongs to the card that created it;
an old detached card must not focus a replacement screen.

Keep the HUD tree for the same actual match through pause/settings/resume and
replace it when the match changes. Hit feedback starts hidden until a hit event.
The crosshair is centered in the viewport; HUD text wraps within its available
width instead of overflowing at larger UI scales. Packaged off-screen captures
cover 720p, maximum UI scale, 1080p and an ultrawide title; this does not replace
moving readability, keyboard/mouse interaction or human feel acceptance.

## Screens and transitions

| Screen | Primary content and behavior |
| --- | --- |
| Title | Game identity, Play, Practice, Settings, Quit; current connection state quietly visible |
| Online play | Host and Join actions; join code with validation and region where relevant; no account sign-in or profile setup |
| Host setup | Map preview/name, private room creation, concise retry on service error |
| Lobby | Room code/copy, four two-slot team cards with symbols, ready state, host map selection and start; explain unmet two-team minimum |
| Loading/connecting | Bounded progress/state and cancel; all paths release old runners and ignore stale callbacks |
| Playing | Small crosshair, health/shield, ammo/slot, grenade count, Blink/shield charges, four-team scores, match timer and zone status/direction |
| Pause | Resume, settings, controls, leave; local menu never pauses network simulation |
| Recovery | Honest reconnect/migration state and countdown; input/scoring behavior belongs to foundation; failed recovery offers retry/back |
| Results | Win/draw/abort distinction, team totals, rematch/back; no invented persistent ranking or trusted progression |

Loading Cancel, Escape and failure Back must leave an active network session even
before gameplay attaches. Clear the exposed session immediately on leave so a late
load cannot acknowledge or spawn. An online loading failure must not offer an offline
retry while that session is active.

Automatic recovery takes the foreground instead of leaving a frozen gameplay HUD.
Show the ceiling of the remaining seconds from the session's original recovery
deadline; presentation never starts or extends that deadline. Hide stale lobby
team/ready/start actions and suppress a second reconnect while recovery is pending.
Leave, Back and Practice still cancel the active session. Once recovery completes,
resume only an attached current match with an active session; cancellation must
never reopen the retired gameplay presentation. Focused regressions are authored
in `MenuPresentationTests` and `OnlineMenuTests`; native results belong in QA.

Practice is explicitly offline, useful for validating movement, maps and weapons.
It may have target fixtures but no claimed online players or gameplay bots. An absent
SDK/service configuration must not make online buttons silently launch practice.
Developer provisioning instructions live outside ordinary player menus; a missing
configuration gives a concise unavailable state and a useful local log.

## Local settings

Mouse sensitivity, FOV, master/effects volume, resolution/fullscreen, VSync/FPS
cap, quality preset, UI scale, reduced motion and input bindings. Clamp finite ranges,
store in this project's namespace, apply/cancel predictably and restore defaults.
Never persist authority, health/ammo, provider passwords or login tokens as settings.
Show the current binding when prompts are used; releasing focus clears held movement.
Practice input latches action press callbacks until render sampling, preserving a
complete tap across input updates for one simulation tick. Clearing input discards
pending presses and combat actions; repeated render samples do not repeat an edge.
Native regressions live in `PracticeInputTests`: all three pass in the 2026-10-27
native PlayMode run `PlayMode-20261027-195621-403.xml` (62/62 overall). The fixture
explicitly routes synthetic keyboard input to the unfocused batch player and
restores the previous input settings; this is local input evidence, not online proof.
Only expose a music volume control when the game has music. The initial authored
content has effects cues only; a stored music preference is not a working control.

## QA

Exercise every screen in a packaged Windows build: keyboard/mouse focus, resizing,
invalid/empty code, failed provider request, cancellation, loading failure, reconnect,
leave/rematch and reset settings. Check screenshots for overflow/contrast and moving
captures for disruptive transitions. UI polish is not proved by compilation.
