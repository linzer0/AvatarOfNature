# Avatar of Nature — Boss Encounter Research Brief

Дата разбора: 2026-09-21  
Репозиторий: `C:\Users\Linar\Projects\Github\AvatarOfNature`

## Executive summary

Boss encounter развивался от базового AvatarBoss-прототипа с HP, stagger и scheduler до дуэли намерений:

```text
intent
→ telegraph
→ player decision
→ arena consequence
→ opening / weak point
→ recovery or next phase
```

Главная идея: игрок не должен просто постоянно стрелять в health bar. Он читает выбранную боссом область давления, переживает атаку или заманивает её в нужный сектор, разрушает часть арены и получает короткое окно уязвимости.

В текущей реализации присутствуют:

- неподвижный босс в центре арены;
- круговая арена из секторов и колец;
- deterministic intent controller;
- Earth, Fire и Shockwave attacks;
- геометрические telegraphs;
- разрушение секторов;
- stagger и vulnerability windows;
- Phase 2;
- WeakPointA и WeakPointB;
- difficulty profiles;
- debug panel;
- EditorTests и runtime regression agent.

При этом полноценный human playtest, финальное standalone-прохождение и доказательство того, что игрок самостоятельно понял механику baiting, в репозитории не подтверждены.

## Original intent

Исходная идея формально зафиксирована в commit `60f567b` в историческом документе `BOSS_DUEL_GAME_DESIGN.md`.

Encounter задумывался не как health sponge, а как противник, которого нужно понять и переиграть:

- босс выбирает область давления;
- игрок читает намерение;
- атака босса может разрушить его собственную защиту;
- разрушение арены меняет доступные решения;
- успешное решение открывает ограниченное окно уязвимости.

Исторический дизайн описывает три защитных узла и более сложную систему из трёх фаз. В текущем коде надёжно подтверждены две фазы и два weak point-объекта; полноценная схема «три узла → финальное ядро» не подтверждается.

## Development timeline

| Этап | Источник | Результат |
|---|---|---|
| `266c55e`, 2026-09-14 | `AvatarBossController`, scheduler, Earth, Shockwave, Stagger, WeakPoint | Создан базовый boss prototype |
| `d6dfdb4` | `AvatarBossPhaseController`, Fire attack | Добавлены Phase 2, Meteor Rain, combo и phase-specific weak point rule |
| `61c5fc1` | `AvatarBossGameplayTestAgent`, assertions, EditorTests | Появился автоматизированный test harness |
| `3bcc8d7` | `AvatarBossArenaController`, `AvatarBossArenaSector`, intent и duel controller | Encounter превращён в arena puzzle |
| `e3627dd` | `AvatarBossDuelArena.unity` | Создана отдельная сцена арены |
| `082199b` | arena controller и сцена | Арена расширена до трёх колец |
| `7442c0f` | arena sector, intent, duel controller | Сектора получили durability |
| `862a0ff` | Shockwave, Earth, Fire, player controller | Shockwave стал позиционной push threat |
| `4c079f0` | attacks и arena | Point decals заменены cell-native targeting |
| `73af8dd` | arena, HUD, Shockwave, player controller | Улучшены feedback и shockwave physics |
| `20e6d8f`, `054bb8e` | `AvatarBossVisualEffects` | Добавлены читаемые attack poses и silhouette cleanup |
| `92aa658`, `c98354b`, `dfe9a42`, `2e0b63f`, `bdc305b` | runtime structure | Код разделён по ответственности, зависимостям и testing assembly |
| `b8ce9e8` | `Assets/AvatarOfNature/` | Проект организован в reviewer-facing структуру |
| `a35d213` | configs, editors, difficulty profiles | Подготовлен boss duel candidate |
| `1521d6a` | balance/config/controller | Выполнен последний закоммиченный balance pass |

## Key iterations

### От scheduler к intent

Первый прототип выбирал и запускал атаки через scheduler. Позже появился отдельный `AvatarBossIntentController`.

Intent содержит:

- `AttackType`;
- `TargetSector`;
- `TargetDirection`;
- `Reason`;
- `TelegraphPhase`;
- `ExpectedResult`;
- `SequenceNumber`.

Источник: `Assets/AvatarOfNature/Runtime/AvatarBoss/Core/AvatarBossIntent.cs` и `AvatarBossIntentController.cs`.

