# The Room: documentation

The Room is a fast knife-fight deathmatch in one room, built by the **Voidra** team with Godot
4.7 (.NET/C#). Players meet in rooms hosted on a dedicated server, fight with light and heavy
attacks, parries and one ability each, and every finished match lands in a shared history and
season leaderboard.

These docs explain how the project works and how to change it. They're kept in sync with the
code: see "Keeping docs current" below.

## Sections

| Section | Read it when you want to… |
|---|---|
| [Getting started](getting-started.md) | install, build, run, and learn the controls and CLI flags |
| [Architecture](architecture/Intro.md) | understand the moving parts |
| ├ [Networking](architecture/networking.md) | server authority, prediction, interpolation, lag compensation, RPC patterns |
| ├ [Sessions & scenes](architecture/sessions-and-scenes.md) | menu → room → menu, practice, how a join connects |
| └ [Match loop](architecture/match-loop.md) | scoring, bounty, Golden Knife, Last Call, results, reporting |
| [Gameplay](gameplay/Intro.md) | the rules players experience |
| ├ [Combat](gameplay/combat.md) | light, heavy, parry, execute, dash, jump |
| ├ [Abilities](gameplay/abilities.md) | the ability grammar and adding an ability |
| └ [Tuning](gameplay/tuning.md) | where every gameplay number lives and how to change it |
| [Characters & animation](characters/Intro.md) | the character pipeline |
| ├ [Adding a character](characters/adding-a-character.md) | a new model, step by step |
| └ [Animation pipeline](characters/animation.md) | humanoid retargeting and adding clips |
| [Lobby service](lobby/Intro.md) | rooms, match history and leaderboard |
| ├ [HTTP API](lobby/api.md) | every endpoint, with examples |
| ├ [Rooms](lobby/rooms.md) | room lifecycle, limits, the main room |
| └ [Match history & leaderboard](lobby/match-history.md) | storage, ranking rules, pagination |
| [Menus (UI)](ui.md) | main menu, in-game menu, theme |
| [Building the game](building.md) | making the macOS app and Windows client (and sharing them) |
| [Testing](testing.md) | the test suites, bots, and checking visuals by rendering |
| [Deployment](deployment.md) | updating the live server we already run |
| [Server configuration](server-config.md) | installing everything on a **new** server from scratch |

Also useful:

- [`plan/main.md`](../plan/main.md): the phased build plan, status and change log.
- The `CLAUDE.md` files at the root and in major folders: project rules and code style.

## Keeping docs current

Docs are part of the change, not an afterthought. Any change to behaviour, commands, flags, API
routes, configuration, deploy steps or file layout updates the affected page in the **same
commit**. A new area gets a new page linked from here. Claude follows this through the root
`CLAUDE.md`; people should too.
