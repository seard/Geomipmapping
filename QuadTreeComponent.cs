using System.Collections;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using UnityEngine;

public class QuadTreeComponent : MonoBehaviour
{
    public TerrainManager terrain;
    public bool ShowBumpMetric = false;
    public bool ShowErrorMetric = false;
    private GUIStyle textStyle;

    void Start()
    {
        textStyle = new GUIStyle();
        textStyle.normal.textColor = Color.green;
    }

    #region Gizmos

    void OnDrawGizmos()
    {
        if(terrain.root != null)
            DrawMode(terrain.activeNodes);
    }

    private Color maxColor = new Color(0, 1, 0, 1f);
    private Color minColor = new Color(1, 0, 0, 1f);

    private void DrawMode(HashSet<QuadTreeNode<TerrainManager.NodeData>> activeNodes)
    {
        foreach (var node in activeNodes)
        {
            Gizmos.color = Color.Lerp(maxColor, minColor, node.Depth / (float)terrain.MaxDepth);
            Gizmos.DrawWireCube(node.Position, new Vector3(1, 0, 1) * node.Size);

            if(ShowBumpMetric)
                drawString(node.Data.Variance + " [" + node.Data.arrayPosition.x + ";" + node.Data.arrayPosition.y, node.Position, textStyle);

            if (ShowErrorMetric)
                drawString(node.Data.ErrorMetric + " [" +node.Data.arrayPosition.x + ";" + node.Data.arrayPosition.y, node.Position, textStyle);
        }
    }

    static void drawString(string text, Vector3 worldPos, GUIStyle style)
    {
        UnityEditor.Handles.BeginGUI();

        var view = UnityEditor.SceneView.currentDrawingSceneView;
        Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);
        Vector2 size = GUI.skin.label.CalcSize(new GUIContent(text));
        GUI.Label(new Rect(screenPos.x - (size.x / 2), -screenPos.y + Camera.main.pixelHeight + 4, size.x, size.y), text, style);
        UnityEditor.Handles.EndGUI();
    }
    #endregion
}
