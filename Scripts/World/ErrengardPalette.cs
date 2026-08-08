using Godot;

namespace DirgeOfWithering;

/// <summary>
/// Базовая палитра Эрренгарда для Vertical Slice (этап 2.1).
/// Пепел / багровый / болезненно-жёлтый — без финальных текстур.
/// </summary>
public static class ErrengardPalette
{
	public static readonly Color Background = new(0.06f, 0.055f, 0.06f);
	public static readonly Color Ambient = new(0.28f, 0.26f, 0.3f);

	public static readonly Color FloorAsh = new(0.16f, 0.15f, 0.16f);
	public static readonly Color Stone = new(0.22f, 0.2f, 0.23f);
	public static readonly Color StoneDark = new(0.14f, 0.13f, 0.15f);
	public static readonly Color IchorCrimson = new(0.38f, 0.08f, 0.1f);
	public static readonly Color BoneYellow = new(0.55f, 0.45f, 0.28f);

	public static readonly Color KeyLight = new(0.78f, 0.68f, 0.42f);
	public static readonly Color FillLight = new(0.35f, 0.4f, 0.48f);
}
