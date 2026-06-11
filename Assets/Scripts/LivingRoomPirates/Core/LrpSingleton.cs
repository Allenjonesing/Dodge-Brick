using UnityEngine;

namespace LivingRoomPirates.Core
{
    /// <summary>
    /// Tiny optional singleton helper. Keeps these systems drop-in friendly without requiring a framework.
    /// </summary>
    public abstract class LrpSingleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        public static T Instance { get; private set; }

        protected virtual void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this as T;
        }

        protected virtual void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
