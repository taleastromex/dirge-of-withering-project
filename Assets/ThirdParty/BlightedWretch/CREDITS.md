# Blighted Wretch assets

Игровые файлы лежат в `Processed/`. Исходные Mixamo FBX из репозитория убраны.

## MutantBrute.glb
- **Source:** Adobe Mixamo — character *Parasite L Starkie* + mutant animation set
- Follow Adobe Mixamo terms for game use.

## ZombieThrall.glb
- **Source:** Adobe Mixamo — character *Ch10_nonPBR* + zombie animation set
- Textures downscaled to **1024** for the Vertical Slice

## Processed clips (logical names)
`idle`, `walk`, `run`, `attack`, `stagger`, `death`

Rebuild (если снова понадобится): скачать FBX с Mixamo и прогнать `Tools/merge_mixamo_anims.py --preset mutant|zombie`.
