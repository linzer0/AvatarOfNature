# AvatarBoss script layout

The boss prototype is split by responsibility so a reviewer can follow the
gameplay loop without scanning one large folder.

- `Core/` — boss contract, intent, duel resolution, phases, difficulty and stagger.
- `Arena/` — arena controller and destructible sector state.
- `Attacks/` — concrete Fire, Earth and Shockwave attack implementations.
- `Presentation/` — HUD, bootstrap flow, weak points, VFX, audio and support presentation systems.
- `Support/` — runtime support mechanics such as summons and healing orbs.
- `Testing/` — deterministic runtime agents, assertions, reports and debug-only harnesses. It is compiled as the separate `fps.AvatarBoss.Testing` assembly.
- `EditorTests/` — Unity Test Framework tests; kept separate from runtime code.

The `fps.AvatarBoss` assembly definition remains at this folder root. Moving a
script within the assembly does not change its Unity type or serialized GUID.
