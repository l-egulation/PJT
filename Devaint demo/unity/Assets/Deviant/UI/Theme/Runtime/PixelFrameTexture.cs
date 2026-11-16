using UnityEngine;
using UnityEngine.UI;

namespace Deviant.UI.Theme
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PixelFrameTexture : MaskableGraphic
    {
        public PixelFramedPanel Frame { get; set; }
        public override Texture mainTexture => Frame ? Frame.SourceTexture : Texture2D.whiteTexture;
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            if (Frame) Frame.PopulateInterior(mesh); else mesh.Clear();
        }
    }
}