Это отделило вопрос «какая атака запускается» от вопроса «зачем босс её запускает».

### Ограниченная память вместо полностью случайных атак

`AvatarBossIntentController` использует:

- фиксированный seed;
- `System.Random`;
- память последних атак;
- память недавно разрушенных секторов;
- последнюю позицию игрока.

При наличии арены primary target выбирается из сектора, где находится игрок во время выбора intent. Повторение одной атаки ограничивается очередью недавних элементов.

Это подтверждается тестами `SameSeed_ProducesSameSequence`, `RecentAttack_DoesNotRepeatIndefinitely` и `TargetSelection_DoesNotRepeatPreviousUsableSector`.

### От point decals к targeting геометрии арены

В ранней версии использовались point decals. Commit `4c079f0` заменил их на telegraph, совпадающий с формой конкретного сектора.

`AvatarBossArenaController` предоставляет:

- `GetSectorIndexAtWorldPosition`;
- `GetSectorTargetPoint`;
- `GetAttackTargetSectors`;
- `CreateSectorTelegraph`.

### От визуального разрушения к состоянию арены

Сектор имеет состояния:

```text
Intact → Damaged → Collapsing → Destroyed
```

Источник: `AvatarBossArenaSector.cs`.

Разрушенный сектор исключается из будущего targeting и меняет доступное пространство. Это подтверждено кодом и EditorTests, но не подтверждено human playtest как осмысленный выбор игрока.

### От обычного урона к vulnerability window

`AvatarBossDuelController` снижает урон по телу в закрытом состоянии. После успешного разрушения сектора:

1. сохраняется resolved intent;
2. запускается collapse;
3. открывается vulnerability window;
4. weak points становятся активными;
5. урон ограничивается damage budget;
6. после окончания окна тело снова получает сниженный урон.

Источники: `AvatarBossDuelController.OnAttackExecuted`, `OpenVulnerabilityAfterCollapse`, `PrepareVulnerabilityWindow`, `ConsumeVulnerabilityDamage`.

## Boss architecture

- `AvatarBossController` — здоровье, смерть, weak points и общая boss state;
- `AvatarBossAttackScheduler` — цикл атаки;
- `AvatarBossIntentController` — выбор намерения;
- `AvatarBossDuelController` — связывает intent, арену и vulnerability;
- `AvatarBossPhaseController` — переход во вторую фазу;
- `AvatarBossArenaController` — геометрия и targeting;
- `AvatarBossArenaSector` — состояние отдельного сектора;
- `AvatarBossWeakPoint` — открытие и закрытие damageable collider;
- attack classes — Earth, Fire, Shockwave;
- presentation classes — HUD, VFX, audio и bootstrap;
- testing assembly — agents, assertions и debug tools.

Структура runtime разделена на `Core`, `Arena`, `Attacks`, `Presentation`, `Support`, `Configuration` и `Tests`.

Источник: `Assets/AvatarOfNature/Runtime/AvatarBoss/README.md`.

## Encounter loop

Фактическая цепочка выглядит так:

```text
intent
→ scheduler выбирает атаку
→ attack получает CurrentIntent
→ создаётся telegraph выбранных секторов
→ игрок может изменить позицию или уклониться
→ атака исполняется
→ сектор повреждается или разрушается
→ arena state меняется
→ открывается vulnerability window
→ weak point становится доступен
→ damage budget ограничивает burst
→ окно закрывается
→ scheduler переходит к следующему циклу
```

Код подтверждает выбор target sector по позиции игрока и последствия атаки, но не доказывает, что человек сознательно обманул босса. Это требует playtest.

## Phase 1

Первая фаза включает:

- закрытое тело с уменьшенным damage multiplier;
- Earth, Fire и Shockwave;
- targeting одного или нескольких секторов;
- stagger;
- WeakPointA;
- разрушение арены;
- стандартные vulnerability windows.

Phase 1 настроена как более медленная и читаемая фаза в difficulty profiles.

Источники: `AvatarBossPhaseController`, `AvatarBossDuelController`, `AvatarBossEncounterConfig`, `AvatarBossWeakPoint`.

## Phase 2

В текущем коде переход в Phase 2 запускается по health threshold, по умолчанию около 50%.

Источники: `AvatarBossPhaseController.PhaseTwoThreshold`, `OnBossDamaged`, `StartPhaseTwo`.

