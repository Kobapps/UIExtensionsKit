using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Kobapps.UIExtensionsKit.Editor
{
    /// <summary>
    /// Creates a ready-to-use <see cref="EnhancedButton"/> from the GameObject menu, with the canvas
    /// and EventSystem set up if the scene has none — the same courtesy Unity's own UI menu does.
    /// </summary>
    internal static class EnhancedButtonCreateMenu
    {
        [MenuItem("GameObject/UI/Enhanced Button", false, 2031)]
        private static void CreateEnhancedButton(MenuCommand command)
        {
            RectTransform parent = ResolveParent(command);

            var buttonObject = new GameObject("Enhanced Button", typeof(RectTransform), typeof(Image), typeof(EnhancedButton));
            Undo.RegisterCreatedObjectUndo(buttonObject, "Create Enhanced Button");

            var rect = (RectTransform)buttonObject.transform;
            rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(200f, 64f);
            rect.anchoredPosition = Vector2.zero;

            var image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.22f, 0.55f, 0.95f, 1f);
            image.sprite = DefaultUISprite();
            image.type = Image.Type.Sliced;

            var button = buttonObject.GetComponent<EnhancedButton>();
            button.targetGraphic = image;

            // The kit tints through its preset. Leaving Unity's ColorTint on as well means both
            // write the same colour every frame and the result looks broken.
            button.transition = Selectable.Transition.None;

            CreateLabel(rect);

            Selection.activeGameObject = buttonObject;
            EditorGUIUtility.PingObject(buttonObject);
        }

        private static void CreateLabel(RectTransform parent)
        {
            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            var labelRect = (RectTransform)labelObject.transform;
            labelRect.SetParent(parent, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            var text = labelObject.GetComponent<Text>();
            text.text = "Button";
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.fontSize = 24;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // The label must not eat the pointer, or hovering the text would exit the button.
            text.raycastTarget = false;
        }

        private static Sprite DefaultUISprite() =>
            AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        /// <summary>
        /// The RectTransform a new button should live under: the clicked object if it is UI, else the
        /// first canvas in the scene, creating one (and an EventSystem) if there is none.
        /// </summary>
        private static RectTransform ResolveParent(MenuCommand command)
        {
            if (command?.context is GameObject clicked && clicked.GetComponentInParent<Canvas>() != null)
            {
                if (clicked.transform is RectTransform clickedRect) return clickedRect;
            }

            Canvas canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas == null) canvas = CreateCanvas();

            EnsureEventSystem();
            return (RectTransform)canvas.transform;
        }

        private static Canvas CreateCanvas()
        {
            var canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(canvasObject, "Create Canvas");

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            return canvas;
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;

            // Let Unity build it. Adding StandaloneInputModule by hand is wrong on any project set
            // to the new Input System, and this menu can't know which backend is active.
            if (EditorApplication.ExecuteMenuItem("GameObject/UI/Event System")) return;

            var fallback = new GameObject("EventSystem", typeof(EventSystem));
            Undo.RegisterCreatedObjectUndo(fallback, "Create EventSystem");
            Debug.LogWarning(
                "[UIExtensionsKit] Created an EventSystem with no input module. Add the one that matches " +
                "this project's Active Input Handling, or the button will not receive clicks.", fallback);
        }
    }
}
