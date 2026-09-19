# AvatarOfNature — Boss Duel

This folder is the product-facing entry point for the AvatarOfNature prototype.
The surrounding top-level folders contain the FPS foundation, imported content,
Unity packages, and rendering dependencies.

## Start here

1. Open `Scenes/BossDuel/AvatarBossShowcaseBootstrap.unity`.
2. Press Play and choose a difficulty.
3. The bootstrap scene loads `AvatarBossDuelArena`.

The same scene is the first enabled scene in `ProjectSettings/EditorBuildSettings.asset`.

## Folder map

- `Scenes/BossDuel/` — entry, showcase, and duel arena scenes.
- `Runtime/AvatarBoss/` — runtime boss gameplay code.
- `Runtime/AvatarBoss/Configuration/` — the ScriptableObject config type.
- `Config/Resources/` — `AvatarBossEncounterConfig.asset`, the balance source.
- `Tests/Runtime/` — deterministic runtime agents, assertions, reports, and debug harnesses.
- `Tests/Editor/` — focused Unity Test Framework tests.

## Project boundaries

- `FPS/` is the FPS foundation and shared gameplay framework.
- `ModAssets/` contains shared imported game content.
- `Plugins/`, `ThirdParty/NavMeshComponents/`, `Rendering/`, and `TextMesh Pro/` are technical dependencies.
- `Documentation/BossDuel/` contains design notes, the implementation checklist, and validation evidence.

Runtime type names, namespaces, assembly names, and scene names intentionally
remain unchanged during this organization pass.
