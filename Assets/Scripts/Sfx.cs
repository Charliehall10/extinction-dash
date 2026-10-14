using System.Collections.Generic;
using UnityEngine;

public class Sfx : MonoBehaviour
{
    AudioSource src;
    readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();

    void Awake()
    {
        src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.volume = 0.7f;
        foreach (var name in SfxSynth.Names)
        {
            var data = SfxSynth.Make(name);
            var clip = AudioClip.Create(name, data.Length, 1, SfxSynth.Rate, false);
            clip.SetData(data, 0);
            clips[name] = clip;
        }
    }

    public void Play(string name, float volume = 1f)
    {
        if (clips.TryGetValue(name, out var clip)) src.PlayOneShot(clip, volume);
    }
}
