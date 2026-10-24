using System.Collections;
using FistVR;
using UnityEngine;

namespace Atlas.MappingComponents.Sandbox
{
    /// <summary>
    /// This component will place the player at it's position when the scene is first loaded.
    /// </summary>
    public class PlayerSpawnPoint : MonoBehaviour
    {
        private IEnumerator Start()
        {
#if NATIVEH3VR
            if (AtlasPlugin.IsAuthoringPreview) yield break;
#endif
            yield return PlacePlayer();
        }

        // Keep game-only type resolution out of the editor preview's coroutine.
        private IEnumerator PlacePlayer()
        {
            // Wait one frame so everything is all setup
            yield return null;
#if NATIVEH3VR
            float deadline = Time.realtimeSinceStartup + 30;
            while (!GM.CurrentMovementManager || !GM.CurrentPlayerBody)
            {
                if (Time.realtimeSinceStartup > deadline) throw new System.TimeoutException("Atlas player initialization timed out.");
                yield return null;
            }
#endif
            GM.CurrentMovementManager.TeleportToPoint(transform.position, true, transform.forward);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.5f);
            Gizmos.DrawSphere(transform.position, 0.1f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.25f);
        }
    }
}
