# Dirge of Withering («Песнь Увядания»)

3D Action-RPG (top-down / isometric camera) на **Godot 4.x + C#**.  
Лор, фракции и дизайн-механики описаны в [CONCEPT.md](CONCEPT.md). Этот файл — про инженерную сторону: стек, запуск, архитектуру и текущий статус прототипа.

## Стек


| Компонент         | Версия / выбор                                   |
| ----------------- | ------------------------------------------------ |
| Движок            | Godot **4.7** (.NET / mono build)                |
| Язык              | **C#** (`Godot.NET.Sdk/4.7.1`)                   |
| Runtime           | **.NET 8**                                       |
| Рендер            | Forward Plus                                     |
| Перспектива       | 3D, камера сверху-сбоку (Sims / Project Zomboid) |
| Графика прототипа | Примитивы локации + Mixamo NPC (Mutant / Zombie) |


Assembly: `DirgeOfWithering`  
Root namespace: `DirgeOfWithering`

## Требования

1. [Godot 4.x .NET](https://godotengine.org/download) (не standard build)
2. [.NET SDK 8+](https://dotnet.microsoft.com/download)
3. macOS / Windows / Linux

Проверка SDK:

```bash
dotnet --version
```



## Запуск

1. Открыть папку проекта в **Godot (.NET)** (`project.godot`)
2. **Project → Rebuild Project** (или иконка Build)
3. **F5** — стартует `Scenes/Locations/FloodedCathedralSlice.tscn`

CLI (опционально):

```bash
dotnet build DirgeOfWithering.csproj -c Debug
```

Сборка кладётся в `.godot/mono/temp/bin/Debug/`.

## Управление


| Ввод           | Действие                                                |
| -------------- | ------------------------------------------------------- |
| WASD / стрелки | Движение **относительно курсора** (W = к точке прицела) |
| Мышь | Прицел (raycast камера → пол/мир) |
| ЛКМ (`attack`) | Обычный удар |
| RMB (`heavy_attack`) | Тяжёлый удар (+Скверна, больше урон) |

Input Map: `move_up/down/left/right`, `attack`, `heavy_attack`.

## Структура репозитория

```
Scripts/
  Camera/FollowCamera.cs
  Combat/                     # Health, Hitbox3D, hit-stop, layers
  Player/                     # move / aim / attack / PlayerAnimDriver
  Enemy/                      # BasicEnemy, EnemyAnimDriver, EnemySpawner
  World/ErrengardPalette.cs   # палитра среза (пепел / багровый / жёлтый)
  UI/SliceHud.cs, EnemyNameplate.cs, DebugHud.cs (TestWorld)
Scenes/
  Locations/FloodedCathedralSlice.tscn   # main scene (Vertical Slice)
  TestWorld.tscn                         # песочница Core Loop
  Player/Player.tscn                     # Blood Knight (Mixamo)
  Enemy/MutantBrute.tscn                 # Mixamo mutant
  Enemy/ZombieThrall.tscn                # Mixamo zombie (Ch10)
  Enemy/BasicEnemy.tscn                  # capsule fallback
Assets/ThirdParty/BloodKnight/           # player glb — CREDITS.md
Assets/ThirdParty/BlightedWretch/        # NPC glb — CREDITS.md
Assets/ThirdParty/CathedralSlice/        # локация: Processed/CathedralSliceArt.glb — CREDITS.md
Assets/ThirdParty/Weapons/               # zweihander.glb
Assets/ThirdParty/appearance-effect-starlight/  # HIGH-FILTH aura
Assets/Audio/                            # music + SFX — CREDITS.md
Assets/VFX/Particles/                    # ash / smoke sprites
Tools/build_cathedral_slice_art.py       # Blender: сборка арта локации
Tools/merge_mixamo_anims.py              # Mixamo FBX → GLB (mutant/zombie/blood_knight)
Tools/prepare_cathedral_slice_assets.sh  # распаковка Source → kits
CONCEPT.md
```

## Архитектура геймплея

**Сейчас:** Vertical Slice **закрыт** (2.1–2.7). Дальше — расширение за пределы среза.  
Маршрут среза: вход → неф (Ichor Beast) → апсида (Ash Walker) → алтарь очищения.  
Визуал: `CathedralSliceArt.glb`; коллизии — прежние `StaticBody3D` в сцене.

| Роль | Сцена | DisplayName | Характер |
|------|-------|-------------|----------|
| Давление | `MutantBrute` (неф) | Ichor Beast | Быстрый, больший урон |
| Блокер | `ZombieThrall` (апсида) | Ash Walker | Медленный, толстый |

Бой: телеграф → окно хитбокса → recovery; death держит позу.  
FILTH (2.2, код `Blight`) + алтарь на месте. HUD среза: `Health` / `FILTH` + nameplates над агро-NPC (EN).

```
Player (CharacterBody3D)
 ├─ Health
 ├─ PlayerAttack
 └─ Visual (AimPivot)
     └─ Hitbox3D ──mask──► Enemy layer

MutantBrute / ZombieThrall (BasicEnemy)
 ├─ Health
 ├─ AnimDriver  → idle / walk|run / telegraph / attack / death
 └─ Visual
     ├─ Model (Processed/*.glb, текстуры зомби 1K)
     └─ Hitbox3D ──mask──► Player layer
```



### Бой

- Урон только через `Hitbox3D` → `Health.TakeDamage` (не через стол тел).
- Один `Health` бьётся не чаще раза за активацию хитбокса.
- Попадание: **knockback**, **hit-stop**, у врага — **stagger** (сбивает телеграф).
- У игрока после урона — **i-frames** (~0.5 с).
- Во время wind-up / active frames скорость игрока режется (`AttackMoveMultiplier`).
- Смерть игрока → короткая пауза → `ReloadCurrentScene`.
- Смерть врага → despawn → `EnemySpawner` поднимает нового через delay.



### Камера и прицел

- `FollowCamera`: позиция = `Target + Offset`, `LookAt` на цель; сглаживание через lerp.
- WASD строится от горизонтального вектора к точке под курсором, не от базиса камеры.
- Прицел: `Camera3D.ProjectRay*` + physics ray (World|Enemy) с fallback на плоскость Y.



### Физические слои


| Слой | Имя    | Bit      |
| ---- | ------ | -------- |
| 1    | World  | `1 << 0` |
| 2    | Player | `1 << 1` |
| 3    | Enemy  | `1 << 2` |


Константы: `Scripts/Combat/CombatLayers.cs`.

## Соглашения по коду

- Скрипты — `partial class`, имя файла = имя класса (`Player.cs` → `Player`).
- Namespace: `DirgeOfWithering` (без вложенного `...Player.Player` — ломает резолв Godot).
- Пути `res://` **case-sensitive** для C# bridge: держим `Scripts/`, `Scenes/`.
- Не вызывать `AddChild` на родителе из `_Ready` ребёнка во время setup — использовать `CallDeferred` (см. `EnemySpawner`).
- Экспорт тюнинга боя через `[Export]` на компонентах (`WindupTime`, `KnockbackForce`, …).
- **In-game UI copy is English** (`Health`, `FILTH`, enemy `DisplayName`). Docs/comments may stay Russian.
## CI (GitHub Actions)

Workflow: [`.github/workflows/ci.yml`](.github/workflows/ci.yml)

| Job | Что делает |
|-----|------------|
| `csharp-build` | `dotnet restore/build` Debug + Release через `Godot.NET.Sdk` (без редактора) |
| `godot-validate` | Godot 4.7.1 .NET: `--import` → `--build-solutions` → проверка `DirgeOfWithering.dll` |

Триггеры: `push` / `pull_request` в `master`|`main`, плюс `workflow_dispatch`.  
Версию Godot в CI держим синхронно с `Godot.NET.Sdk` в `.csproj`.

## Роудмап

| Этап | Цель | Статус |
|------|------|--------|
| 1. Core Loop | Move / attack / damage / death | Готово |
| 2.1 Каркас среза | Локация «Затопленный собор», палитра, Core Loop на месте | Готово |
| 2.2 Скверна | Шкала, бафф, перегрузка | Готово |
| 2.3 Канон-враг | Ихор-зверь + Пепельный ходок, телеграф, роли, тинт | Готово |
| 2.4 Локация | Sketchfab-арт маршрута, кьяроскуро, пропы | Готово |
| 2.5 UI среза | SliceHud (Health/FILTH), nameplates, fog/grading | Готово |
| 2.6–2.7 | Звук, критерий «5 минут атмосферы» | Готово |
| **3. Ребаланс** | Урон / FILTH / окна хитбоксов / читаемость HIGH-баффа | **Следующее** |
| 4. Масштабирование | Больше контента и систем за пределами одного среза | Далее |

### DoD Vertical Slice (playtest ~5 мин)

Маршрут: вход → Ichor Beast → Ash Walker → алтарь. Срез закрыт, если прогон подтверждает:

- играет ambient (`Assets/Audio/HauntedLullabiesofJapan_Kazoe Uta (Tension).mp3`);
- слышны swing / hit / footsteps / death / FILTH overload / altar;
- у рыцаря виден zweihander в руках;
- смерть → overlay **You Died** + кнопка **Restart** (не silent auto-reload);
- main scene = `Scenes/Locations/FloodedCathedralSlice.tscn`;
- визуально читается как Errengard (fog / ichor / altar).

Credits: [`Assets/Audio/CREDITS.md`](Assets/Audio/CREDITS.md).

Песочница Core Loop: `Scenes/TestWorld.tscn` (не main; удобна для ребаланса).

### Перспективы (после ребаланса / при масштабировании)

**Бой и прогрессия**
- Ребаланс урона, FILTH gain/decay, порогов HIGH / OVERLOAD.
- Явнее связать урон со скверной (множители уже есть — донастроить и показать в UI).
- Анимации получения урона с прерыванием атак (player + enemies).
- 3D SFX врагов: шаги, рычания / телеграф-вой, смерть (spatial `AudioStreamPlayer3D`).

**Снаряжение и враги**
- Инвентарь: смена оружия / доспехов, статы предметов (срез остаётся на одном zweihänder до этого этапа).
- Категории врагов: не все заражают FILTH; свои статы, пассивки, уязвимости.

**Мир и RPG-системы**
- Свободная 3D-камера (мышь), без фикса top-down — отдельный control-pass.
- Дружественные NPC, диалоги, квесты, система отношений.
- Цель: полноценная dark-fantasy Action-RPG в Errengard (см. [CONCEPT.md](CONCEPT.md)).

## Документация

- [CONCEPT.md](CONCEPT.md) — сеттинг, герой, Скверна, живое оружие, метроидвания, моральные развилки.

