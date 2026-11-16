using UnityEngine;
using UnityEngine.UI;
namespace Deviant.UI
{
    public class PreservedGauge : MonoBehaviour
    {
        public RectTransform mask,fill;
        public float maxWidth=301,height=39,capWidth=6;
        public float Ratio {get;private set;}
        public float VisibleWidth=>Ratio*maxWidth;
        public void Set(int current,int maximum)
        {
            Ratio=maximum>0?Mathf.Clamp01((float)current/maximum):0;
            mask.gameObject.SetActive(Ratio>0);
            mask.sizeDelta=new Vector2(VisibleWidth,height);
            // For very small ratios, clip a minimum-width sliced sprite instead of shrinking the caps.
            fill.sizeDelta=new Vector2(Mathf.Max(capWidth*2,VisibleWidth),height);
        }
    }
}
