using Godot;

namespace DirgeOfWithering;

/// <summary>
/// Weapon + armor slots, carry weight, draw/sheathe. Default Calm (sheathed).
/// </summary>
public partial class PlayerLoadout : Node
{
	[Export]
	public float MaxCarryWeight { get; set; } = 20f;

	[Export]
	public int UnarmedDamageN { get; set; } = 14;

	[Export]
	public PlayerWeaponAttach? WeaponAttach { get; set; }

	[Export]
	public PlayerAnimDriver? AnimDriver { get; set; }

	[Export]
	public PlayerAttack? Attack { get; set; }

	[Export]
	public Health? Health { get; set; }

	[Export]
	public float UnarmedCombatTimeout { get; set; } = 5f;

	private readonly System.Collections.Generic.List<WeaponEquipData> _weapons = new();
	private ArmorEquipData? _armor;
	private int _weaponIndex;
	private bool _drawn;
	private bool _pendingSheathe;
	private bool _pendingDraw;
	private float _drawBindNorm = 0.62f;
	private float _sheatheBindNorm = 0.84f;
	private bool _sheatheSocketBound;
	private bool _unarmedCombat;
	private float _unarmedCombatTimer;

	public string HudLine { get; private set; } = "";

	public string TransientHint { get; private set; } = "";

	public override void _Ready()
	{
		WeaponAttach ??= GetNodeOrNull<PlayerWeaponAttach>("../WeaponAttach");
		AnimDriver ??= GetNodeOrNull<PlayerAnimDriver>("../AnimDriver");
		Attack ??= GetNodeOrNull<PlayerAttack>("../PlayerAttack");
		Health ??= GetNodeOrNull<Health>("../Health");
		SetProcess(true);
		SetProcessUnhandledInput(true);
		CallDeferred(nameof(Boot));
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		if (_pendingSheathe)
		{
			TickPendingSheathe();
		}

		TickPendingDraw();
		TickUnarmedCombatTimeout(dt);
	}

	public void NotifyAttackStarted()
	{
		if (_drawn)
		{
			return;
		}

		_unarmedCombatTimer = 0f;
		if (_unarmedCombat)
		{
			return;
		}

		_unarmedCombat = true;
		ApplyUnarmedClipSet();
		RefreshHud();
	}

	public bool IsDrawn => _drawn;

	public override void _UnhandledInput(InputEvent @event)
	{
		if (WeaponAttach != null && WeaponAttach.GripTuneMode)
		{
			return;
		}

		if (@event.IsEcho() || !@event.IsPressed())
		{
			return;
		}

		if (@event.IsActionPressed("weapon_toggle"))
		{
			ToggleDrawn();
			GetViewport().SetInputAsHandled();
			return;
		}

		if (@event.IsActionPressed("weapon_prev"))
		{
			CycleWeapon(-1);
			GetViewport().SetInputAsHandled();
			return;
		}

		if (@event.IsActionPressed("weapon_next"))
		{
			CycleWeapon(1);
			GetViewport().SetInputAsHandled();
		}
	}

	private void Boot()
	{
		TryAddWeapon("res://Assets/Combat/Weapons/Zweihander_Equip.tres");
		TryAddWeapon("res://Assets/Combat/Weapons/BanditAxe_Equip.tres");
		TryAddWeapon("res://Assets/Combat/Weapons/OneHandedSword_Equip.tres");
		_armor = GD.Load<ArmorEquipData>("res://Assets/Combat/Armor/CursedKnightPlate_Equip.tres");
		_weaponIndex = 0;
		_drawn = false;
		ApplyAll(playEquipClip: false);
	}

	private void TryAddWeapon(string path)
	{
		WeaponEquipData? data = GD.Load<WeaponEquipData>(path);
		if (data != null)
		{
			_weapons.Add(data);
		}
		else
		{
			GD.PushWarning($"PlayerLoadout: missing '{path}'.");
		}
	}

	private bool IsBusy =>
		_pendingSheathe
		|| _pendingDraw
		|| (Attack != null && Attack.IsBusy)
		|| (AnimDriver != null && (AnimDriver.IsPlayingAttack || AnimDriver.IsHurt));

