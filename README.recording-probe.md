# Local recording probe (not for distribution)

This experimental branch adds diagnostics to the Mac public-build controls. It is not a release recorder and never declares complete operation coverage. No Arena task, identity, scoring or upload lives here.

Build with `-p:DefineConstants=STS2_PUBLIC_23811903%3BSTS2_RECORDING_PROBE` and the actual Mac `Sts2DataDir` as described in README.arena-public.md. Launch only the explicit isolated client, private agent settings and game-control capability. `STS2_RECORDING_PROBE_FILE` must point to a private absolute JSONL destination. Capture a separate file per process; diagnostic sequence numbers are per process. Do not publish logs or game/save artifacts.

Harmony diagnostics observe game synchronizers, actual action-executor events, asynchronous completion and selection callbacks. Call stacks are evidence for controlled UI/API probes, not authenticated origin. Engine-generated actions keep their causal origin separate. These hooks change timing and are not used to qualify a formal run.

`POST /game/control/probe-cancel-selection` is a restricted test adapter using the same isolated capability. It supports only native deck/upgrade grid screens with zero selected cards and exactly one enabled, visible Back button. It emits that button's native Released signal. It accepts no method names, arbitrary node paths or task payload. The response acknowledges dispatch only; verify the selection task and outer operation result separately. A rejected precondition changes nothing. This endpoint does not exist on the release branch.

Use one operator. Do not mix a running script with UI inputs. Natural samples and deliberate cancellation are allowed; no console or save editing. Stop and back up the isolated game before changing probe binaries. Release build and offline-test results are tracked separately from this diagnostic build; the latter has real-game hook checks and does not inherit a full-suite acceptance claim.
