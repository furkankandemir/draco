using UnityEngine;

namespace EntropyOnline
{
    public class KOBlacksmithHammerEffect : MonoBehaviour
    {
        [Header("Spark Particles Reference")]
        public ParticleSystem sparkParticles;

        [Header("Hit Timing (0.0 to 1.0)")]
        [Range(0f, 1f)]
        public float hitNormalizedTime = 0.52f; // Exact frame ratio of the hit

        private Animator animator;
        private float lastTime = 0f;

        void Start()
        {
            animator = GetComponent<Animator>();
            if (sparkParticles != null)
            {
                // Ensure the particle system is stopped at startup
                var main = sparkParticles.main;
                main.playOnAwake = false;
                sparkParticles.Stop();
            }
        }

        void Update()
        {
            if (animator == null || sparkParticles == null) return;

            // Get current animator state normalized time (0 to 1)
            var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            float currentTime = stateInfo.normalizedTime % 1f;

            // Trigger particles when the hit frame is reached in this loop cycle
            if (lastTime < hitNormalizedTime && currentTime >= hitNormalizedTime)
            {
                sparkParticles.Play();
            }
            
            // Handle looping state transition
            if (currentTime < lastTime)
            {
                lastTime = 0f;
            }
            else
            {
                lastTime = currentTime;
            }
        }
    }
}
