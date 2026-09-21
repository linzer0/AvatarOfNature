# Avatar of Nature — Boss Duel

[Русская версия](README.ru.md)

Avatar of Nature is a Unity prototype built around a readable boss duel: the player studies the Avatar's intent, survives telegraphed attacks, breaks the arena one sector at a time, and uses the resulting vulnerability window to damage the boss.

The project is currently a focused Windows desktop prototype rather than a finished game. The main goal is to explore encounter readability, positional pressure, and a combat loop where solving the arena matters more than holding the fire button.

## Play the prototype

Download the latest Windows build from the [GitHub Releases](../../releases) page.

1. Extract the release archive to a local folder.
2. Run `AvatarOfNature.exe` from the extracted folder.
3. Choose a difficulty and start the duel.

Controls:

- `WASD` — move
- Mouse — aim
- Left mouse button — shoot

The opening scene is `AvatarBossShowcaseBootstrap`. It presents the difficulty selection and then loads `AvatarBossDuelArena`.

## Encounter loop

- Read the boss telegraph and move to a safe position.
- Use the arena sectors to answer the current attack pattern.
- Build stagger while the boss is protected.
- Collapse a sector to open the duel vulnerability window.
- Shoot the exposed green core before the window closes.
- Reach Phase 2, where timing and attack parameters become more demanding.

The duel includes Earth, Fire, and Shockwave attacks, destructible arena sectors, difficulty profiles, weak points, stagger, phase progression, and a presentation HUD for the current combat state.

## Project structure

- `Assets/AvatarOfNature/Runtime/AvatarBoss/` — boss, arena, attacks, configuration, presentation, and support systems.
- `Assets/AvatarOfNature/Scenes/BossDuel/` — bootstrap, showcase, and arena scenes.
- `Assets/AvatarOfNature/Tests/Editor/` — focused Unity Test Framework coverage.
- `Documentation/BossDuel/` — design notes, implementation checklist, and validation research.

The detailed subsystem map is available in [`Assets/AvatarOfNature/README.md`](Assets/AvatarOfNature/README.md) and [`Documentation/BossDuel/README.md`](Documentation/BossDuel/README.md).

## Build from source

Requirements:

- Unity `6000.3.11f1`
- Windows 64-bit target support

Open the project in Unity and use the scenes listed in `ProjectSettings/EditorBuildSettings.asset`. For a repeatable Windows build from the command line, run:

```powershell
& 'C:\Unity\6000.3.11f1\Editor\Unity.exe' `
  -batchmode -quit -nographics `
  -projectPath 'C:\path\to\AvatarOfNature' `
  -executeMethod AvatarOfNatureBuild.BuildWindows64
```

The helper writes the player to `Builds/AvatarOfNature-Windows64/`. Build output is intentionally ignored by Git and is distributed through GitHub Releases.

## Validation snapshot

The Windows 64-bit build was generated on 2026-09-21 with Unity `6000.3.11f1`:

- Build result: successful
- Build errors: `0`
- Unity-reported build warnings: `488`
- Player smoke launch: engine initialized successfully in headless mode

This is an active prototype. A full human playthrough and final game-feel pass remain separate validation steps from the automated build check.
