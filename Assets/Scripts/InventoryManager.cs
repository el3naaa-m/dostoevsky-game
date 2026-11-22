using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace NightChase
{
    public sealed class InventoryManager : MonoBehaviour
    {
        static readonly string[] Names = { "Удостоверение", "Кошелёк", "Блокнот кожаный", "Пистолет" };
        static readonly string[] Icons = { "IdCard", "Wallet", "Notebook", "Pistol" };
        static readonly string[] Symbols = { "ID", "▰", "▤", "⌐" };
        public static InventoryManager Instance { get; private set; }

        FirstPersonController movement;
        GameObject panel;
        Text title, hint;
        Font font;
        bool open, restoreCapture, movementWasEnabled, ready;

        internal static void Receive(FirstPersonController controller)
        {
            if (Instance && Instance.ready) return;
            var root = new GameObject("Chase Inventory");
            root.transform.SetParent(controller.transform, false);
            var inventory = root.AddComponent<InventoryManager>();
            Instance = inventory;
            inventory.movement = controller;
            inventory.Build();
            inventory.ready = true;
            inventory.Invoke(nameof(HideHint), 3f);
        }

        void Update()
        {
            if (ready && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
                Toggle();
        }

        void Toggle()
        {
            if (open)
            {
                Close();
                return;
            }

            open = true;
            movementWasEnabled = movement.enabled;
            restoreCapture = Cursor.lockState == CursorLockMode.Locked;
            movement.enabled = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            panel.SetActive(true);
        }

        void Close()
        {
            if (!open) return;
            open = false;
            if (panel) panel.SetActive(false);
            if (!movement || !movementWasEnabled) return;
            movement.enabled = true;
            movement.SendMessage("SetCapture", restoreCapture && Application.isFocused,
                SendMessageOptions.DontRequireReceiver);
        }

        void HideHint()
        {
            if (hint) hint.gameObject.SetActive(false);
        }

        void Build()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var hud = new GameObject("Inventory Canvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            hud.transform.SetParent(transform, false);
            var canvas = hud.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 95;
            var scaler = hud.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = .5f;

            hint = Label(hud.transform, "Инвентарь — R", new Vector2(0, 210), new Vector2(480, 65), 30);
            var background = Rect(hud.transform, "Inventory", Vector2.zero, new Vector2(620, 330));
            panel = background.gameObject;
            background.gameObject.AddComponent<Image>().color = new Color(.055f, .065f, .08f, .96f);
            Label(background, "Инвентарь", new Vector2(0, 131), new Vector2(500, 46), 29);

            var gridTransform = Rect(background, "Slots", new Vector2(0, 0), new Vector2(558, 201));
            var grid = gridTransform.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(101, 90);
            grid.spacing = new Vector2(10, 10);
            grid.childAlignment = TextAnchor.MiddleCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 5;
            title = Label(background, "", new Vector2(0, -128), new Vector2(550, 40), 23);
            for (int i = 0; i < 10; i++) AddSlot(gridTransform, i);

            if (!EventSystem.current)
            {
                var input = new GameObject("Inventory Event System", typeof(EventSystem));
                input.transform.SetParent(transform, false);
                input.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            panel.SetActive(false);
        }

        void AddSlot(Transform grid, int index)
        {
            var slot = Rect(grid, $"Slot {index + 1}", Vector2.zero, Vector2.one);
            var background = slot.gameObject.AddComponent<Image>();
            background.color = new Color(.16f, .18f, .21f);
            if (index >= Names.Length) return;

            var texture = Resources.Load<Texture2D>("InventoryIcons/" + Icons[index]);
            if (texture)
            {
                var icon = Rect(slot, "Icon", Vector2.zero, new Vector2(67, 67)).gameObject.AddComponent<RawImage>();
                icon.texture = texture;
                icon.raycastTarget = false;
            }
            else Label(slot, Symbols[index], Vector2.zero, new Vector2(74, 68), 31);

            string itemName = Names[index];
            var button = slot.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.onClick.AddListener(() => title.text = itemName);
            var pointer = slot.gameObject.AddComponent<EventTrigger>();
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => title.text = itemName);
            pointer.triggers.Add(enter);
            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => { if (title.text == itemName) title.text = ""; });
            pointer.triggers.Add(exit);
        }

        Text Label(Transform parent, string text, Vector2 position, Vector2 size, int fontSize)
        {
            var label = Rect(parent, "Label", position, size).gameObject.AddComponent<Text>();
            label.font = font;
            label.text = text;
            label.fontSize = fontSize;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            return label;
        }

        static RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = Vector2.one * .5f;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        void OnDestroy()
        {
            Close();
            if (Instance == this) Instance = null;
        }
    }
}
