// The lesson copy and behavior of the four tutorial levels (Tilewrap tutorial pack v1.5).
// The guided swipe itself is not here: it is the level file's "tutorial" axis + its first solution move.
// Levels without a lesson (5+) show the HUD's regular goal text and no callouts.
public class TutorialLesson
{
    public string Title;          // before the first swipe
    public string Body;
    public string MovedTitle;     // after a swipe; on a guided level, the confirmation shown before the win card.
    public string MovedBody;      // {used} and {left} are replaced with the move counts. Null = keep Title / Body.
    public bool IdleHint;         // pulse the target icons after Tuning.TutorialIdleHint seconds without a move
    public bool PulseMoves;       // pulse the moves pill on the first spent move
}

public static class TutorialLessons
{
    private static readonly TutorialLesson[] Lessons =
    {
        null,
        new TutorialLesson   // 1 · shape (row)
        {
            Title = "Swipe the middle row right",
            Body = "The square on the left labels this row. Swipe right to wrap the coral square to the left.",
            MovedTitle = "Coral square matched!",
            MovedBody = "It fits the square row and the coral column.",
        },
        new TutorialLesson   // 2 · color (column)
        {
            Title = "Swipe the middle column down",
            Body = "The teal marker above labels this column. Swipe down to wrap the teal circle to the top.",
            MovedTitle = "Teal circle matched!",
            MovedBody = "It fits the circle row and the teal column.",
        },
        new TutorialLesson   // 3 · both targets
        {
            Title = "Find where shape and color meet",
            Body = "A teal square goes where the square row meets the teal column.",
            MovedTitle = "Find where shape and color meet",
            MovedBody = "A checkmark means a tile matches both targets. Keep going until every tile has one.",
            IdleHint = true,
        },
        new TutorialLesson   // 4 · move budget
        {
            Title = "Practice with a move limit",
            Body = "Each swipe uses 1 move. Undo gives it back.",
            MovedTitle = "{used} used · {left} left",
            MovedBody = "Each swipe uses 1 move. Undo gives it back.",
            PulseMoves = true,
        },
    };

    public static TutorialLesson For(int levelNumber) =>
        levelNumber > 0 && levelNumber < Lessons.Length ? Lessons[levelNumber] : null;
}
