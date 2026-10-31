# Unified public-build bridge profile

Based on upstream89d5fd3; AGPL applies to this separate checkout. This is not an Arena MIT package.

Target: installed macOS arm64 engine build23811903, gamev0.107.1, SHA256e7ceb80669bfaf5c8fccabaa126ae2bb283aba514be5b5b55612579cfd285f18. Compile with DefineConstants=STS2_PUBLIC_23811903 and actual Sts2DataDir. Runtime initialization checks an OS/architecture hash table and refuses a different engine hash. Windows x64 has an explicit empty entry and refuses initialization until a collaborator measures and reviews the real Windows engine hash. The Mac hash must never be reused for Windows.

The profile maps StartRunLobbyPlayer->LobbyPlayer, RunLobby.PlayerIds->ConnectedPlayerIds, Player.CanUseOrRemovePotions->CanRemovePotions, and the lobby max-player reflection field to its public-build backing field. Original names remain on the default compilation path. Manifests lower min-game-version only with this explicitly constrained profile.

Compilation: .NET9.0.320, 0warnings/0errors. Follow-up2026-10-29: isolated client2026102801 accepted the actual game mod prompt and loaded the bridge. Healthready with27/27probes; ordinary HTTP gameplay performed an actual strike, then legal end-turn-only policy reached naturaldefeat floor1. Historyschema9 was verified, not abandoned. This is a basic unseeded smoke, not full-GUI victory/CLI-parity/formal-cup acceptance. Native mod settings use a separate private file, no API credentials. Windows is NOT_TESTED.

Game binaries, SDK and user saves are never committed. Before distributing the modified module, provide this corresponding source and AGPL license. Do not publish experimental binaries as validated releases.

## Generic isolated game controls

The previous ArenaPracticeSetup experiment is retired. This branch now exposes only game primitives under /game/control/info, /catalog and /setting. It has no Arena Task type, participant, match selection, scoring, upload, or named tournament profile. The external Node participant decides preparation order and sends individual seed/character/ascension/unlock/discovery settings; ordinary gameplay still uses /action. The current profile uses native single-player Custom, A0 and no modifiers (A27). Standard seed-setting is no longer supported.

Control routes require a 64-hex capability in X-STS2-Game-Control and an explicitly matching isolated --clientId via STS2_GAME_ISOLATED_CLIENT_ID / STS2_GAME_CONTROL_TOKEN. Without these variables the additional controls remain disabled. Generic unlock behavior changes only after an explicit setting, not at startup. Seed setting uses Custom's native SetSeed/SeedChanged path; no standard-mode setter bypass is used. Nothing changes user profiles unless the operator deliberately launches that exact isolated client.

game_state_token is an opaque per-process state token, checked in the shared dispatch boundary when supplied. It is not an anti-cheat or authentication claim. info returns the game instance ID and actual game conditions privately; the Node participant filters what reaches a player. The game process does not select seeds or construct competition facts from its HTTP requests. Controlled-response persistence and coverage declarations belong to the independent Node participant.

Mac integration against Arena0f8765d plus the current adapter: platform-issued seed, pure rule player, natural death at floor8,140 acknowledged actions,563 Arena events, schema9 non-abandoned history matched. An intentional platform outage left the entire terminal log local; restarting the same assignment sealed/uploaded it with unchanged Arena/source journal hashes and zero new actions. Public result is controlled-script-only, not complete engine/input coverage. Actual start response-loss and action response-loss tests use private fixtures and never replay uncertain commands. See the corresponding Arena M0 handoff for exact code/artifact fingerprints and later checks.

## MCP turn-handoff validation (2026-10-29)

Native Streamable HTTP was tested with @modelcontextprotocol/client2.0.0 and the installed dsh-mcp-client0.2.0-rc.2, without a model. Initialization, discovery, ordinary actions, error envelopes and a deliberately lost action response were exercised. Native tools remain a developer surface: raw state and raw_state are available and need an external competition filter.

The public-build live test reproduced a false readiness window: EndingPlayerTurnPhaseTwo was true while the next hand contained only two cards, but the previous gate reported ready; the next card reset its 200ms sampler and act rejected the previously advertised action. CombatTurnReadinessPolicy now requires both native turn-handoff phases to be false. The caller passes both flags; a pure regression case checks partial and full hands during handoff and preserves normal empty-hand readiness.