	private void ToggleDrawn()
	{
		if (IsBusy || CurrentWeapon() == null)
		{
			return;
		}

		if (_drawn)
		{
			WeaponEquipData? weapon = CurrentWeapon();
			string clip = ClipOr(weapon?.SheatheClip, SheatheFallback(weapon));
			_unarmedCombat = false;
			_unarmedCombatTimer = 0f;
			if (AnimDriver != null && AnimDriver.TryPlayEquipClip(clip))
			{
				BeginPendingSheathe(weapon);
				return;
			}

			string drawClip = ClipOr(weapon?.DrawClip, DrawFallback(weapon));
			if (AnimDriver != null && AnimDriver.TryPlayEquipClipBackwards(drawClip))
			{
				BeginPendingSheathe(weapon);
				return;
			}

			CommitSheathe();
			return;
		}

		BeginDraw();
	}

	private void BeginPendingSheathe(WeaponEquipData? weapon)
	{
		_pendingSheathe = true;
		_sheatheSocketBound = false;
		_sheatheBindNorm = Mathf.Clamp(weapon?.SheatheBindNorm ?? 0.84f, 0f, 1f);
		ApplyUnarmedClipSet();
		RefreshHud();
	}

	private void BeginDraw()
	{
		WeaponEquipData? weapon = CurrentWeapon();
		if (weapon == null)
		{
			return;
		}

		_unarmedCombat = false;
		_unarmedCombatTimer = 0f;
		ApplyDrawnClips(weapon);
		string clip = ClipOr(weapon.DrawClip, DrawFallback(weapon));
		if (AnimDriver != null && AnimDriver.TryPlayEquipClip(clip))
		{
			_pendingDraw = true;
			_drawBindNorm = Mathf.Clamp(weapon.DrawBindNorm, 0f, 1f);
			WeaponAttach?.SetDrawn(false);
			RefreshHud();
			return;
		}

		_drawn = true;
		ApplyAll(playEquipClip: false);
	}

	private void TickPendingDraw()
	{
		if (!_pendingDraw)
		{
			return;
		}

		if (AnimDriver == null || !AnimDriver.IsPlayingAttack)
		{
			CommitDrawBind();
			return;
		}

		float len = AnimDriver.GetCurrentLength();
		if (len <= 0.05f)
		{
			return;
		}

		if (AnimDriver.GetCurrentPosition() / len >= _drawBindNorm)
		{
			CommitDrawBind();
		}
	}

	private void CommitDrawBind()
	{
		_pendingDraw = false;
		_drawn = true;
		WeaponAttach?.SetDrawn(true);
		if (Attack != null)
		{
			Attack.Damage = CurrentWeapon()?.BaseDamageN ?? UnarmedDamageN;
		}

		RefreshHud();
	}

	private void TickPendingSheathe()
	{
		if (!_sheatheSocketBound)
		{
			bool clipEnded = AnimDriver == null || !AnimDriver.IsPlayingAttack;
			if (clipEnded || SheatheClipNorm() >= _sheatheBindNorm)
			{
				BindSheatheSocket();
			}
		}

		if (AnimDriver == null || !AnimDriver.IsPlayingAttack)
		{
			_pendingSheathe = false;
			if (!_sheatheSocketBound)
			{
				CommitSheathe();
			}
		}
	}

	private float SheatheClipNorm()
	{
		if (AnimDriver == null)
		{
			return 1f;
		}

		float len = AnimDriver.GetCurrentLength();
		if (len <= 0.05f)
		{
			return 0f;
		}

		float t = AnimDriver.GetCurrentPosition() / len;
		if (AnimDriver.IsEquipReverse)
		{
			t = 1f - t;
		}

		return t;
	}

	private void BindSheatheSocket()
	{
		_sheatheSocketBound = true;
		_drawn = false;
		WeaponAttach?.SetDrawn(false);
		if (Attack != null)
		{
			Attack.Damage = UnarmedDamageN;
		}

		RefreshHud();
	}

	private void CommitSheathe()
	{
		_drawn = false;
		_unarmedCombat = false;
		_unarmedCombatTimer = 0f;
		ApplyAll(playEquipClip: false);
	}

	public void SetDrawnImmediate(bool drawn)
	{
		_pendingSheathe = false;
		_pendingDraw = false;
		_sheatheSocketBound = false;
		_unarmedCombat = false;
		_unarmedCombatTimer = 0f;
		_drawn = drawn && CurrentWeapon() != null;
		if (Attack != null)
		{
			Attack.Damage = _drawn ? CurrentWeapon()?.BaseDamageN ?? UnarmedDamageN : UnarmedDamageN;
		}

		WeaponAttach?.SetDrawn(_drawn);
		if (_drawn)
		{
			ApplyDrawnClips(CurrentWeapon());
		}
		else if (AnimDriver != null)
		{
			AnimDriver.HeavyHitWindows = System.Array.Empty<Vector2>();
			AnimDriver.LightHitWindows = System.Array.Empty<Vector2>();
			AnimDriver.AttackSpeedScale = 1f;
			AnimDriver.HeavyAttackSpeedScale = 1f;
			ApplyUnarmedClipSet();
		}

		RefreshHud();
	}

