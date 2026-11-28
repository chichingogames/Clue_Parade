using UnityEngine;

// The content pack's 1080x1920 reference screen (the old pack's ui_layout.json regions; the v5 pack's Data/presentation.json uses the same 1080x1920 frame
// and geometry) in world units: 1 unit = 100 reference px, origin at the screen's centre, x right,
// y up. Manager_Board fits this 10.8 x 19.2 area uniformly into the safe area, as UI_ReferenceFit
// fits the HUD, so the world-space board lines up with the uGUI HUD around it (the subjects over
// its columns, the delivery banner over its rows). px (x, y from top-left) -> ((x - 540) / 100, (960 - y) / 100). These are the art's
// positions, not tuning.
public static class ReferenceLayout
{
    public const float Width = 10.8f;
    public const float Height = 19.2f;

    // board: x 105, y 800, 870 x 870 px (centred: the any-row HUD has no category tabs beside it)
    // -> centre (540, 1235) px; cells 290 px (geometry.cellSize).
    public static readonly Vector2 BoardCenter = new Vector2(0f, -2.75f);
    public const float CellSize = 2.90f;
    // boardFrame: 15 px left / right and 10 px top / bottom around the grid.
    public static readonly Vector2 FramePadding = new Vector2(0.15f, 0.10f);
    public const float DividerWidth = 0.026f;

    // Inside a cell (px from the cell's top-left): the item art centred in a 250 x 240 box (tiles show
    // no name), the check [238, 14, 44, 44]. Offsets are from the cell's centre.
    public static readonly Vector2 ItemArtOffset = Vector2.zero;
    public static readonly Vector2 ItemArtBox = new Vector2(2.50f, 2.40f);
    public static readonly Vector2 CheckOffset = new Vector2(1.15f, 1.09f);
    public const float CheckSize = 0.44f;
}
