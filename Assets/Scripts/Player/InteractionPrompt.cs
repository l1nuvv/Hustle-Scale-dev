using UnityEngine;
using UnityEngine.UI;

namespace Player
{
    public class InteractionPrompt : MonoBehaviour
    {
        [SerializeField] private PlayerRaycast interaction;
        [SerializeField] private Text label;
        private void LateUpdate()
        {
            if (label != null && interaction != null && label.text != interaction.FocusLabel)
                label.text = interaction.FocusLabel;
        }
    }
}