The new case fails before the guard and passes after it. Offline suite: 1032pass/1fail; baseline d196db6:1031pass/1fail. The same StateSplit.MovedBodiesUnchanged source-shape failure is pre-existing; it was not loosened. Use PATH including the private .NET9 SDK to let subprocess tests locate dotnet. Mod build:0warnings/0errors. Live retest:9 consecutive legal end_turn actions after restoring the isolated run reached natural defeat, then MCP continue_game_over verified the schema9 .run (not abandoned). This includes an intentional save/reload for the binary replacement, not uninterrupted-run or crash-recovery acceptance. GUI victory, fixed seed, unlock parity, competition filtering and Windows remain unverified.

## Native Custom controls (M0-R2)

`POST /game/control/open-custom` opens the native single-player Custom screen, or returns the already-open lobby. `setting` accepts the existing individual character/ascension/seed/unlock/discovery primitives only in that lobby. `/info` returns actual mode=custom, conditions, game instance and Custom unlock state. The ordinary raw `/state` remains the upstream gameplay surface; menu preparation uses the privileged `/info` screen field.

`has_saved_run` reads SaveManager's actual single-player and multiplayer save flags for the isolated profile. It does not infer save presence from the currently visible Continue action: that action is absent on Custom even when a saved run exists. The participant therefore refuses preparation without changing settings in either menu.

`POST /game/control/start` takes only `{command_id}` (1–128 ASCII letters/digits/underscore/hyphen). The mod flushes a private intent before emitting the native confirm-button signal; successful start conditions are saved to a separate receipt. `GET /game/control/start?command_id=...` reads it. A known ID never emits another start, including unknown outcomes and process restarts. A new process cannot claim an old receipt. Corrupt receipts fail closed. The Node participant owns task/condition matching and recovery policy.

Custom epoch unlock is an explicit operator step: launch the isolated client with `STS2_GAME_ALLOW_EPOCH_UNLOCK=1`, remain on the main menu, then POST `/game/control/unlock-custom` with the same capability. It requires an existing isolated progress file; creates and flushes the non-overwriting `progress.save.before-custom` backup before calling the game's ObtainEpochOverride and SaveProgressFile. A failed backup prevents mutation. No startup auto-unlock and no console/save editing. Stop the isolated game to restore that backup to progress.save, preserving the backup; never apply it to a personal profile. Remove the opt-in on subsequent launches.

Game command files live under the isolated profile’s `game-control-commands` directory, with private permissions. This is a generic game receipt store, not a tournament journal or complete operation recorder. Offline tests cover reconstruction/no-repeat, malformed results and path-safe command IDs. Recording diagnostics are developed only on a separate local experiment branch and are not part of this release branch.

## Persisted native HTTP/MCP port (M1-G)

Settings now accept nullable `apiPort` (1..65535), shared by HTTP and native `/mcp`. Valid `STS2_API_PORT` retains priority and its existing no-fallback default unless `STS2_API_ALLOW_FALLBACK` opts in. Without a valid environment port, `apiPort` is preferred and ordinary automatic fallback remains enabled. Missing/invalid persisted values retain the original8080 behavior. Restart the game after changing it. This is separate from legacy Python `mcpPort` (8765); no Arena or tournament endpoints were added. The player kit writes19080. `InstanceEndpointFile` still reports the actual bound port.

Offline protection: `Settings.ApiPort` round-trips JSON, validates range and precedence; `HttpServerPort.*` keeps explicit-environment fallback guards. Build and test evidence is recorded in the Arena M1-G2026-10-30 report; existing StateSplit fingerprint failure is not relaxed. Windows branch is unchanged pending M1-A.

The occupied-port Mac smoke additionally exposed HttpListener errno48 (EADDRINUSE). It is now included in the existing unavailable-port classification, so persisted-port collisions use the existing fallback loop. Explicit environment ports still never drift without opt-in. LoopbackListener.MacAddressInUse protects this case.


## Unified branch: arena/public-23811903 (M1-A, 2026-10-30)

Based on Mac d5663ef, not a merge of the Windows competition branch. Both old branches are unchanged. Retains the four `STS2_PUBLIC_23811903` conditional interface mappings; default upstream compilation remains separate. Build against each platform's actual engine assemblies. Do not call a Mac build a Windows build.

