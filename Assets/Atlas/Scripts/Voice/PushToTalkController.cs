using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace Atlas
{
    /// <summary>
    /// Hold to record, release to send. In the editor, hold Space. On Quest, hook a pinch pose or
    /// controller button to BeginTalk/EndTalk (e.g. Interaction SDK's ActiveStateUnityEventWrapper:
    /// WhenActivated -> BeginTalk, WhenDeactivated -> EndTalk).
    /// </summary>
    public class PushToTalkController : MonoBehaviour
    {
        public static bool IsRecording { get; private set; }
        public static float Level { get; private set; }   // 0..1 mic level, drives the orb

        public KeyCode editorKey = KeyCode.Space;
        public int sampleRate = 16000;
        public int maxSeconds = 15;

        AudioClip clip;
        string device;
        float startedAt;
        readonly float[] levelBuf = new float[256];

        void Start()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
                Permission.RequestUserPermission(Permission.Microphone);
#endif
        }

        void Update()
        {
            if (!InputCompat.TypingInUI)
            {
                if (InputCompat.KeyDown(editorKey)) BeginTalk();
                if (IsRecording && !InputCompat.KeyHeld(editorKey) && keyStarted) EndTalk();
            }
            if (IsRecording) UpdateLevel();
            if (IsRecording && Time.time - startedAt > maxSeconds) EndTalk();
        }

        bool keyStarted;

        public void BeginTalk()
        {
            if (IsRecording) return;
            if (Microphone.devices.Length == 0) { Debug.LogWarning("[Atlas] No microphone found."); return; }
            device = Microphone.devices[0];
            clip = Microphone.Start(device, false, maxSeconds, sampleRate);
            startedAt = Time.time;
            IsRecording = true;
            keyStarted = InputCompat.KeyHeld(editorKey) || InputCompat.KeyDown(editorKey);
        }

        public void EndTalk()
        {
            if (!IsRecording) return;
            int samples = Microphone.GetPosition(device);
            Microphone.End(device);
            IsRecording = false;
            keyStarted = false;
            Level = 0;
            if (samples < sampleRate / 4) return; // under 0.25 s: ignore accidental taps

            var trimmed = AudioClip.Create("ptt", samples, clip.channels, sampleRate, false);
            var data = new float[samples * clip.channels];
            clip.GetData(data, 0);
            trimmed.SetData(data, 0);

            var stt = AtlasVoice.SpeechToText;
            if (stt == null)
            {
                Debug.LogWarning("[Atlas] No speech-to-text provider assigned (AtlasVoice.SpeechToText). Use the question buttons.");
                return;
            }
            stt.Transcribe(trimmed,
                text => { if (!string.IsNullOrWhiteSpace(text)) AtlasServices.Backend?.AddTranscript(text); },
                err => Debug.LogWarning("[Atlas] Speech-to-text failed: " + err));
        }

        void UpdateLevel()
        {
            int pos = Microphone.GetPosition(device) - levelBuf.Length;
            if (pos < 0 || clip == null) return;
            clip.GetData(levelBuf, pos);
            float peak = 0;
            foreach (var s in levelBuf) peak = Mathf.Max(peak, Mathf.Abs(s));
            Level = Mathf.Lerp(Level, Mathf.Clamp01(peak * 3f), 0.5f);
        }
    }
}
