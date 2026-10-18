namespace Interactables
{
    public interface IWorldInteraction
    {
        string Prompt { get; }
        void Interact();
    }
}
