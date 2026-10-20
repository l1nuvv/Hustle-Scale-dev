using UnityEngine;

namespace Interactables
{
    // An interaction target belonging only to the isolated art sample.
    public sealed class ArtTestInteraction : MonoBehaviour, IWorldInteraction
    {
        public enum ActionKind { Lamp, Bench, Test, Crate }
        [SerializeField] private ArtDirectionTestView view;
        [SerializeField] private ActionKind action;
        [SerializeField] private int index;

        public ActionKind Kind => action;
        public int Index => index;
        public string Prompt => view.Prompt(action, index);
        public void Interact() => view.Act(action, index);
    }
}
