using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Graphic))]
[ExecuteAlways]
public class UIUnstretchedUVs : BaseMeshEffect
{
    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || vh.currentVertCount == 0) return;

        // 1. Find the physical left and right boundaries of the UI element's mesh
        float minX = float.MaxValue;
        float maxX = float.MinValue;

        UIVertex vert = new UIVertex();
        for (int i = 0; i < vh.currentVertCount; i++)
        {
            vh.PopulateUIVertex(ref vert, i);
            if (vert.position.x < minX) minX = vert.position.x;
            if (vert.position.x > maxX) maxX = vert.position.x;
        }

        float width = maxX - minX;

        // 2. Calculate a perfect 0-to-1 gradient and save it into the hidden UV1 channel
        for (int i = 0; i < vh.currentVertCount; i++)
        {
            vh.PopulateUIVertex(ref vert, i);

            // This creates a perfect 0.0 (left) to 1.0 (right) value
            float normalizedX = (width > 0) ? (vert.position.x - minX) / width : 0f;

            // uv0 is the standard 9-sliced texture. We inject our custom math into uv1!
            vert.uv1 = new Vector2(normalizedX, 0);

            vh.SetUIVertex(vert, i);
        }
    }
}