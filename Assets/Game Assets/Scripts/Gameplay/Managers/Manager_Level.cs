// Turns a level number (1-based) into a LevelDefinition.
public class Manager_Level : Singleton<Manager_Level>
{
    private ILevelSource _source;

    public override void Awake2()
    {
        var fileRef = FileRef.Instance;
        _source = new ProceduralLevelSource(fileRef.PlayableShapes, fileRef.PlayableColors);
    }

    public LevelDefinition Load(int levelNumber) => _source.Load(levelNumber);
}