Во второй фазе:

- сокращаются timing values;
- сокращается telegraph time;
- меняются stagger multipliers;
- увеличивается давление;
- может включаться Shockwave → Earth combo;
- Fire получает отдельные damage values;
- становится доступен `WeakPointB`;
- vulnerability duration уменьшается.

Важно: checklist описывает переход после достаточного количества успешных циклов, но текущий код использует прежде всего HP threshold, а не отдельный счётчик resolved cycles.

## Player readability and agency

### Что подтверждено кодом

- intent содержит target sector и expected result;
- telegraph геометрически совпадает с сектором;
- атаки имеют разные элементы и visual effects;
- scheduler использует состояния `Idle`, `Telegraph`, `Windup`, `Execute`, `Recover`;
- attack poses различаются для Earth, Fire и Shockwave;
- HUD показывает фазу, атаку, stagger и weak point state;
- недавно разрушенные сектора исключаются из выбора.

### Что пока не доказано

Не найден human playtest, подтверждающий, что игрок:

- понимает intent без объяснения;
- сознательно выбирает, какой сектор пожертвовать;
- отличает осмысленный успех от случайного;
- меняет стратегию после поражения;
- воспринимает бой как дуэль, а не как scripted sequence.

## Weak point and reward loop

Награда за успешное решение состоит из нескольких уровней:

1. сектор арены разрушается;
2. старое безопасное пространство исчезает;
3. босс получает stagger и feedback;
4. открывается короткое vulnerability window;
5. weak point получает увеличенный multiplier и включает collider;
6. damage budget ограничивает burst;
7. после закрытия окна бой продолжается с изменённым состоянием.

`AvatarBossWeakPoint.SetExposed(true)` включает collider и renderer, повышает damage multiplier и hit priority. В закрытом состоянии body damage multiplier снижен через `AvatarBossDuelController`.

## Agentic Development and Unity MCP

Encounter создавался с использованием Agentic Development и Unity MCP.

Agentic workflow использовался для:

- исследования текущей архитектуры и Git history;
- формулирования и уточнения gameplay-гипотез;
- небольших итерационных изменений;
- разделения систем по ответственности;
- поиска regression points;
- анализа тестов и Console;
- поддержания reviewer-readable структуры проекта.

Unity MCP использовался как управляемый интерфейс к Unity Editor для:

- проверки активной сцены и состояния Editor;
- чтения Hierarchy и компонентов;
- проверки сцен, арен и конфигураций;
- запуска и анализа Unity tests;
- чтения Console;
- проверки bootstrap flow;
- итеративной настройки arena sectors, telegraphs, boss poses и combat feedback.

Рабочий цикл:

```text
design hypothesis
→ repository inspection
→ small implementation step
→ Unity MCP validation
→ test / Console inspection
→ gameplay architecture refinement
→ next iteration
```

Unity MCP помогал проверять техническую связность системы, но не заменял human playtest. Техническая validation доказывает наличие состояния, перехода или ошибки в Console; она не доказывает хороший pacing, понятность боя или эмоциональный эффект.

## Validation and evidence

### Unity Editor snapshot

На момент read-only проверки:

- Unity `6000.3.11f1` подключён;
- активна сцена `AvatarBossShowcaseBootstrap`;
- play mode выключен;
- compilation завершена;
- текущая Console не содержит записей;
- test job не выполняется;
- live editor state не содержит свежего `last_run`.

Bootstrap-сцена содержит `AvatarBossShowcaseBootstrap`, `BootstrapCamera`, `BootstrapCanvas`, `EventSystem` и `BootstrapLight`. Компонент bootstrap указывает на `AvatarBossDuelArena`.

### Evidence table

