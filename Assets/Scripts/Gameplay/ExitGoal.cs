using UnityEngine;

namespace BreakingHue.Gameplay
{
    /// <summary>
    /// Exit goal that triggers level completion when the player reaches it.
    /// Uses a static event for loose coupling with GameManager.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ExitGoal : MonoBehaviour
    {
        /// <summary>
        /// Fired when the player reaches the exit.
        /// Subscribe to this in GameManager to handle level completion.
        /// </summary>
        public static event System.Action OnPlayerReachedExit;

        [Header("Visual Effects")]
        [SerializeField] private float pulseSpeed = 2f;
        [SerializeField] private float pulseIntensity = 0.5f;

        private Renderer _renderer;
        private Color _baseEmissionColor;
        private Material _material;

        private void Awake()
        {
            // Ensure collider is a trigger
            var collider = GetComponent<Collider>();
            collider.isTrigger = true;

            _renderer = GetComponentInChildren<Renderer>();
            if (_renderer != null)
            {
                _material = _renderer.material;
                if (_material.HasProperty("_EmissionColor"))
                {
                    _baseEmissionColor = _material.GetColor("_EmissionColor");
                }
            }
        }

        private void Update()
        {
            // Pulsing glow effect
            if (_material != null && _material.HasProperty("_EmissionColor"))
            {
                float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseIntensity;
                _material.SetColor("_EmissionColor", _baseEmissionColor * pulse);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            Debug.Log("[ExitGoal] Player reached the exit!");
            OnPlayerReachedExit?.Invoke();
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            // Draw exit indicator in scene view
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 2f);
        }
#endif
    }
}
