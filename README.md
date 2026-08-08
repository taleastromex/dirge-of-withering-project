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
| Графика прототипа | Примитивы (`CapsuleMesh`, `BoxMesh`)             |


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
| Мышь           | Прицел (raycast камера → пол/мир)                       |
| ЛКМ (`attack`) | Ближний удар в сторону прицела                          |


Input Map: `move_up/down/left/right`, `attack`.

## Структура репозитория

```
Scripts/
  Camera/FollowCamera.cs
  Combat/                     # Health, Hitbox3D, hit-stop, layers
  Player/                     # move / aim / attack
  Enemy/                      # BasicEnemy, EnemySpawner
  World/ErrengardPalette.cs   # палитра среза (пепел / багровый / жёлтый)
  UI/DebugHud.cs
Scenes/
  Locations/FloodedCathedralSlice.tscn   # main scene (Vertical Slice 2.1)
  TestWorld.tscn                         # песочница Core Loop
  Player/Player.tscn
  Enemy/BasicEnemy.tscn
CONCEPT.md
```

## Архитектура геймплея

**Сейчас:** этап **2.1** — каркас Vertical Slice на локации «Затопленный собор».  
Core Loop (движение → удар → урон → смерть) перенесён как есть; DebugHud пока остаётся.

```
Player (CharacterBody3D)
 ├─ Health
 ├─ PlayerAttack
 └─ Visual (AimPivot)
     └─ Hitbox3D ──mask──► Enemy layer

BasicEnemy (CharacterBody3D)
 ├─ Health
 └─ Visual
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
| 2.1 Каркас среза | Локация «Затопленный собор», палитра, Core Loop на месте | **Сейчас** |
| 2.2 Скверна | Шкала, бафф, перегрузка | Дальше |
| 2.3 Канон-враг | Читаемый архетип под лор | Дальше |
| 2.4–2.7 | Арт маршрута, UI, звук, критерий «5 минут атмосферы» | Дальше |

Песочница Core Loop: `Scenes/TestWorld.tscn` (не main).

## Документация

- [CONCEPT.md](CONCEPT.md) — сеттинг, герой, Скверна, живое оружие, метроидвания, моральные развилки.

