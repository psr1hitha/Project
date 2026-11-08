// Voice seams for H2. The spec leaves the speech provider open (NVIDIA speech services or
// Meta Voice SDK), so the UI depends only on these two interfaces. Write one class per provider
// that implements them and assign it at startup:
//
//     AtlasVoice.SpeechToText = new MyRivaSpeechToText(apiKey);
//     AtlasVoice.TextToSpeech = new MyRivaTextToSpeech(apiKey);
//
// With nothing assigned: push-to-talk logs a warning (use the question buttons), and the orb
// shows the answer as a caption for an estimated reading time.

using System;
using UnityEngine;

namespace Atlas
{
    public interface ISpeechToText
    {
        void Transcribe(AudioClip clip, Action<string> onText, Action<string> onError);
    }

    public interface ITextToSpeech
    {
        void Speak(string text, Role forRole, Action<AudioClip> onClip, Action<string> onError);
    }

    public static class AtlasVoice
    {
        public static ISpeechToText SpeechToText;
        public static ITextToSpeech TextToSpeech;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { SpeechToText = null; TextToSpeech = null; }
    }
}
