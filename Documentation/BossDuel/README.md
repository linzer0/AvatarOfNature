# Boss Duel documentation

- [`GAME_DESIGN.md`](GAME_DESIGN.md) — intended player experience and combat loop.
- [`TZ_CHECKLIST.md`](TZ_CHECKLIST.md) — implementation checklist and current evidence.

The product map starts at [`Assets/AvatarOfNature/README.md`](../../Assets/AvatarOfNature/README.md).

The runtime entry point is `AvatarBossShowcaseBootstrap`, followed by
`AvatarBossDuelArena`. Gameplay code is grouped under
`Assets/AvatarOfNature/Runtime/AvatarBoss/`; focused Unity Test Framework
coverage is under `Assets/AvatarOfNature/Tests/Editor/`.
