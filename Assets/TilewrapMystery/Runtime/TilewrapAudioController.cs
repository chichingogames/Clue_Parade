using System;
using UnityEngine;

namespace Chichingo.TilewrapMystery
{
    [Serializable] public sealed class ClipBinding { public string key; public AudioClip clip; public float volume; }
    public sealed class TilewrapAudioController : MonoBehaviour
    {
        public AudioSource musicSource, effectsSource;
        public ClipBinding[] clips;
        public bool musicEnabled = true, effectsEnabled = true;
        [Range(0,1)] public float musicGain = .6f, effectsGain = .8f;
        private float duckUntil;
        private bool paused, wasPlaying, musicStarted;
        void Start() { PlayMusic(); }
        public void PlayMusic()
        {
            if(musicSource==null)return;
            foreach(var binding in clips)if(binding.key=="mystery_loop") { if(musicSource.clip!=binding.clip) { musicSource.clip=binding.clip;musicStarted=false; }break; }
            musicSource.loop=true;musicSource.spatialBlend=0;musicSource.volume=musicGain;
            if(musicEnabled&&!paused&&!musicSource.isPlaying) { if(musicStarted)musicSource.UnPause();else {musicSource.Play();musicStarted=true;} }
        }
        public void SetMusicEnabled(bool value) { musicEnabled=value;if(value)PlayMusic();else if(musicSource!=null)musicSource.Pause(); }
        public void SetEffectsEnabled(bool value) { effectsEnabled=value; }
        public void PlayEvent(string key)
        {
            if(!effectsEnabled||effectsSource==null)return;
            foreach(var binding in clips)if(binding.key==key) { effectsSource.PlayOneShot(binding.clip,binding.volume*effectsGain);if(key=="level_win"||key=="case_closed"||key=="hint")duckUntil=Time.unscaledTime+1.4f;return; }
            Debug.LogWarning("Unknown Tilewrap sound: "+key);
        }
        void Update()
        {
            if(musicSource==null)return;
            float target=musicEnabled?musicGain*(Time.unscaledTime<duckUntil?.52f:1):0;
            musicSource.volume=Mathf.MoveTowards(musicSource.volume,target,Time.unscaledDeltaTime*1.8f);
        }
        void OnApplicationPause(bool value)
        {
            paused=value;if(musicSource==null)return;
            if(value) { wasPlaying=musicSource.isPlaying;musicSource.Pause(); }
            else if(wasPlaying&&musicEnabled)musicSource.UnPause();
        }
    }
}
