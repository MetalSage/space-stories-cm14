# Путеводитель по системе Синтетиков (Synth)

Порт синтетиков/андроидов из CM-SS13 (BYOND) на этот SS14-форк. Документ описывает архитектуру:
что где лежит, из каких компонентов/систем состоит и как они связаны друг с другом.

## Структура папок

```
Content.Shared/_Stories/Synth/
├── Core/                — базовый компонент/система синта (SynthComponent, SharedSynthSystem)
├── Generation/           — поколения (Gen1/2/3, ARES Worker) и их применение
├── ItemRestriction/       — общий гейт "только для синта / не для синта" на предметы
├── MaintenanceStation/     — ремонтная станция для синта
├── SelfRecharging/         — саморегенерация раствора/жидкостей у синта
├── VoiceSynthesizer/       — голосовой синтезатор (панель фраз Working Joe и т.п.)
├── WorkingJoe/             — Working Joe: спецкомпоненты внешности/юниформы
├── STWallBreacherComponent.cs / STWallBreacherSystem.cs
│                          — механика "снос стены до балки" (молот-таран)
└── (в Resources/Prototypes/_Stories/Synth/) — все YAML-прототипы

Content.Server/_Stories/Synth/    — серверные половины систем выше (SynthSystem, VariantSystem и т.д.)
Content.Client/_Stories/Synth/    — клиентские половины (UI voice synthesizer и т.д.)

Content.Shared/_RMC14/ARES/
├── CoreGas/    — нейрогаз ядра ареса (без атмосферы, через маркеры на карте)
└── Lockdown/    — лок-даун ядра ареса (двери + турели по сигналу)
```

## Core: SynthComponent / SharedSynthSystem

Базовый компонент, вешается на любого синта. Ключевые поля:
- `CanUseGuns` / `CanUseMeleeWeapons` — разрешения на оружие
- `NewBloodReagent`, `NewDamageModifier` — своя кровь и модификатор урона (см. `synth_modifier_sets.yml`)
- `RepairTime` / `SelfRepairTime` — ремонт сваркой (Brute) и кабелем (Burn)
- `CritThreshold` — свой порог крита

`SharedSynthSystem` вешает/снимает компоненты через `STSynthAddComponents`/`STSynthRemoveComponents`
(`Resources/Prototypes/_Stories/Synth/synth_add_remove_components.yml`), даёт стан-резист,
блокирует сон, обрабатывает попытки использовать оружие/ближний бой не по праву.

Серверная часть (`Content.Server/_Stories/Synth/Core/SynthSystem.cs`) удаляет органы
(не дышит/не метаболизирует), меняет кровь, ставит протезный мозг, блокирует гранаты без `CanUseGuns`.

## Generation: поколения синта

`SynthGenerationComponent` — какое поколение (`STSynthGenOne`/`Two`/`Three`/`STARESWorker`),
можно ли выбирать (`Selectable`). Прототипы поколений — `synth_generations.yml`:
- **Gen1** — не выбирается нигде, зафиксировано только за Automaton Joe. Может хватать/кидать тир2 ксеносов.
- **Gen2/Gen3** — выбираются через `variants` у большинства джобов; умеют учить ксено-язык
  (`LanguageLearning: preset: STLanguageLearningPresetSynthColonist`)
- **ARES Worker** — зафиксировано только за Working Joe, не учит язык

`SharedSynthGenerationSystem` применяет компоненты поколения (`AddComponents`) и обновляет
модификаторы скорости движения через `RefreshMovementSpeedModifiers`. Применение идёт в двух
точках: на `MapInit` (`SynthStartup`) для уже проставленного поколения, и повторно в
`PlayerSpawnCompleteEvent` — на случай, если поколение выставляется позже инициализации карты.

Для admin-spawn (`RMCAdminSpawnedComponent`) поколение сбрасывается и переспрашивается заново,
но только если оно уже было установлено к моменту спавна.

`STSynthGenerationVariantSystem` применяет поколение, выбранное в лобби через
`JobPrototype.Variants`, вместо диалога в игре.

## WorkingJoe

- `STMobWorkingJoe` — прототип моба (jobEntity-паттерн), генерация зашита напрямую (`STARESWorker`)
- `STWorkingJoeUniformTagSystem` — подменяет имя на error-tag при снятой форме (у Working Joe нет лица)
- `STWorkingJoeVariantSystem` (Server) — Standard/Hazmat вариант из лобби-преференса
- `STMaintenanceJack` (`Resources/Prototypes/_Stories/Synth/WorkingJoe/maintenance_jack.yml`) —
  фирменный инструмент: crowbar/wrench toggle, `Prying: pryPowered: true` (вскрывает двери
  независимо от наличия питания)

## Reset Key / Дефибрилляция

Синта нельзя откачать обычным дефибриллятором (`RMCRevivableComponent` снимается в `MakeSynth`).
Нужен `STSynthResetKey` (и его брендовые варианты, напр. `STSynthResetKeySeegson`) — кладётся
не в suit storage напрямую, а внутрь контейнера (`storage: back:`/`belt:`), как в CM13.

