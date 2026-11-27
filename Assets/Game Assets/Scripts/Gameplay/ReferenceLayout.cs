using UnityEngine;

// The Tilewrap handoff's 1080x1920 reference screen (game_config.json ui.layoutTopLeft) in world
// units: 1 unit = 100 reference px, origin at the screen's centre, x right, y up. Manager_Board fits
// this 10.8 x 19.2 area uniformly into the safe area, as UI_ReferenceFit fits the HUD, so the
// world-space board and crew line up with the uGUI HUD. px (x, y from top-left) -> ((x - 540) / 100,
// (960 - y) / 100). These are the art's positions, not tuning.
public static class ReferenceLayout
{
    public const float Width = 10.8f;
    public const float Height = 19.2f;

    // Board: x 210, y 816, 720 x 720 px -> centre (570, 1176) px. The frame is inset 20 px; the inner
    // 680 px square is split in equal thirds (cells 226.7 px = ItemTile size, AssetBuilder.CellSize).
    public static readonly Vector2 BoardCenter = new Vector2(0.30f, -2.16f);
    public const float FramePadding = 0.20f;
    public const float DividerWidth = 0.026f;

    // Category tabs beside the rows: 112 x 124 px, overlapping the frame's left edge by 6 px.
    public static readonly Vector2 TagSize = new Vector2(1.12f, 1.24f);
    public const float TagOffset = FramePadding + 0.56f - 0.06f;   // grid edge -> tab centre

    // A matched cell's check badge: 50 px, at the cell's bottom-right.
    public static readonly Vector2 CheckOffset = new Vector2(0.80f, -0.80f);
    public const float CheckSize = 0.50f;

    // Crew (y 528..744 px) and name tabs (y 744..816 px), in CharacterRow's local space (= world).
    // Customers stand with their feet at y 752 px (hidden behind the tab), 230 px tall.
    public const float PortraitBottom = 2.08f;
    public const float CustomerHeight = 2.30f;
    // A Customer's registered layers are 600 x 900 px at 100 px per unit: 9 units tall unscaled.
    public const float CustomerArtHeight = 9f;
    public const float PlateY = 1.80f;
    public static readonly Vector2 PlateSize = new Vector2(2.18f, 0.72f);
    public const float PlateSymbolSize = 0.38f;
    public static readonly Vector2 PlateNameSize = new Vector2(1.20f, 0.52f);
}
