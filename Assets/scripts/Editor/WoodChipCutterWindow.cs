using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class WoodChipCutterWindow : EditorWindow
{
    private enum BrushMode
    {
        Cut,
        Erase
    }

    private BreakableTimedPlatform targetPlatform;
    private BrushMode brushMode;
    private float pointSpacing = 0.02f;
    private float eraseRadius = 0.08f;
    private readonly List<Vector2> activeStroke = new List<Vector2>();
    private bool isDrawing;

    [MenuItem("Tools/Crumbling Platform/Wood Chip Cutter")]
    private static void Open()
    {
        GetWindow<WoodChipCutterWindow>("Wood Chip Cutter");
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += DuringSceneGui;
        Selection.selectionChanged += Repaint;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= DuringSceneGui;
        Selection.selectionChanged -= Repaint;
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Target", EditorStyles.boldLabel);
        targetPlatform = (BreakableTimedPlatform)EditorGUILayout.ObjectField("Platform", targetPlatform, typeof(BreakableTimedPlatform), true);

        if (targetPlatform == null && Selection.activeGameObject != null)
        {
            targetPlatform = Selection.activeGameObject.GetComponent<BreakableTimedPlatform>();
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Line Brush", EditorStyles.boldLabel);
        brushMode = (BrushMode)EditorGUILayout.EnumPopup("Mode", brushMode);
        pointSpacing = EditorGUILayout.Slider("Point Spacing", pointSpacing, 0.005f, 0.08f);

        if (brushMode == BrushMode.Erase)
        {
            eraseRadius = EditorGUILayout.Slider("Erase Radius", eraseRadius, 0.01f, 0.25f);
        }

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(targetPlatform == null))
        {
            EditorGUILayout.LabelField("Cut Lines", targetPlatform != null ? targetPlatform.GuidedCutLineCount.ToString() : "0");

            if (GUILayout.Button("Clear Cut Lines"))
            {
                Undo.RecordObject(targetPlatform, "Clear Wood Cut Lines");
                targetPlatform.ClearGuidedCutLines();
                EditorUtility.SetDirty(targetPlatform);
                SceneView.RepaintAll();
            }
        }

        EditorGUILayout.HelpBox("Draw cut lines across the Visual Target in Scene view. Each line should go from one side of the sprite to the other. The runtime cuts between your lines and splits each band into chips.", MessageType.Info);
    }

    private void DuringSceneGui(SceneView sceneView)
    {
        if (targetPlatform == null)
        {
            return;
        }

        SpriteRenderer visualTarget = targetPlatform.VisualTarget;
        if (visualTarget == null || visualTarget.sprite == null)
        {
            Handles.BeginGUI();
            GUILayout.BeginArea(new Rect(12f, 12f, 380f, 42f), EditorStyles.helpBox);
            GUILayout.Label("Assign Visual Target on BreakableTimedPlatform before cutting.");
            GUILayout.EndArea();
            Handles.EndGUI();
            return;
        }

        DrawExistingCutLines(visualTarget);
        DrawActiveStroke(visualTarget);
        HandleBrushInput(visualTarget);
        DrawBrushPreview(visualTarget);
    }

    private void HandleBrushInput(SpriteRenderer visualTarget)
    {
        Event current = Event.current;
        if (current.alt || current.button != 0)
        {
            return;
        }

        if (!TryGetNormalizedPoint(visualTarget, current.mousePosition, out Vector2 normalizedPoint))
        {
            return;
        }

        if (brushMode == BrushMode.Erase)
        {
            if (current.type == EventType.MouseDown || current.type == EventType.MouseDrag)
            {
                Undo.RecordObject(targetPlatform, "Erase Wood Cut Line");
                targetPlatform.RemoveGuidedCutLine(normalizedPoint, eraseRadius);
                EditorUtility.SetDirty(targetPlatform);
                current.Use();
                SceneView.RepaintAll();
            }

            return;
        }

        if (current.type == EventType.MouseDown)
        {
            activeStroke.Clear();
            activeStroke.Add(normalizedPoint);
            isDrawing = true;
            current.Use();
            SceneView.RepaintAll();
        }
        else if (current.type == EventType.MouseDrag && isDrawing)
        {
            Vector2 previousPoint = activeStroke[activeStroke.Count - 1];
            if ((normalizedPoint - previousPoint).sqrMagnitude >= pointSpacing * pointSpacing)
            {
                activeStroke.Add(normalizedPoint);
            }

            current.Use();
            SceneView.RepaintAll();
        }
        else if (current.type == EventType.MouseUp && isDrawing)
        {
            if (activeStroke.Count >= 2)
            {
                Undo.RecordObject(targetPlatform, "Add Wood Cut Line");
                targetPlatform.AddGuidedCutLine(activeStroke);
                EditorUtility.SetDirty(targetPlatform);
            }

            activeStroke.Clear();
            isDrawing = false;
            current.Use();
            SceneView.RepaintAll();
        }
    }

    private void DrawExistingCutLines(SpriteRenderer visualTarget)
    {
        Handles.color = new Color(1f, 0.65f, 0.1f, 0.9f);
        for (int i = 0; i < targetPlatform.GuidedCutLineCount; i++)
        {
            BreakableTimedPlatform.WoodCutLine line = targetPlatform.GetGuidedCutLine(i);
            DrawCutLine(visualTarget, line.points, 4f);
        }
    }

    private void DrawActiveStroke(SpriteRenderer visualTarget)
    {
        if (activeStroke.Count < 2)
        {
            return;
        }

        Handles.color = new Color(0.2f, 1f, 0.25f, 0.9f);
        DrawCutLine(visualTarget, activeStroke, 5f);
    }

    private void DrawBrushPreview(SpriteRenderer visualTarget)
    {
        if (!TryGetNormalizedPoint(visualTarget, Event.current.mousePosition, out Vector2 normalizedPoint))
        {
            return;
        }

        Handles.color = brushMode == BrushMode.Cut
            ? new Color(0.2f, 1f, 0.25f, 0.75f)
            : new Color(1f, 0.2f, 0.2f, 0.75f);

        Bounds bounds = visualTarget.sprite.bounds;
        Vector2 localPoint = NormalizedToLocal(bounds, normalizedPoint);
        float worldRadius = Mathf.Max(bounds.size.x, bounds.size.y) * 0.015f * visualTarget.transform.lossyScale.x;
        if (brushMode == BrushMode.Erase)
        {
            worldRadius = Mathf.Max(bounds.size.x, bounds.size.y) * eraseRadius * visualTarget.transform.lossyScale.x;
        }

        Handles.DrawWireDisc(visualTarget.transform.TransformPoint(localPoint), visualTarget.transform.forward, worldRadius);
        HandleUtility.Repaint();
    }

    private void DrawCutLine(SpriteRenderer visualTarget, List<Vector2> normalizedPoints, float width)
    {
        if (normalizedPoints == null || normalizedPoints.Count < 2)
        {
            return;
        }

        Bounds bounds = visualTarget.sprite.bounds;
        var worldPoints = new Vector3[normalizedPoints.Count];
        for (int i = 0; i < normalizedPoints.Count; i++)
        {
            worldPoints[i] = visualTarget.transform.TransformPoint(NormalizedToLocal(bounds, normalizedPoints[i]));
        }

        Handles.DrawAAPolyLine(width, worldPoints);
    }

    private bool TryGetNormalizedPoint(SpriteRenderer visualTarget, Vector2 guiPoint, out Vector2 normalizedPoint)
    {
        normalizedPoint = Vector2.zero;
        Ray ray = HandleUtility.GUIPointToWorldRay(guiPoint);
        Plane plane = new Plane(visualTarget.transform.forward, visualTarget.transform.position);
        if (!plane.Raycast(ray, out float distance))
        {
            return false;
        }

        Vector3 worldPoint = ray.GetPoint(distance);
        Vector2 localPoint = visualTarget.transform.InverseTransformPoint(worldPoint);
        Bounds bounds = visualTarget.sprite.bounds;

        if (localPoint.x < bounds.min.x || localPoint.x > bounds.max.x || localPoint.y < bounds.min.y || localPoint.y > bounds.max.y)
        {
            return false;
        }

        normalizedPoint = new Vector2(
            Mathf.InverseLerp(bounds.min.x, bounds.max.x, localPoint.x),
            Mathf.InverseLerp(bounds.min.y, bounds.max.y, localPoint.y));
        return true;
    }

    private static Vector2 NormalizedToLocal(Bounds bounds, Vector2 normalizedPoint)
    {
        return new Vector2(
            Mathf.Lerp(bounds.min.x, bounds.max.x, normalizedPoint.x),
            Mathf.Lerp(bounds.min.y, bounds.max.y, normalizedPoint.y));
    }
}
