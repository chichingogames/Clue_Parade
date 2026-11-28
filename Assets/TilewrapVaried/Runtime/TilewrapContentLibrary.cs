using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chichingo.TilewrapVaried
{
    [Serializable] public sealed class SpriteEntry { public string id; public Sprite sprite; }
    [Serializable] public sealed class RigEntry { public string id; public TilewrapRigView prefab; }
    [Serializable] public sealed class AudioEntry { public string id; public AudioClip clip; public float volume; }
    public sealed class TilewrapContentLibrary : ScriptableObject
    {
        public string contentId;
        public SpriteEntry[] sprites;
        public RigEntry[] rigs;
        public AudioEntry[] audio;
        public TextAsset[] levels;
        public TextAsset uiLayout, hintDistances, campaign;
        public Font regularFont, boldFont;
        private Dictionary<string, Sprite> spriteMap;
        private Dictionary<string, TilewrapRigView> rigMap;

        private CampaignData campaignData;
        public CampaignData GetCampaign()
        {
            if (campaignData == null)
            {
                if (campaign == null) throw new InvalidOperationException("Missing campaign asset. Run the content importer.");
                campaignData = JsonUtility.FromJson<CampaignData>(campaign.text);
                if (campaignData == null || campaignData.levelCount != levels.Length)
                    throw new FormatException("Campaign and level counts differ.");
            }
            return campaignData;
        }

        public Sprite GetSprite(string id)
        {
            if (spriteMap == null)
            {
                spriteMap = new Dictionary<string, Sprite>(StringComparer.Ordinal);
                foreach (var entry in sprites) spriteMap.Add(entry.id, entry.sprite);
            }
            return spriteMap[id];
        }
        public TilewrapRigView CreateRig(string id, RectTransform frame, string[] board)
        {
            if (rigMap == null)
            {
                rigMap = new Dictionary<string, TilewrapRigView>(StringComparer.Ordinal);
                foreach (var entry in rigs) rigMap.Add(entry.id, entry.prefab);
            }
            var instance = Instantiate(rigMap[id]);
            instance.FitInto(frame);
            instance.ApplyBoard(board);
            return instance;
        }
        public TilewrapCaseData GetCase(int id)
        {
            foreach (var source in levels)
            {
                var data = TilewrapCaseData.Parse(source);
                if (data.id == id) return data;
            }
            throw new KeyNotFoundException("Missing Tilewrap case " + id);
        }
    }
}