	public void CycleWeaponForTune(int delta)
	{
		if (_weapons.Count < 2)
		{
			return;
		}

		CycleWeapon(delta, ignoreBusy: true);
	}

	private void CycleWeapon(int delta, bool ignoreBusy = false)
	{
		if (_weapons.Count < 2 || (!ignoreBusy && IsBusy))
		{
			return;
		}

		int next = (_weaponIndex + delta) % _weapons.Count;
		if (next < 0)
		{
			next += _weapons.Count;
		}

		WeaponEquipData candidate = _weapons[next];
		if (!CanCarry(candidate, _armor))
		{
			TransientHint = "Too heavy";
			RefreshHud();
			return;
		}

		_pendingSheathe = false;
		_pendingDraw = false;
		_sheatheSocketBound = false;
		_drawn = false;
		_unarmedCombat = false;
		_unarmedCombatTimer = 0f;
		_weaponIndex = next;
		ApplyAll(playEquipClip: false);
	}

	private bool CanCarry(WeaponEquipData? weapon, ArmorEquipData? armor)
	{
		float w = (weapon?.Weight ?? 0f) + (armor?.Weight ?? 0f);
		return w <= MaxCarryWeight + 0.001f;
	}

	private WeaponEquipData? CurrentWeapon() =>
		_weaponIndex >= 0 && _weaponIndex < _weapons.Count ? _weapons[_weaponIndex] : null;

	private void ApplyAll(bool playEquipClip)
	{
		WeaponEquipData? weapon = CurrentWeapon();
		if (weapon != null && !CanCarry(weapon, _armor))
		{
			TransientHint = "Too heavy";
			RefreshHud();
			return;
		}

		TransientHint = "";
		if (Health != null)
		{
			Health.PhysicalResist = _armor?.PhysicalResist ?? 0f;
		}

		if (Attack != null)
		{
			Attack.Damage = _drawn ? weapon?.BaseDamageN ?? UnarmedDamageN : UnarmedDamageN;
		}

		WeaponAttach?.Equip(weapon);
		WeaponAttach?.SetDrawn(_drawn);

		if (_drawn)
		{
			ApplyDrawnClips(weapon);
			if (playEquipClip)
			{
				string clip = ClipOr(weapon?.DrawClip, DrawFallback(weapon));
				AnimDriver?.TryPlayEquipClip(clip);
			}
		}
		else
		{
			if (AnimDriver != null)
			{
				AnimDriver.HeavyHitWindows = System.Array.Empty<Vector2>();
			AnimDriver.LightHitWindows = System.Array.Empty<Vector2>();
			AnimDriver.AttackSpeedScale = 1f;
			AnimDriver.HeavyAttackSpeedScale = 1f;
			}

			ApplyUnarmedClipSet();
		}

		RefreshHud();
	}

	private void ApplyDrawnClips(WeaponEquipData? weapon)
	{
		bool oneHand = weapon?.Stance == WeaponStance.OneHand;
		if (AnimDriver != null)
		{
			Vector2[] heavyWindows = weapon?.HeavyHitWindows ?? System.Array.Empty<Vector2>();
			Vector2[] lightWindows = weapon?.LightHitWindows ?? System.Array.Empty<Vector2>();
			if (heavyWindows.Length == 0 && weapon?.Stance == WeaponStance.TwoHandGreat)
			{
				heavyWindows =
				[
					new Vector2(0.16f, 0.28f),
					new Vector2(0.42f, 0.55f),
					new Vector2(0.68f, 0.81f)
				];
			}

			if (weapon?.Stance == WeaponStance.OneHand)
			{
				if (lightWindows.Length == 0)
				{
					lightWindows = [new Vector2(0.40f, 0.56f)];
				}

				if (heavyWindows.Length == 0)
				{
					heavyWindows =
					[
						new Vector2(0.18f, 0.30f),
						new Vector2(0.40f, 0.52f),
						new Vector2(0.61f, 0.74f)
					];
				}
			}

			AnimDriver.HeavyHitWindows = heavyWindows;
			AnimDriver.LightHitWindows = lightWindows;
			AnimDriver.AttackSpeedScale = weapon?.AttackSpeedScale > 0.01f ? weapon.AttackSpeedScale : 1f;
			AnimDriver.HeavyAttackSpeedScale = weapon?.HeavyAttackSpeedScale > 0.01f
				? weapon.HeavyAttackSpeedScale
				: 1f;
		}

		ApplyWeaponClipSet(weapon);
	}

