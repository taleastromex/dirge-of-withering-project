# Blood Knight (player)

Игровой файл: `Processed/BloodKnight.glb`.

## Sources
- **Character mesh:** Sketchfab / pack *Knight of the Blood Order* (Drakul), re-rigged via Adobe Mixamo Auto-Rigger
- **Animations:** Adobe Mixamo — Great Sword pack, Action Adventure Pack (Calm), Unarmed Combat, One-Hand Sword (+ Sword and Shield locomotion), Roll
- Follow Adobe Mixamo terms for game use

## Processed clips
Great Sword (drawn 2H): `idle`…`idle_5`, `walk`, `run`, `attack` (Great Sword Slash / ЛКМ), `attack_heavy` (Great Sword Slash (2) / ПКМ), `attack_spin`, `stagger`, `death`, `2h_draw`, `2h_draw_alt`, `2h_sheathe`, `2h_sheathe_alt`  
Calm: `calm_idle`, `calm_walk`, `calm_run`  
Unarmed combat: `unarmed_idle`, `unarmed_attack`, `unarmed_attack_alt`, `unarmed_attack_heavy`, `unarmed_stagger`  
1H: `1h_idle`, `1h_walk`, `1h_run` (Run With Sword), `1h_attack`, `1h_attack_alt`, `1h_attack_heavy`, `1h_stagger`, `1h_draw`, `1h_sheathe`  
Other: `roll` (не привязан к вводу)  
Death variants: `death_dying`, `death_alt`, `death_flyback`

## Rebuild
```bash
blender --factory-startup --background --python Tools/merge_mixamo_anims.py -- \
  --base "Assets/ThirdParty/CathedralSlice/Source/knight-of-the-blood-order/source/bloodknight_mixamo_rigged.fbx" \
  --anims-dir "Assets/ThirdParty/CathedralSlice/Source/Great Sword Pack" \
  --out "Assets/ThirdParty/BloodKnight/Processed/BloodKnight.glb" \
  --preset blood_knight \
  --strip-root-motion \
  --textures-dir "Assets/ThirdParty/CathedralSlice/Source/knight-of-the-blood-order/textures" \
  --extra-anims-dir "Assets/ThirdParty/CathedralSlice/Source/HumanHostiles/Bandit" \
  --extra-preset mixamo_deaths \
  --extra "Assets/ThirdParty/CathedralSlice/Source/Great Sword Pack::great_sword_draw" \
  --extra "Assets/ThirdParty/CathedralSlice/Source/Mixamo/Player/Unarmed/Action Adventure Pack::unarmed_calm" \
  --extra "Assets/ThirdParty/CathedralSlice/Source/Mixamo/Player/Unarmed/Combat::unarmed_combat" \
  --extra "Assets/ThirdParty/CathedralSlice/Source/Mixamo/Player/OneHand/Sword::one_hand_sword" \
  --extra "Assets/ThirdParty/CathedralSlice/Source/Mixamo/Player::player_roll"
```