| Claim | Что подтверждает | Источник | Уровень уверенности |
|---|---|---|---|
| Encounter задуман как дуэль намерений, а не постоянный DPS | design document | commit `60f567b`, historical `BOSS_DUEL_GAME_DESIGN.md` | confirmed as design intent |
| Босс выбирает target sector и expected result | код | `AvatarBossIntent`, `AvatarBossIntentController.TryGetNextIntent` | confirmed |
| Intent детерминирован при одинаковом seed | автоматический тест | `AvatarBossIntentControllerTests.SameSeed_ProducesSameSequence` | confirmed |
| Повторение атак ограничено | автоматические тесты | `RecentAttack_DoesNotRepeatIndefinitely`, `TargetSelection_DoesNotRepeatPreviousUsableSector` | confirmed |
| Арена состоит из секторов и колец | код и сцены | `AvatarBossArenaController`, `AvatarBossDuelArena.unity`, commits `082199b`, `7442c0f` | confirmed |
| Сектор проходит Intact → Damaged → Collapsing → Destroyed | код и EditorTests | `AvatarBossArenaSector`, `AvatarBossArenaTests.StateTransitions_RaiseEventsAndReachDestroyed` | confirmed |
| Telegraph совпадает с формой сектора | код и EditorTests | `CreateSectorTelegraph`, `CreateSectorTelegraph_MatchesCellWithoutAddingCollision` | confirmed |
| Shockwave является positional/push threat | код и история | `AvatarBossShockwaveAttack`, commits `862a0ff`, `73af8dd` | confirmed |
| Shockwave не открывает arena vulnerability window напрямую | код | `AvatarBossDuelController.OnAttackExecuted` | confirmed |
| Разрушение сектора открывает vulnerability window | код | `AvatarBossDuelController.OpenVulnerabilityAfterCollapse` | confirmed |
| Урон в window ограничен damage budget | код и EditorTests | `ConsumeVulnerabilityDamage`, `DuelWindow_StandardBudgetClampsAcceptedDamage` | confirmed |
| WeakPointA и WeakPointB существуют | код и runtime agent | `AvatarBossWeakPoint`, `AvatarBossGameplayTestAgent` | confirmed |
| WeakPointB относится ко второй фазе | код и EditorTest | `PhaseTwoAttached`, `WeakPoint_PhaseTwoAttached_DefaultsFalse` | confirmed |
| Phase 2 ускоряет timing и меняет параметры атак | код | `AvatarBossPhaseController.StartPhaseTwo` | confirmed |
| Phase 2 запускается после resolved cycles | checklist | `Documentation/BossDuel/TZ_CHECKLIST.md` | partial; code uses HP threshold |
| Босс неподвижен в центре | design, checklist, scene structure | `BOSS_DUEL_GAME_DESIGN.md`, `TZ_CHECKLIST.md`, `AvatarBossDuelArena.unity` | partial |
| 17/17 EditMode tests прошли | historical validation note | `TZ_CHECKLIST.md`, final check section | confirmed historically, not freshly rerun |
| 14/14 runtime steps прошли | historical validation note | `TZ_CHECKLIST.md` | confirmed historically, not freshly rerun |
| Runtime regression является обычным player playthrough | runtime agent source | `AvatarBossGameplayTestAgent`, hybrid nudge/finisher comments | disproved |
| Игрок понимает intent без подсказки | отсутствует human playtest | нет подходящего артефакта | pending |
| Pacing является хорошим | код и regression tests недостаточны | нет human playtest evidence | pending |
| Финальный standalone build проверен | checklist оставляет шаг незавершённым | `TZ_CHECKLIST.md` | pending |

## Current limitations

1. Human playtest не найден.
2. Runtime regression использует test-only shortcuts: hybrid nudge, forced phase progression, forced combo и deterministic finisher.
3. Phase 2 привязана к HP threshold, хотя design language говорит о resolved cycles.
4. Исторический дизайн описывает Phase 3 и final core, но текущий код надёжно подтверждает две фазы.
5. Unity Editor во время исследования открыт на bootstrap-сцене, а не на живой arena scene.
6. Рабочая копия содержит незакоммиченные изменения в balance/config, duel controller, summon и projectile-related файлах.
7. В checklist остаются незакрытые пункты по human playtest, readable baiting, полноценному прохождению, standalone build и финальному видео.

## What is safe to claim publicly

Без преувеличения можно заявлять:

- Спроектирован boss encounter, в котором основной урон открывается через решение атаки, а не через постоянный DPS.
- Реализована круговая разрушаемая арена с состояниями секторов.
- Добавлена детерминированная модель intent с target sector, expected result и ограниченной памятью недавних атак.
- Реализованы Earth, Fire и Shockwave attacks с различными пространственными последствиями.
- Telegraph привязан к геометрии арены и виден до исполнения атаки.
- Успешное разрушение сектора открывает ограниченное vulnerability window.
- Реализованы weak points, stagger, Phase 2, difficulty profiles и debug harness.
- Архитектура переработана по ответственности: controller, scheduler, intent, arena, attacks, presentation, configuration и testing.
- Автоматические EditorTests и runtime regression agent проверяют ключевые переходы и cleanup.
- В разработке использовались Agentic Development и Unity MCP для короткого цикла между hypothesis, implementation и validation.