	private void ApplyWeaponClipSet(WeaponEquipData? weapon)
	{
		bool oneHand = weapon?.Stance == WeaponStance.OneHand;
		AnimDriver?.ApplyClipSet(
			PickClip(weapon?.IdleClip, oneHand ? "1h_idle" : "idle"),
			PickClip(weapon?.WalkClip, oneHand ? "1h_walk" : "walk"),
			PickClip(weapon?.RunClip, oneHand ? "1h_run" : "run"),
			PickClip(weapon?.AttackClip, oneHand ? "1h_attack" : "attack"),
			PickClip(weapon?.HeavyAttackClip, oneHand ? "1h_attack_heavy" : "attack_heavy"),
			PickClip(weapon?.HurtClip, oneHand ? "1h_stagger" : "stagger"));
	}

	private void ApplyUnarmedClipSet()
	{
		string punch = PickClip("unarmed_attack", "attack");
		string cross = PickClip("unarmed_attack_alt", punch);
		string kick = PickClip("unarmed_attack_heavy", "attack_heavy");
		string stagger = PickClip("unarmed_stagger", "stagger");
		if (_unarmedCombat)
		{
			AnimDriver?.ApplyClipSet(
				PickClip("unarmed_idle", "calm_idle"),
				PickClip("calm_walk", "walk"),
				PickClip("calm_run", "run"),
				punch,
				kick,
				stagger,
				cross);
			return;
		}

		AnimDriver?.ApplyClipSet(
			PickClip("calm_idle", "idle"),
			PickClip("calm_walk", "walk"),
			PickClip("calm_run", "run"),
			punch,
			kick,
			stagger,
			cross);
	}

	private void TickUnarmedCombatTimeout(float dt)
	{
		if (_drawn || !_unarmedCombat)
		{
			return;
		}

		if (Attack != null && Attack.IsBusy)
		{
			return;
		}

		if (AnimDriver != null && AnimDriver.IsPlayingAttack)
		{
			return;
		}

		_unarmedCombatTimer += dt;
		if (_unarmedCombatTimer < UnarmedCombatTimeout)
		{
			return;
		}

		_unarmedCombat = false;
		_unarmedCombatTimer = 0f;
		ApplyUnarmedClipSet();
		RefreshHud();
	}

	private string PickClip(string? preferred, string fallback)
	{
		if (AnimDriver != null && AnimDriver.HasClip(preferred ?? ""))
		{
			return preferred!;
		}

		if (AnimDriver != null && AnimDriver.HasClip(fallback))
		{
			return fallback;
		}

		return "idle";
	}

	private void RefreshHud()
	{
		WeaponEquipData? weapon = CurrentWeapon();
		float weight = (weapon?.Weight ?? 0f) + (_armor?.Weight ?? 0f);
		int n = Attack?.Damage ?? UnarmedDamageN;
		float resist = Health?.PhysicalResist ?? 0f;
		string stance = StanceLabel();
		string weaponName = weapon?.DisplayName ?? "—";
		string armorName = _armor?.DisplayName ?? "—";
		string hint = string.IsNullOrEmpty(TransientHint) ? "" : $"  [{TransientHint}]";
		HudLine =
			$"N {n}  Resist {resist:0%}  {weight:0}/{MaxCarryWeight:0}  {weaponName}  {armorName}  {stance}{hint}";
	}

	private string StanceLabel()
	{
		if (_drawn && !_pendingSheathe)
		{
			return "Combat";
		}

		return _unarmedCombat ? "Combat" : "Calm";
	}

	private static string ClipOr(string? authored, string fallback) =>
		string.IsNullOrEmpty(authored) ? fallback : authored;

	private static string DrawFallback(WeaponEquipData? weapon) =>
		weapon?.Stance == WeaponStance.OneHand ? "1h_draw" : "2h_draw";

	private static string SheatheFallback(WeaponEquipData? weapon) =>
		weapon?.Stance == WeaponStance.OneHand ? "1h_sheathe" : "2h_sheathe";
}
