using UnityEngine;

namespace Atlas.UI
{
    /// <summary>Keeps a panel or label turned toward the user's head (yaw only by default).</summary>
    public class FaceCamera : MonoBehaviour
    {
        public bool yawOnly = true;
        [Tooltip("0 = snap; higher = slower, smoother turning.")]
        public float smoothing = 8f;

        Transform cam;

        void LateUpdate()
        {
            if (cam == null)
            {
                var c = Camera.main;
                if (c == null) return;
                cam = c.transform;
            }
            // A world-space canvas is read from its back side, so look *away* from the camera.
            var dir = transform.position - cam.position;
            if (yawOnly) dir.y = 0;
            if (dir.sqrMagnitude < 1e-6f) return;
            var target = Quaternion.LookRotation(dir.normalized, Vector3.up);
            transform.rotation = smoothing <= 0
                ? target
                : Quaternion.Slerp(transform.rotation, target, 1 - Mathf.Exp(-smoothing * Time.deltaTime));
        }
    }
}
