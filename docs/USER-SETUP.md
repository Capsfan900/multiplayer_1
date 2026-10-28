# What needs your account or another computer

This is the user-dependent checklist for the active MVP. Development continues on
everything independent of these inputs. It does not claim the multiplayer is already
implemented or tested. Current build/test evidence belongs in [HANDOFF](HANDOFF.md)
and [MVP progress](MVP-PROGRESS.md).

Setup rechecked on 2026-10-27 for the active checkout
`C:\Users\tyler\orca\workspaces\multiplayer_1\mvp-2`. The user-provided Fusion
2.1.3 Stable build 2390 SDK is imported and Unity compilation passes. The user supplied
the Fusion App ID; it is saved in the ignored local
`Assets/Photon/Fusion/Resources/PhotonAppSettings.asset` and persists after reload.
No further SDK download or App ID input is needed in this checkout. See
`Artifacts/Resume/photon-setup.md`; live service validation, verified player identity
and internet acceptance remain open.

## Unity Editor setup

Unity **6000.5.10f1** licensing, import, native tests, map baking and the offline
Windows build now pass. No new approval or licensing action is needed. Exact
commands and results are in [QA](QA.md). Use this active checkout, never the
source project. Managed rule tests do
not replace native checks.

## 1. Create the free Photon Fusion application

The user supplied this app's App ID and it is configured locally. Do not create a
duplicate. The steps below remain the reference for recreating local settings;
the App ID belongs under the app, not the dashboard's API Tokens section.

1. Open the [Photon dashboard](https://dashboard.photonengine.com/) and register/sign
   in using your own email. Complete any email verification yourself; do not share
   your account password.
2. Select **YOUR > APPS > Development**, then **Create a New App**. Select **Fusion** for the Photon SDK and **Fusion 2**
   for SDK version. Name the application **Wildstone** (the working game name), then
   create it.
3. Copy the application's **App ID**. This is the application identifier, not your
   password or a service administrator secret. Give it to the assistant privately
   when resuming setup; avoid posting it publicly.
4. Stay on the free development allowance for initial tests. Photon currently
   describes 20 concurrent users free in development, enough capacity for one
   eight-player match. The free launch allowance is 100 concurrent users for one
   game app per customer. Traffic limits also apply; no paid upgrade is required
   just to begin these tests.

Verified instructions: [Fusion App ID setup](https://doc.photonengine.com/fusion/v2/getting-started/appid-instructions),
[Fusion Host Mode setup](https://doc.photonengine.com/fusion/v2/tutorials/host-mode-basics/1-getting-started),
[Photon getting started](https://doc.photonengine.com/photon/current/getting-started),
[pricing and traffic limits](https://www.photonengine.com/fusion/pricing).

## 2. Download the legitimate SDK while signed in

Completed on 2026-10-27: the user's package was found at the original project's
root and copied without changing that source into the destination below. The
archive contains Fusion 2.1.3 build 2390 release history; source and copy hashes
match. Native import and a clean compile now pass after enabling the built-in Physics
2D dependency required by Fusion. The download instructions below are retained for
reproducibility; another download is unnecessary.

The official download returned a sign-in requirement during unattended setup.
Use the [Fusion SDK page](https://doc.photonengine.com/fusion/v2/getting-started/sdk-download)
and download **Fusion 2.1.3 Stable, build 2390**, the version selected for compatibility
testing. Save the `.unitypackage` into:

`C:\Users\tyler\orca\workspaces\multiplayer_1\mvp-2\Downloads\`

The official package is named `photon-fusion-2.1.3-stable-2390.unitypackage`.
If that exact version is no longer available, retain the downloaded filename and
tell the assistant; do not silently substitute a different version or project sample.
No need to import it manually while development is running. The assistant can verify
its identity, import it into this destination project, connect the App ID and run
the real compatibility/network tests. Never import into the original `vibegame1`.
Keep the source project at `C:\Users\tyler\Main Storage\multiplayer_1` untouched.
After import, Photon documents entering the App ID through Fusion Hub or
`PhotonAppSettings > AppSettings > App Id Fusion`; do this only in the active
checkout. Import and a configured App ID alone do not implement the missing online
adapters or establish internet compatibility.

## 3. Identity/profile configuration

The foundation requires verified, recoverable player identity for secure reconnect.
Photon room codes alone are not account authentication. The initial provider client
now implements recoverable PlayFab email login/registration/recovery and Photon-token
acquisition. Its current Development Mode allows no-charge, low-volume evaluation
with up to **1,000 lifetime player accounts per title** and ten development titles.
This fits initial friend testing, but its terms exclude high-volume load testing;
production and endurance-test eligibility must be resolved separately. Foundation
Mode requires an Xbox publishing commitment and is not assumed here.
[Pricing](https://developer.microsoft.com/en-us/games/products/playfab/pricing/),
[service modes](https://developer.microsoft.com/en-us/games/products/playfab/terms/).

The user requested a dashboard walkthrough; Title ID and setup completion have not
yet been supplied. No account request was made by the development tools and no paid
plan was selected. When proceeding,
create a development title through your own Microsoft sign-in and provide its Title
ID. Associate the Fusion App ID with that title's Photon add-on; in Photon configure
the title's `/photon/authenticate` URL and disable anonymous connections. The client
must obtain a Photon authentication token after recoverable player sign-in; a local
device/custom ID is insufficient for this project's identity requirement. Keep
passwords, session tickets and tokens out of files/logs, and never put a provider
administrator secret in a build. Exact configuration still needs live verification.
[Official Fusion/PlayFab integration](https://doc.photonengine.com/fusion/v2/manual/connection-and-matchmaking/authentication/playfab),
[PlayFab Photon token API](https://learn.microsoft.com/en-us/rest/api/playfab/client/authentication/get-photon-authentication-token?view=playfab-rest).

The Photon-side URL, anonymous-client setting and token flow above were rechecked
against the official integration page on 2026-10-27; dashboard/title configuration
remains unverified. Its sample uses a device Custom ID and logs the token: do not
copy those parts into this project. Retain recoverable login and omit token logging.

## 4. Real internet and feel checks

After an actual online build is ready, use at least two computers on independent
home networks for the first external test. Host, share the private room code, join
as another team, finish a match, reconnect and rotate the host. Then test graceful
host departure, host crash and a relay-only connection path. The assistant must
record actual results and failures instead of inferring these from local clients.

Final load/performance acceptance also needs representative separate host/client
machines for eight players, defined hardware/uplink, the canonical network profiles
and endurance checks. Local automated clients are useful functional checks only.
Your own movement, weapon feel, readability and menu feedback is a final quality
input; automated tests cannot supply that judgment.

## Resume message

> Continue Wildstone in `C:\Users\tyler\orca\workspaces\multiplayer_1\mvp-2`. I created the
> free Photon Fusion 2 app and downloaded the requested SDK into `Downloads`.
> Read `docs/HANDOFF.md`, `docs/MVP-PROGRESS.md` and `docs/USER-SETUP.md`, then complete
> the real SDK/service integration and remaining tests without redoing completed work.

The current handoff will list the precise build, completed work, unresolved defects
and remaining user-dependent steps when unattended work stops.