Integrated Windows generic fixes: c42db74 snapshots the old event page before dispatch; 286619b requires native PlayerTurnPhase.Play, alongside Mac 4ae9f7d's handoff guard and existing stable-action sampling. From c4cb81a only the generic play-card choice boundary is extracted: an open selection is not settled, cannot trigger hang-recovery cancellation, and cannot roll back the accepted play counter. Selection continues through existing ordinary actions; a pending parent is not proof of completion. No Arena selection tracker, continuation endpoint, permits, bindings or fact logs are included.

### Supported bridge contract vs retained upstream modules

| Our bridge contract | Entry / boundary |
| --- | --- |
| Health, state, ordinary action/result, action discovery, SSE/data | `/health`, `/state`, `/action`, `/actions/available`, `/events/stream`, `/data/*`; retain unknown/pending semantics |
| Native MCP | `/mcp`, enabled only through explicit mcpEnabled or `/mcp/control`; no Python proxy needed |
| Preferred port + collision fallback | persisted apiPort; absent setting keeps 8080 legacy selection, explicit environment port precedence/strictness unchanged |
| Isolated generic opening/settings | only `/game/control/*`; matching isolated clientId and 64-hex capability required; no Arena task/score/upload |
| Public engine guard | `PublicBuildProfile`: Mac arm64 validated; Windows x64 missing hash refuses before initialization |

Upstream game Agent/autoplay, Jev, model settings, overlay, dual-instance companion, Python sidecar MCP, screenshot and strategy surfaces remain in source. They are **outside our supported bridge interface** and do not start models, companions or screenshot work automatically. Empty settings have no models, proactive chat and dual-layer play off; native MCP itself also needs explicit opt-in. In this public build the UI/first-run guide is not installed unless the Host explicitly sets `STS2_PUBLIC_ENABLE_UPSTREAM_UI=1`. Python sidecar requires separately launching Python. Existing loopback screenshot/strategy/AI HTTP handlers remain callable on explicit request; this is not endpoint removal or a new authentication promise. Do not treat the retained modules as approved competition tools.

### Validation and limits

Release build: `dotnet build STS2AIAgent/STS2AIAgent.csproj -c Release -p:DefineConstants=STS2_PUBLIC_23811903 -p:Sts2DataDir=<actual platform engine directory>`.
Offline executable suite: put the private .NET 9 SDK on PATH, then `dotnet run --project STS2AIAgent.Tests -c Release` (`dotnet test` alone does not run this executable suite). Retains both platforms' generic regression cases; Windows port errors 5/32/183/10013/10048 are fixtures on Mac. Existing `StateSplit.MovedBodiesUnchanged` stays failing with its original fingerprints; checks are not weakened. Build success is separate from this known suite failure and from runtime acceptance. Mac live evidence and Windows migration checklist are maintained in Arena's M1-A delivery documents. Windows on-device and cross-platform same-seed/full-run parity remain unverified. No native binaries are published by this branch task.

Offline run for this slice: 1043 PASS / 1 existing StateSplit failure; Release 0 warnings / 0 errors against the actual Mac engine. `StateSplit` test source and fingerprint table are byte-identical to d5663ef; its existing public-compatibility differences are not hidden, and the new Play-phase change also remains visible to that check. The previously passing Mac handoff case plus the original Windows event/native-Play cases pass. Windows fixture success is not Windows runtime acceptance.

## Native MCP nested choices (2026-10-31)

A real ARMAMENTS play exposed a transport receipt error: the game dispatcher returned pending while CARD_SELECTION was open, but native MCP promoted it to completed because the selection itself was actionable. Native MCP now preserves the pending parent receipt and does not wait again when the dispatch snapshot already contains that choice. It also keeps pending if a choice appears during the normal transition wait. This adds no continuation endpoint or Arena type. Use the existing select_deck_card and, only when offered, confirm_selection; the single-card combat upgrade screen confirms natively on selection. Subsequent game observation proves the effect; do not replay the parent card. Regression tests exercise the actual MCP tool boundary with both immediate and delayed choices and retain ordinary pending-to-completed behavior. Mac on-device evidence and final file identities are handed off in Arena's 2026-10-31 bridge closeout report, separate from catalog approval.
