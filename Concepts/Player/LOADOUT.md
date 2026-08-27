# Player loadout (4.3)

Слоты: одно оружие + один доспех. Визуал Blood Knight не меняется — броня только статы.

## Стойки

По умолчанию **Calm**. Calm не бывает с оголённым оружием.

| Состояние | Когда | Клипы |
| --- | --- | --- |
| Calm | ножны, нет рукопашки ≥5 с | Adventure Pack: `calm_idle` / `calm_walk` / `calm_run` |
| Combat, unarmed | удар кулаками из ножен | `Unarmed/Combat`: idle; ЛКМ Punch↔Cross Punch; ПКМ kick; ходьба Adventure |
| Combat, drawn | R достал оружие | 2H Great Sword или 1H клипы |

- **R** — достать (Combat) / убрать (сразу Calm).
- Удар в ножнах — рукопашка: вход в Combat. Через **5 с** без удара — Calm.
- **1** / **2** — предыдущее / следующее оружие (авто-ножны → Calm).
- `roll` в GLB есть, уворот не в этом шаге.

Grip tune (`PlayerWeaponAttach.GripTuneMode`): HAND / SHEATHE, опциональный T-pose, **SAVE .tres**. Ножны крутить на Calm idle, одноруч — на HAND + `1h_idle`.

## Вес

`MaxCarryWeight = 20`. Сумма веса оружия + брони. Сверх лимита экип не ставится.

Стартовый сет: zweihander 8 + plate 10 = 18.

## Ресурсы

- `Assets/Combat/Weapons/*_Equip.tres`
- `Assets/Combat/Armor/CursedKnightPlate_Equip.tres`

Вторую броню и смену меша игрока — не в этом шаге.

Playtest Windows (Aug 2026): ок — этап 4.3 закрыт. Далее: 4.4 World expand.
