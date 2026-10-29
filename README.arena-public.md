# Experimental Arena public-build profile

Based on upstream89d5fd3; AGPL applies to this separate checkout. This is not an Arena MIT package.

Target: installed macOS arm64 engine build23811903, gamev0.107.1, SHA256e7ceb80669bfaf5c8fccabaa126ae2bb283aba514be5b5b55612579cfd285f18. Compile with DefineConstants=STS2_PUBLIC_23811903 and actual Sts2DataDir. Runtime initialization refuses a different engine hash.

The profile maps StartRunLobbyPlayer->LobbyPlayer, RunLobby.PlayerIds->ConnectedPlayerIds, Player.CanUseOrRemovePotions->CanRemovePotions, and the lobby max-player reflection field to its public-build backing field. Original names remain on the default compilation path. Manifests lower min-game-version only with this explicitly constrained profile.

Compilation: .NET9.0.320, 0warnings/0errors. Follow-up2026-10-29: isolated client2026102801 accepted the actual game mod prompt and loaded the bridge. Healthready with27/27probes; ordinary HTTP gameplay performed an actual strike, then legal end-turn-only policy reached naturaldefeat floor1. Historyschema9 was verified, not abandoned. This is a basic unseeded smoke, not full-GUI victory/CLI-parity/formal-cup acceptance. Native mod settings use a separate private file, no API credentials. Windows is NOT_TESTED.

Game binaries, SDK and user saves are never committed. Before distributing the modified module, provide this corresponding source and AGPL license. Do not publish experimental binaries as validated releases.

## Generic isolated game controls

The previous ArenaPracticeSetup experiment is retired. This branch now exposes only game primitives under /game/control/info, /catalog and /setting. It has no Arena Task type, participant, match selection, scoring, upload, or named tournament profile. The external Node participant decides preparation order and sends individual seed/character/ascension/unlock/discovery settings; ordinary gameplay still uses /action. Only A0/standard is validated by this public-build profile.

Control routes require a 64-hex capability in X-STS2-Game-Control and an explicitly matching isolated --clientId via STS2_GAME_ISOLATED_CLIENT_ID / STS2_GAME_CONTROL_TOKEN. Without these variables the additional controls remain disabled. Generic unlock behavior changes only after an explicit setting, not at startup. Seed setting bypasses the standard UI's intentionally unsupported SeedChanged callback using the verified property setter. Nothing changes user profiles unless the operator deliberately launches that exact isolated client.

game_state_token is an opaque per-process state token, checked in the shared dispatch boundary when supplied. It is not an anti-cheat or authentication claim. info returns the game instance ID and actual game conditions privately; the Node participant filters what reaches a player. The game process does not select seeds or construct competition facts from its HTTP requests. Controlled-response persistence and coverage declarations belong to the independent Node participant.

Mac integration against Arena0f8765d plus the current adapter: platform-issued seed, pure rule player, natural death at floor8,140 acknowledged actions,563 Arena events, schema9 non-abandoned history matched. An intentional platform outage left the entire terminal log local; restarting the same assignment sealed/uploaded it with unchanged Arena/source journal hashes and zero new actions. Public result is controlled-script-only, not complete engine/input coverage. Actual start response-loss and action response-loss tests use private fixtures and never replay uncertain commands. See the corresponding Arena M0 handoff for exact code/artifact fingerprints and later checks.

## MCP turn-handoff validation (2026-10-29)

Native Streamable HTTP was tested with @modelcontextprotocol/client2.0.0 and the installed dsh-mcp-client0.2.0-rc.2, without a model. Initialization, discovery, ordinary actions, error envelopes and a deliberately lost action response were exercised. Native tools remain a developer surface: raw state and raw_state are available and need an external competition filter.

The public-build live test reproduced a false readiness window: EndingPlayerTurnPhaseTwo was true while the next hand contained only two cards, but the previous gate reported ready; the next card reset its 200ms sampler and act rejected the previously advertised action. CombatTurnReadinessPolicy now requires both native turn-handoff phases to be false. The caller passes both flags; a pure regression case checks partial and full hands during handoff and preserves normal empty-hand readiness.

The new case fails before the guard and passes after it. Offline suite: 1032pass/1fail; baseline d196db6:1031pass/1fail. The same StateSplit.MovedBodiesUnchanged source-shape failure is pre-existing; it was not loosened. Use PATH including the private .NET9 SDK to let subprocess tests locate dotnet. Mod build:0warnings/0errors. Live retest:9 consecutive legal end_turn actions after restoring the isolated run reached natural defeat, then MCP continue_game_over verified the schema9 .run (not abandoned). This includes an intentional save/reload for the binary replacement, not uninterrupted-run or crash-recovery acceptance. GUI victory, fixed seed, unlock parity, competition filtering and Windows remain unverified.
