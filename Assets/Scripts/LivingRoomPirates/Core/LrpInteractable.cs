using UnityEngine;

namespace LivingRoomPirates.Core
{
    /// <summary>
    /// Simple interaction surface. Wire this to Oculus/Meta XR, XR Interaction Toolkit, or your own hand code.
    /// </summary>
    public interface ILrpInteractable
    {
        string InteractionLabel { get; }
        bool CanInteract(GameObject interactor);
        void Interact(GameObject interactor);
    }
}
