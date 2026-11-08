using UnityEngine;

namespace Atlas.UI
{
    /// <summary>
    /// The agent's presence: a small glowing sphere above the map.
    /// Idle = slow breathe, Thinking = violet pulse, Speaking = bright cyan pulse, Listening = red, follows mic level.
    /// Speaks answers for this person's tasks (and unprompted comments) through AtlasVoice.TextToSpeech,
    /// with a caption either way.
    /// </summary>
    public class AgentOrb : MonoBehaviour
    {
        public enum State { Idle, Thinking, Speaking, Listening }

        public float radius = 0.03f;
        public CaptionPanel caption;
        public AudioSource audioSource;

        public State Current { get; private set; }

        Transform core, halo, ring;
        Material coreMat, haloMat, ringMat;
        float speakUntil;
        int workingTasks;
        Color color = AtlasTheme.Agent;
        float scale = 1f;
        string lastText = "";

        void Awake()
        {
            core = AtlasMaterials.Primitive(PrimitiveType.Sphere, "Core", transform).transform;
            core.localScale = Vector3.one * radius * 2;
            coreMat = AtlasMaterials.Glow(AtlasTheme.Agent);
            core.GetComponent<MeshRenderer>().sharedMaterial = coreMat;

            halo = AtlasMaterials.Primitive(PrimitiveType.Sphere, "Halo", transform).transform;
            halo.localScale = Vector3.one * radius * 3.2f;
            haloMat = AtlasMaterials.Glow(new Color(1, 1, 1, 0.15f));
            halo.GetComponent<MeshRenderer>().sharedMaterial = haloMat;

            ring = AtlasMaterials.Primitive(PrimitiveType.Cylinder, "Ring", transform).transform;
            ring.localScale = new Vector3(radius * 4.4f, 0.0008f, radius * 4.4f);
            ring.localPosition = Vector3.down * radius * 1.6f;
            ringMat = AtlasMaterials.Glow(new Color(1, 1, 1, 0.08f));
            ring.GetComponent<MeshRenderer>().sharedMaterial = ringMat;

            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.spatialBlend = 1f;      // sound comes from the orb
                audioSource.minDistance = 0.5f;
                audioSource.playOnAwake = false;
            }
        }

        void Start() => AtlasServices.WhenReady(b =>
        {
            b.AnswerPosted += OnAnswer;
            b.TaskUpdated += OnTask;
        });

        void OnDestroy()
        {
            var b = AtlasServices.Backend;
            if (b == null) return;
            b.AnswerPosted -= OnAnswer;
            b.TaskUpdated -= OnTask;
        }

        readonly System.Collections.Generic.HashSet<ulong> working = new System.Collections.Generic.HashSet<ulong>();

        void OnTask(AgentTask t)
        {
            if (t.status == TaskStatus.Working || t.status == TaskStatus.Waiting) working.Add(t.id);
            else working.Remove(t.id);
            workingTasks = working.Count;
        }

        void OnAnswer(AgentAnswer a)
        {
            var b = AtlasServices.Backend;
            var t = b.GetTask(a.taskId);
            bool mine = t == null || t.askedBy == b.LocalIdentity || t.askedBy == "agent";
            bool myRole = b.LocalUser == null || a.forRole == b.LocalUser.role;
            if (!mine || !myRole) return;
            Speak(a.text, a.forRole);
        }

        /// <summary>Say something: plays TTS if a provider is set, always shows the caption.</summary>
        public void Speak(string text, Role role)
        {
            lastText = text;
            float estimate = Mathf.Max(2f, text.Split(' ').Length / 2.7f);
            speakUntil = Time.time + estimate;
            if (caption != null) caption.Say(text, estimate);

            var tts = AtlasVoice.TextToSpeech;
            if (tts == null) return;
            tts.Speak(text, role, PlayClip, err => Debug.LogWarning("[Atlas] TTS failed, caption only: " + err));
        }

        public void PlayClip(AudioClip clip)
        {
            if (clip == null) return;
            audioSource.Stop();
            audioSource.clip = clip;
            audioSource.Play();
            speakUntil = Time.time + clip.length;
            if (caption != null) caption.Say(lastText, clip.length);
        }

        void Update()
        {
            Current = PushToTalkController.IsRecording ? State.Listening
                    : (Time.time < speakUntil || audioSource.isPlaying) ? State.Speaking
                    : workingTasks > 0 ? State.Thinking
                    : State.Idle;

            Color target; float pulse; float speed;
            switch (Current)
            {
                case State.Listening:
                    target = AtlasTheme.Listening; speed = 0; pulse = 0.15f + PushToTalkController.Level * 0.5f; break;
                case State.Speaking:
                    target = AtlasTheme.Speaking; speed = 9f; pulse = 0.18f; break;
                case State.Thinking:
                    target = AtlasTheme.Thinking; speed = 5f; pulse = 0.12f; break;
                default:
                    target = AtlasTheme.Agent; speed = 1.4f; pulse = 0.05f; break;
            }

            color = Color.Lerp(color, target, 1 - Mathf.Exp(-8f * Time.deltaTime));
            float wave = speed > 0 ? (Mathf.Sin(Time.time * speed) + 1) * 0.5f : 1f;
            float s = 1f + pulse * wave;
            scale = Mathf.Lerp(scale, s, 1 - Mathf.Exp(-14f * Time.deltaTime));

            core.localScale = Vector3.one * radius * 2 * scale;
            halo.localScale = Vector3.one * radius * (3.0f + 0.8f * wave * (Current == State.Idle ? 0.3f : 1f));
            coreMat.color = color;
            haloMat.color = new Color(color.r, color.g, color.b, 0.10f + 0.12f * wave);
            ringMat.color = new Color(color.r, color.g, color.b, 0.08f + 0.06f * wave);
            transform.localPosition = baseLocal + Vector3.up * Mathf.Sin(Time.time * 1.1f) * 0.004f;
        }

        Vector3 baseLocal;
        public void SetHome(Vector3 localPosition) { baseLocal = localPosition; transform.localPosition = localPosition; }
    }
}
