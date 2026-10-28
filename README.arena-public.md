# Experimental Arena public-build profile

Based on upstream89d5fd3; AGPL applies to this separate checkout. This is not an Arena MIT package.

Target: installed macOS arm64 engine build23811903, gamev0.107.1, SHA256e7ceb80669bfaf5c8fccabaa126ae2bb283aba514be5b5b55612579cfd285f18. Compile with DefineConstants=STS2_PUBLIC_23811903 and actual Sts2DataDir. Runtime initialization refuses a different engine hash.

The profile maps StartRunLobbyPlayer->LobbyPlayer, RunLobby.PlayerIds->ConnectedPlayerIds, Player.CanUseOrRemovePotions->CanRemovePotions, and the lobby max-player reflection field to its public-build backing field. Original names remain on the default compilation path. Manifests lower min-game-version only with this explicitly constrained profile.

Compilation: .NET9.0.320, 0warnings/0errors. GUI startup and isolated client2026102801 reached the game's load-mod confirmation; Mac lock currently prevents completing it. No runtimehealth/actions/whole-GUI-run claim. Native mod settings use a separate private file, no API credentials. Windows is NOT_TESTED.

Game binaries, SDK and user saves are never committed. Before distributing the modified module, provide this corresponding source and AGPL license. Do not publish experimental binaries as validated releases.