## Suit Storage (домкрат, любая броня)

Домкрат (`STMaintenanceJack`) помещается в suitstorage при наличии любой брони/куртки,
не только фирменной. Тег `STMaintenanceJack` добавлен в whitelist всех 12 семейств
`AllowSuitStorage` в `base_armor.yml`/`coats.yml`/`webbing.yml` (`_RMC14/Entities/Clothing/OuterClothing/`).

## Телескопическая дубинка (STTelescopicBaton)

`Resources/Prototypes/_RMC14/Entities/Objects/Weapons/Melee/stun_baton.yml`. Механика: стамина-урон
на каждый удар в разложенном состоянии — `RMCStaminaDamageOnHit: damage: 40` (= `stun_force` из
CM13). Свой спрайт `_Stories/Synth/telescopic_baton.rsi` (из CM13 non_lethal.dmi).

## Молот-таран (STSynthBreachingHammer)

`Resources/Prototypes/_Stories/Synth/synth_breaching_hammer.yml`. 55/55 Blunt+Structural урон
(45 база + 10 бонус за хват), только для синта (`STSynthItemRestriction`), носится на спине.

**Снос стены** — отдельная механика, не просто урон: `STWallBreacherComponent`/`STWallBreacherSystem`
(`Content.Shared/_Stories/Synth/STWallBreacherComponent.cs`) — юз на стене (тег `Wall`) в двуручном
хвате → do_after 5 сек → стена удаляется, спавнится `RMCGirderDamaged` (та же RMC-балка, что и от
кислотной дыры ксеноса) — матчит CM13: стена не исчезает целиком, а откатывается до стадии балки.

## ARES / Гермес: нейрогаз и лок-даун

Пункт "Ядро" в терминале Hermes виден только если у залогинившегося персонажа
`SynthGenerationComponent.Generation == "STARESWorker"` (т.е. Working Joe) — проверяется при
логине в `ARESExternalTerminalSystem.OnExternalLogin`, для обычных маринов терминал не меняется.

### Нейрогаз (`Content.Shared/_RMC14/ARES/CoreGas/`)

В этом форке нет симуляции атмосферы, поэтому вместо газового облака: `STARESCoreGasZone` —
невидимый маркер без коллизии (`Resources/Prototypes/_Stories/Synth/ares_core_gas_zone.yml`,
наследник `MarkerBase`, виден только в редакторе карт). По кнопке "Ship Decontamination" в
Гермесе `STARESCoreGasSystem` находит все маркеры на карте ядра, для каждого убивает всё живое в
радиусе (1000 урона Poison, `ignoreResistances: true`), кроме синтов (`SynthComponent`), и
уничтожает сорняки/смолу (`XenoWeedsComponent`). Кулдаун 2 мин на `ARESCoreComponent.NextGasRelease`.

Маркеры `STARESCoreGasZone` расставляются мапперами вручную по полу комнаты ядра ареса на каждой
карте — это картостроительная задача, а не программная.

### Лок-даун (`Content.Shared/_RMC14/ARES/Lockdown/`)

По кнопке "Lockdown Overrides" `STARESCoreLockdownSystem` тоглит `ARESCoreComponent.LockdownActive`
(кулдаун 2 мин) и рассылает broadcast `STARESCoreLockdownChangedEvent`:
- `STARESLockdownDoorComponent` на двери — закрывается/открывается, если на одной карте с ядром
  (прототип `STARESLockdownDoor`, свой спрайт `_Stories/Synth/ares_lockdown_door.rsi` из CM13 aidoor)
- `STARESLockdownTurretComponent` на уже задеплоенной (но выключенной) турели —
  `SentrySystem.TrySetMode(On/Off, remote: true)`

Известные ограничения:
- Дверь использует статичные кадры открытия/закрытия вместо покадровой анимации CM13
- Физическая кнопка "AI Core Lockdown" в мире не реализована (спрайт
  `_Stories/Synth/ares_lockdown_button.rsi` есть, кнопка доступна только из терминала Hermes)
- `STARESLockdownTurret` нужно вручную добавлять на конкретный прототип турели рядом с ядром
  (изначально в режиме `Off`) — выбор турели остаётся за маппером/дизайнером

## Механики синта из CM13 — что портировано, а что нет

Портировано: урон, стан, иммунитеты, языки, оружие, участие в мятеже, ремонт, нейротоксин,
reset-key. Не портировано: полный иммунитет к холоду/жаре (сейчас только резист), отсутствие
болевого урона / x7 урон голыми руками, EMP-эффект, хирургия пришивания головы; самопочинка не
завязана на `CanUseGuns`, как в CM13.

## Специализации и вендор

- `synth_specialization_ids.yml` / `synth_specialization_loadout.yml` — косметические
  специализации ID-карты синта
- `synthetic_vendor.yml` / `synth_tokens.yml` / `STVendorPointsTokenComponent`/`System` —
  вендор синта и его токены очков
