using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

public sealed class InGameResetButton : MonoBehaviour
{
    private const string RootName = "__InGameResetButton";

    [Header("Label")]
    [SerializeField] private string buttonText = "RESET";

    [Header("Shortcut")]
    [SerializeField] private Key resetKey = Key.R;
    [SerializeField] private bool requireCtrl = false;
    [SerializeField] private bool requireShift = false;
    [SerializeField] private bool requireAlt = false;

    [Header("Layout (px)")]
    [SerializeField] private Vector2 size = new Vector2(160f, 56f);
    [SerializeField] private Vector2 margin = new Vector2(16f, 16f);

    [Header("Style")]
    [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.65f);
    [SerializeField] private Color textColor = Color.white;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureResetButtonExists()
    {
        if (GameObject.Find(RootName) != null)
        {
            return;
        }

        Canvas canvas = FindBestCanvas();
        if (canvas == null)
        {
            return;
        }

        EnsureEventSystemExists();

        GameObject root = new GameObject(RootName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(InGameResetButton));
        root.layer = canvas.gameObject.layer;
        root.transform.SetParent(canvas.transform, false);

        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.sizeDelta = new Vector2(160f, 56f);
        rect.anchoredPosition = new Vector2(-16f, -16f);

        Image background = root.GetComponent<Image>();
        background.raycastTarget = true;

        Button button = root.GetComponent<Button>();
        button.targetGraphic = background;

        GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textGo.layer = root.layer;
        textGo.transform.SetParent(root.transform, false);

        RectTransform textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text text = textGo.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.alignment = TextAnchor.MiddleCenter;
        text.raycastTarget = false;
        text.resizeTextForBestFit = false;
        text.fontSize = 28;

        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.95f);
        colors.pressedColor = new Color(1f, 1f, 1f, 0.85f);
        colors.selectedColor = colors.normalColor;
        colors.disabledColor = new Color(1f, 1f, 1f, 0.35f);
        button.colors = colors;
    }

    private static Canvas FindBestCanvas()
    {
        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude);
        if (canvases == null || canvases.Length == 0)
        {
            return null;
        }

        Canvas best = null;
        int bestScore = int.MinValue;
        foreach (Canvas c in canvases)
        {
            if (c == null || !c.isActiveAndEnabled)
            {
                continue;
            }

            int score = 0;
            if (c.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                score += 1000;
            }
            score += c.sortingOrder;

            if (score > bestScore)
            {
                bestScore = score;
                best = c;
            }
        }

        return best != null ? best : canvases[0];
    }

    private static void EnsureEventSystemExists()
    {
        if (Object.FindAnyObjectByType<EventSystem>() != null)
        {
            return;
        }

        GameObject es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        Object.DontDestroyOnLoad(es);
    }

    private void Awake()
    {
        Image background = GetComponent<Image>();
        if (background != null)
        {
            background.color = backgroundColor;
        }

        Button button = GetComponent<Button>();
        if (button != null)
        {
            button.onClick.RemoveListener(ResetScene);
            button.onClick.AddListener(ResetScene);
        }

        Text text = GetComponentInChildren<Text>(true);
        if (text != null)
        {
            text.text = buttonText;
            text.color = textColor;
        }

        RectTransform rect = GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.sizeDelta = size;
            rect.anchoredPosition = new Vector2(-margin.x, -margin.y);
        }
    }

    private void Update()
    {
        if (!IsShortcutPressed())
        {
            return;
        }

        ResetScene();
    }

    private bool IsShortcutPressed()
    {
        if (Keyboard.current == null)
        {
            return false;
        }

        if (!Keyboard.current[resetKey].wasPressedThisFrame)
        {
            return false;
        }

        if (requireCtrl && !(Keyboard.current.ctrlKey.isPressed || Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed))
        {
            return false;
        }

        if (requireShift && !(Keyboard.current.shiftKey.isPressed || Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed))
        {
            return false;
        }

        if (requireAlt && !(Keyboard.current.altKey.isPressed || Keyboard.current.leftAltKey.isPressed || Keyboard.current.rightAltKey.isPressed))
        {
            return false;
        }

        return true;
    }

    private static void ResetScene()
    {
        Time.timeScale = 1f;

        Scene scene = SceneManager.GetActiveScene();
        if (scene.buildIndex >= 0)
        {
            SceneManager.LoadScene(scene.buildIndex);
            return;
        }

        SceneManager.LoadScene(scene.name);
    }
}
