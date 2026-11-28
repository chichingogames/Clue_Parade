using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Chichingo.TilewrapMystery.Editor
{
    public static class TilewrapContentImporter
    {
        const string Root="Assets/TilewrapMystery";
        [MenuItem("Tools/Chichingo/Tilewrap/Build current 30-level content")]
        public static void Build()
        {
            var contentAsset=AssetDatabase.LoadAssetAtPath<TextAsset>(Root+"/Data/content.json");
            if(contentAsset==null)throw new FileNotFoundException("Missing current Tilewrap content.json");
            var content=JsonUtility.FromJson<ContentData>(contentAsset.text);
            if(content==null||content.schemaVersion!=5)throw new FormatException("Expected schema 5 content.");
            var campaignAsset=AssetDatabase.LoadAssetAtPath<TextAsset>(content.levelsPath);
            var campaign=JsonUtility.FromJson<CampaignData>(campaignAsset.text);
            if(campaign.levels.Length!=30)throw new FormatException("Expected levels 1–30.");
            foreach(var level in campaign.levels) {
                level.Validate();var state=TilewrapSwapPuzzle.Create(level);
                foreach(var move in level.referenceSolution) {SwapState next;List<SwapEvent> events;if(!TilewrapSwapPuzzle.TrySwap(state,level,move.a,move.b,out next,out events))throw new FormatException("Invalid route in level "+level.id);state=next;}
                if(state.status!="won"||state.moves!=level.referenceMoveCount)throw new FormatException("Route did not win level "+level.id);
            }
            var sprites=new Dictionary<string,Sprite>(StringComparer.Ordinal);
            foreach(var record in content.sprites) {
                var ti=AssetImporter.GetAtPath(record.path)as TextureImporter;if(ti==null)throw new FileNotFoundException(record.path);
                ti.textureType=TextureImporterType.Sprite;ti.spriteImportMode=SpriteImportMode.Single;ti.spritePixelsPerUnit=100;
                ti.alphaIsTransparency=true;ti.sRGBTexture=true;ti.mipmapEnabled=false;ti.isReadable=false;ti.npotScale=TextureImporterNPOTScale.None;ti.filterMode=FilterMode.Bilinear;ti.wrapMode=TextureWrapMode.Clamp;
                int max=record.maxSize>0?record.maxSize:record.path.Contains("/UI/")||record.path.Contains("/Background/")?2048:512;ti.maxTextureSize=max;
                var settings=new TextureImporterSettings();ti.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;settings.spriteAlignment=(int)SpriteAlignment.Custom;
                settings.spritePivot=new Vector2(record.pivot[0],record.pivot[1]);settings.spriteBorder=new Vector4(record.border[0],record.border[1],record.border[2],record.border[3]);ti.SetTextureSettings(settings);
                foreach(string platform in new[]{"Android","iPhone"}) {var ps=ti.GetPlatformTextureSettings(platform);ps.name=platform;ps.overridden=true;ps.maxTextureSize=max;ps.format=TextureImporterFormat.ASTC_4x4;ps.compressionQuality=100;ti.SetPlatformTextureSettings(ps);}
                ti.SaveAndReimport();sprites.Add(record.key,AssetDatabase.LoadAssetAtPath<Sprite>(record.path));
            }
            Directory.CreateDirectory(Root+"/Resources/TilewrapMystery/Rigs");Directory.CreateDirectory(Root+"/Generated");AssetDatabase.Refresh();
            foreach(var rig in content.rigs)BuildRig(rig,sprites);
            string path=Root+"/Generated/TilewrapContentLibrary.asset";
            var library=AssetDatabase.LoadAssetAtPath<TilewrapContentLibrary>(path);
            if(library==null) {library=ScriptableObject.CreateInstance<TilewrapContentLibrary>();AssetDatabase.CreateAsset(library,path);}
            library.contentJson=contentAsset;library.campaignJson=campaignAsset;library.presentationJson=AssetDatabase.LoadAssetAtPath<TextAsset>(content.presentationPath);library.ResetCaches();EditorUtility.SetDirty(library);
            BuildBackground(library);BuildAudio(content);
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();Debug.Log("Tilewrap: imported all 30 current levels, 90 rigs, background and audio. Run the device playtest.");
        }
        static void BuildRig(RigData data,Dictionary<string,Sprite> sprites)
        {
            var root=new GameObject(data.id,typeof(RectTransform),typeof(TilewrapRigView));
            try {
                var rt=(RectTransform)root.transform;rt.sizeDelta=new Vector2(data.width,data.height);var view=root.GetComponent<TilewrapRigView>();view.nativeSize=rt.sizeDelta;var bindings=new List<RigBinding>();
                Array.Sort(data.layers,(a,b)=>a.order.CompareTo(b.order));
                foreach(var layer in data.layers) {
                    var go=new GameObject(layer.sprite,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));var child=(RectTransform)go.transform;child.SetParent(rt,false);
                    child.anchorMin=child.anchorMax=child.pivot=new Vector2(0,1);child.anchoredPosition=new Vector2(layer.rect[0],-layer.rect[1]);child.sizeDelta=new Vector2(layer.rect[2],layer.rect[3]);
                    var image=go.GetComponent<Image>();image.sprite=sprites[layer.sprite];image.color=Color.white;image.preserveAspect=false;image.raycastTarget=false;go.SetActive(layer.alwaysVisible);
                    bindings.Add(new RigBinding{layer=go,itemId=layer.itemId,alwaysVisible=layer.alwaysVisible});
                }
                view.bindings=bindings.ToArray();PrefabUtility.SaveAsPrefabAsset(root,Root+"/Resources/TilewrapMystery/Rigs/"+data.id+".prefab");
            }finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        static void BuildBackground(TilewrapContentLibrary library)
        {
            var root=new GameObject("TilewrapAmbientBackground",typeof(RectTransform),typeof(Canvas),typeof(TilewrapAmbientBackground));
            try {var rt=(RectTransform)root.transform;rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.sizeDelta=Vector2.zero;root.GetComponent<TilewrapAmbientBackground>().library=library;PrefabUtility.SaveAsPrefabAsset(root,Root+"/Generated/TilewrapAmbientBackground.prefab");}
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        static void BuildAudio(ContentData content)
        {
            var clips=new List<ClipBinding>();
            foreach(var record in content.audio) {
                var ai=AssetImporter.GetAtPath(record.path)as AudioImporter;if(ai==null)throw new FileNotFoundException(record.path);
                var settings=ai.defaultSampleSettings;settings.loadType=record.loop?AudioClipLoadType.Streaming:AudioClipLoadType.DecompressOnLoad;settings.compressionFormat=record.loop?AudioCompressionFormat.Vorbis:AudioCompressionFormat.PCM;settings.quality=.85f;settings.sampleRateSetting=AudioSampleRateSetting.PreserveSampleRate;
                ai.defaultSampleSettings=settings;ai.forceToMono=!record.loop;ai.loadInBackground=record.loop;ai.SaveAndReimport();
                clips.Add(new ClipBinding{key=record.key,clip=AssetDatabase.LoadAssetAtPath<AudioClip>(record.path),volume=record.volume});
            }
            var root=new GameObject("TilewrapAudio",typeof(TilewrapAudioController));
            try {
                var control=root.GetComponent<TilewrapAudioController>();control.clips=clips.ToArray();
                var music=new GameObject("Music",typeof(AudioSource));music.transform.SetParent(root.transform,false);control.musicSource=music.GetComponent<AudioSource>();control.musicSource.playOnAwake=false;control.musicSource.loop=true;control.musicSource.spatialBlend=0;
                var effects=new GameObject("Effects",typeof(AudioSource));effects.transform.SetParent(root.transform,false);control.effectsSource=effects.GetComponent<AudioSource>();control.effectsSource.playOnAwake=false;control.effectsSource.spatialBlend=0;
                PrefabUtility.SaveAsPrefabAsset(root,Root+"/Generated/TilewrapAudio.prefab");
            }finally{UnityEngine.Object.DestroyImmediate(root);}
        }
    }
}
