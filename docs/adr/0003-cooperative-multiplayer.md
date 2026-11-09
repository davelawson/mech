# Cooperative multiplayer with a host-authoritative leader

The campaign supports one to three players cooperating as one mercenary company, each commanding one lance. The player count is fixed when a campaign begins, and every player must be present to start or resume it. Single-player is a one-slot campaign on the same code path.

The **leader** (player slot 0) hosts the game and runs the only authoritative campaign and combat simulation. Other players connect by direct IP and port over Godot's `ENetMultiplayerPeer`, carrying typed messages defined in one protocol class rather than node-path RPCs. Clients send commands. The host validates them in arrival order, applies them, and broadcasts state. Clients and host must run the same build: a protocol version plus a build hash is checked on joining. There is no host migration. If the leader disconnects, the others return to the main menu.

Deterministic lockstep was rejected. Combat runs on float math, and the timing of its fixed steps depends on frame-time remainders, so separate machines would drift apart. Host authority avoids desync and keeps the simulation in one place.

## Roles and ownership

- **Leader-only actions:** travelling on the star chart, accepting and abandoning contracts, advancing time, paying or deferring quarterly bills, settling missions, and saving and loading. Company dialogs are shown to everyone; other players see *Waiting for leader* in place of the buttons.
- **Ownership:** a player may act on mechs in their own lance and in the lance pool: assign MechWarriors, refit, repair, sell, unassign, and move mechs between their lance and the pool. They may never act on another player's lance. Spare equipment and company cash are shared and open to everyone.
- **Lances:** each lance holds up to four mechs. A MechWarrior belongs to the lance of their assigned mech.
- **Initial draft:** each player drafts their own lance at the same time.
- **Scaling:** enemy forces and contract pay scale with the number of lances.

## Sessions

- **Lobby:** for a new campaign, the leader picks the player count and port. Joining players enter the address and a name, and claim any free slot. The leader starts once every slot is filled. On resume, slots show the names saved for them, and any player can claim any free slot. A one-player campaign skips the lobby.
- **Saves:** saves live only on the leader's machine and record the player count and slot names. A save is also written automatically when a mission starts and when it ends. There are no saves mid-mission.
- **Strategy-layer sync:** the host broadcasts the full campaign snapshot (the save format, compressed), merged into at most about five updates a second. Joining or reconnecting players receive the full snapshot. Players browse strategy screens independently, and all are pulled into force selection and combat.
- **Disconnects:** a player who quits counts as disconnected.
  - **Outside battle:** time cannot advance until they reconnect. Everything else stays available.
  - **During a battle:** the battle pauses until they reconnect. The leader may stop waiting, which shuts the game down; the campaign then resumes from the mission-start autosave.

## Combat

- **Deployment:** each player selects 1–4 mechs from their own lance and marks Ready. The leader launches once all are ready, and every player must deploy at least one mech.
- **Sync:** the host runs the simulation. Clients send combat commands and interpolate state snapshots sent 10–20 times a second.
- **Orders and vision:** each player orders only their own lance. All players share friendly vision; each has their own camera and selection.
- **Pausing:** each player has a pause toggle, and all start on. The battle runs only while every toggle is off and every player is connected.
- **Other tools:** players can ping the map; there is no in-game chat. The terrain seed, Quick Launch, and the results screen's Continue button belong to the leader.

## Delivery

1. Lances and the command layer in single-player, with no networking.
2. Lobby, connection and strategy-layer sync.
3. Combat networking: commands, snapshots, pause toggles, pings and lance scaling.
4. Disconnect and reconnect handling everywhere.

A `multiplayer` test group will run a host and clients as headless processes on localhost, with engine-free checks for the protocol and commands.
