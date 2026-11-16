using UnityEngine;
using UnityEngine.UI;

namespace Deviant.Game
{
    // Shared by ordinary actions, selected tabs and disabled product buttons.
    public sealed class PaperGuiButtonStyle : MonoBehaviour
    {
        Button button;Text label;
        void Awake(){button=GetComponent<Button>();label=GetComponentInChildren<Text>();}
        void LateUpdate()
        {
            if(!button||!label)return;
            if(button.image&&button.image.sprite&&button.image.sprite.name.StartsWith("slot"))return;
            bool selected=button.image&&button.image.sprite&&button.image.sprite.name=="button-selected";
            label.color=selected?PaperGuiSkin.Paper:!button.interactable?PaperGuiSkin.Muted:PaperGuiSkin.Text;
        }
    }
}
