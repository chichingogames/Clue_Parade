// Turns a level number (1-based) into a LevelDefinition: the authored JSON level if there is one,
// otherwise a generated level (so play continues past the last authored file).
public class Manager_Level : Singleton<Manager_Level>
{
    private ILevelSource _authored;
    private ILevelSource _generated;

    public override void Awake2()
    {
        var fileRef = FileRef.Instance;
        _authored = new JsonLevelSource(fileRef.PlayableShapes, fileRef.PlayableColors);
        _generated = new ProceduralLevelSource(fileRef.PlayableShapes, fileRef.PlayableColors);
    }

    public LevelDefinition Load(int levelNumber) => _authored.Load(levelNumber) ?? _generated.Load(levelNumber);
}
