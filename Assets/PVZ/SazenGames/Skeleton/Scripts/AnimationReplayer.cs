using UnityEngine;

namespace SazenGames.Skeleton
{
    /// <summary>
    /// This script replays a specified animation state on an Animator component at regular intervals.
    /// It also provides an option to reset the GameObject's position before each replay.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class AnimationReplayer : MonoBehaviour
    {
        [Header("Animation Settings")]
        [SerializeField] private string animationStateName = "YourAnimationState"; // Replace with your actual animation state name
        [SerializeField] private float replayDelay = 2f; // Delay in seconds before replaying

        [Header("Reset Settings")]
        [SerializeField] private bool resetPositionOnReplay = false; // Toggle to enable/disable position reset before each replay

        private Animator animator;
        private Vector3 initialPosition;

        void Start()
        {
            animator = GetComponent<Animator>();
            if (animator == null)
            {
                Debug.LogError("Animator component not found on this GameObject!");
                return;
            }

            initialPosition = transform.position; // Store the initial position

            // Start the first play
            PlayAnimation();
        }

        private void PlayAnimation()
        {
            // Reset position to initial if enabled
            if (resetPositionOnReplay)
            {
                transform.position = initialPosition;
            }

            animator.Play(animationStateName, 0, 0f); // Play the specified state in layer 0 from normalized time 0
                                                      // Schedule the next replay after the delay
            Invoke(nameof(PlayAnimation), replayDelay);
        }

        // Public method to manually reset position (e.g., call from another script or UI button)
        public void ResetPosition()
        {
            transform.position = initialPosition;
        }
    }
}
