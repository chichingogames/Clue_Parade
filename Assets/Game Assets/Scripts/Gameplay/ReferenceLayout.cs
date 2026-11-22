using UnityEngine;

// The Tilewrap art pack's 1080x1920 reference screen (Layout_1080x1920.csv) in world units:
// 1 unit = 100 reference px, origin at the screen's centre, x right, y up. Manager_Board fits this
// 10.8 x 19.2 area uniformly into the safe area, as the HUD fits its 1080x1920 content, so the
// world-space board and the uGUI HUD line up. These are the art's positions, not tuning.
public static class ReferenceLayout
{
    public const float Width = 10.8f;
    public const float Height = 19.2f;

    // Grid (BoardClip): x 136, y 820, 888 x 768 px -> centre (580, 1204) px.
    public static readonly Vector2 BoardCenter = new Vector2(0.40f, -2.44f);

    // Frame (BoardFrame): 940 x 820 px around the 888 x 768 grid, i.e. 26 px past each side.
    public const float FramePadding = 0.26f;

    // Category tags: 122 x 88 px, centred 60 px left of the grid's left edge.
    public const float TagOffset = 0.60f;
    public static readonly Vector2 TagSize = new Vector2(1.22f, 0.88f);

    // A confirmed row's check: 58 x 58 px, centred 112 px right of and 89 px above the tile centre.
    public static readonly Vector2 CheckOffset = new Vector2(1.12f, 0.89f);
    public const float CheckSize = 0.58f;
}