Формулировка для публичного case study:

> Я использовал Agentic Development как партнёрский workflow для исследования репозитория, итеративной реализации и проверки gameplay-систем. Через Unity MCP я работал с Unity Editor на уровне сцен, компонентов, Console и тестов, сохраняя короткий цикл между design hypothesis, implementation и validation.

И обязательная оговорка:

> MCP и автоматические агенты помогали проверять техническую связность систем, но не заменяли human playtest и не доказывали качество pacing сами по себе.

## What still needs confirmation

1. Был ли human playtest без объяснения правил?
2. Понимал ли игрок, что атаку можно использовать против босса?
3. Было ли сознательное заманивание атаки или только перемещение в target sector?
4. Почему Phase 2 в итоге привязана к HP threshold?
5. Phase 3 и final core были отброшены или ещё не завершены?
6. Сколько раз проходились обе фазы от bootstrap до победы?
7. Что стало причиной последних balance changes?
8. Какие проблемы были обнаружены в реальном playtest?
9. Был ли записан финальный standalone build или video playthrough?

## Source map

### Design and documentation

- `Documentation/BossDuel/GAME_DESIGN.md`
- `Documentation/BossDuel/TZ_CHECKLIST.md`
- historical `BOSS_DUEL_GAME_DESIGN.md` from commit `60f567b`
- `Assets/AvatarOfNature/README.md`

### Runtime

- `Assets/AvatarOfNature/Runtime/AvatarBoss/Core/AvatarBossController.cs`
- `AvatarBossController.Vulnerability.cs`
- `AvatarBossAttackScheduler.cs`
- `AvatarBossIntent.cs`
- `AvatarBossIntentController.cs`
- `AvatarBossDuelController.cs`
- `AvatarBossPhaseController.cs`
- `AvatarBossStagger.cs`

### Arena and attacks

- `Assets/AvatarOfNature/Runtime/AvatarBoss/Arena/AvatarBossArenaController.cs`
- `AvatarBossArenaSector.cs`
- `Attacks/AvatarBossEarthAttack.cs`
- `Attacks/AvatarBossFireAttack.cs`
- `Attacks/AvatarBossShockwaveAttack.cs`

### Presentation and configuration

- `Presentation/AvatarBossWeakPoint.cs`
- `Presentation/AvatarBossVisualEffects.cs`
- `Presentation/AvatarBossPresentationHUD.cs`
- `Presentation/AvatarBossWorldHUD.cs`
- `Presentation/AvatarBossShowcaseBootstrap.cs`
- `Config/Resources/AvatarBossEncounterConfig.asset`
- `Config/AvatarBossBalance.json`
- `Runtime/AvatarBoss/Configuration/AvatarBossEncounterConfig.cs`
- `Runtime/AvatarBoss/Configuration/AvatarBossDifficultyProfile.cs`

### Tests and harness

- `Tests/Editor/AvatarBossArenaTests.cs`
- `Tests/Editor/AvatarBossIntentControllerTests.cs`
- `Tests/Editor/AvatarBossStateTests.cs`
- `Tests/Runtime/AvatarBossGameplayTestAgent.cs`
- `Tests/Runtime/AvatarBossFreeRunAgent.cs`
- `Tests/Runtime/AvatarBossGameStateAssertions.cs`
- `Tests/Runtime/AvatarBossDebugPanel.cs`
- `Tests/Runtime/AvatarBossTestReport.cs`

### Important commits

`266c55e`, `d6dfdb4`, `61c5fc1`, `60f567b`, `3bcc8d7`, `e3627dd`, `082199b`, `7442c0f`, `862a0ff`, `4c079f0`, `73af8dd`, `20e6d8f`, `92aa658`, `dfe9a42`, `2e0b63f`, `b8ce9e8`, `a35d213`, `1521d6a`.

---

Исследование выполнено read-only: play mode и тесты для сбора этого отчёта не запускались, destructive-команды и коммиты не выполнялись. В рамках подготовки этого документа создан только данный Markdown-файл; существующие изменения рабочей копии не затрагивались.
