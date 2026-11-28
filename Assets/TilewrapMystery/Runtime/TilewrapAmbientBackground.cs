using System;
using UnityEngine;
using UnityEngine.UI;

namespace Chichingo.TilewrapMystery
{
    public sealed class TilewrapAmbientBackground : MonoBehaviour
    {
        public TilewrapContentLibrary library;
        public bool motionEnabled = true;
        private RectTransform design;
        private Image baseImage;
        private RectTransform[] layers;
        private Image[] images;
        private BackgroundData data;
        private float elapsed;
        private Color tint = Color.white, targetTint = Color.white;
        void Start() { Build(); }
        void Build()
        {
            if (library == null || design != null) return;
            data = JsonUtility.FromJson<PresentationData>(library.presentationJson.text).background;
            design = new GameObject("Background design", typeof(RectTransform)).GetComponent<RectTransform>();
            design.SetParent(transform, false); design.anchorMin = design.anchorMax = design.pivot = new Vector2(.5f,.5f);
            design.sizeDelta = new Vector2(1080,1920);
            baseImage = MakeImage(data.baseSprite, new float[]{0,0,1080,1920}, 1);
            layers = new RectTransform[data.layers.Length]; images = new Image[data.layers.Length];
            for (int i=0;i<layers.Length;i++) { images[i]=MakeImage(data.layers[i].sprite,data.layers[i].rect,data.layers[i].opacity); layers[i]=images[i].rectTransform; }
            Resize(); SetMotionEnabled(motionEnabled);
        }
        Image MakeImage(string key, float[] rect, float opacity)
        {
            var go = new GameObject(key,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));
            var rt = (RectTransform)go.transform;rt.SetParent(design,false);rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(0,1);
            rt.anchoredPosition=new Vector2(rect[0],-rect[1]);rt.sizeDelta=new Vector2(rect[2],rect[3]);
            var image=go.GetComponent<Image>();image.sprite=library.GetSprite(key);image.raycastTarget=false;image.color=new Color(1,1,1,opacity);return image;
        }
        void Resize()
        {
            if (design == null) return;
            var rect=((RectTransform)transform).rect;design.localScale=Vector3.one*Mathf.Max(rect.width/1080,rect.height/1920);
        }
        void OnRectTransformDimensionsChange() { Resize(); }
        public void SetMotionEnabled(bool value)
        {
            motionEnabled=value;if(layers==null)return;
            for(int i=0;i<layers.Length;i++) { var p=data.layers[i];layers[i].anchoredPosition=new Vector2(p.rect[0],-p.rect[1]);images[i].color=new Color(1,1,1,!value&&p.sprite=="ambient_mote"?0:p.opacity); }
        }
        public void SetChapter(int chapter)
        {
            Build();if(data==null)return;
            Color parsed;if(ColorUtility.TryParseHtmlString(data.chapterTints[Mathf.Clamp(chapter-1,0,data.chapterTints.Length-1)],out parsed))targetTint=parsed;
            if(!motionEnabled) { tint=targetTint;baseImage.color=tint; }
        }
        void Update()
        {
            if(design==null||!motionEnabled)return;
            elapsed+=Time.unscaledDeltaTime;tint=Color.Lerp(tint,targetTint,Time.unscaledDeltaTime*.4f);baseImage.color=tint;
            for(int i=0;i<layers.Length;i++) {
                var p=data.layers[i];float phase=elapsed/Mathf.Max(1,p.period)*Mathf.PI*2+p.phase;
                layers[i].anchoredPosition=new Vector2(p.rect[0]+Mathf.Sin(phase)*p.dx,-p.rect[1]+Mathf.Cos(phase*.83f)*p.dy);
                if(p.sprite=="ambient_leaf")layers[i].localRotation=Quaternion.Euler(0,0,Mathf.Sin(phase)*2);
                images[i].color=new Color(1,1,1,p.opacity*(.92f+.08f*Mathf.Sin(phase)));
            }
        }
    }
}
