using System;
using NightChase.Station;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NightChase
{
    public sealed class InventoryManager : MonoBehaviour
    {
        public static InventoryManager Instance { get; private set; }

        FirstPersonController movement;
        Action afterReview;
        StationUI view;
        Item[] items;
        bool open, restoreCapture, movementWasEnabled, ready;

        internal static void Receive(FirstPersonController controller, Action afterReview)
        {
            if (Instance && Instance.ready) return;
            var root = new GameObject("Chase Inventory");
            root.transform.SetParent(controller.transform, false);
            var inventory = root.AddComponent<InventoryManager>();
            Instance = inventory;
            inventory.movement = controller;
            inventory.afterReview = afterReview;
            // Используем тот же список предметов и Canvas, что и в бюро.
            inventory.view = DialogueSystem.CreateView(root.transform, linear: true, interactive: true);
            inventory.items = new[]
            {
                Item.Make("IdCard", "Удостоверение"),
                Item.Make("Wallet", "Кошелёк"),
                Item.Make("Notebook", "Кожаный блокнот"),
                Item.Make("Pistol", "Пистолет")
            };
            inventory.view.Prompt.text = "Инвентарь — R";
            inventory.view.Prompt.gameObject.SetActive(true);
            inventory.ready = true;
            inventory.Invoke(nameof(HideHint), 3f);
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (!ready || keyboard == null) return;
            if (open && (keyboard.rKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame))
                Close(completedReview: true);
            else if (!open && keyboard.rKey.wasPressedThisFrame)
                Open();
        }

        void Open()
        {
            if (!movement) return;
            open = true;
            movementWasEnabled = movement.enabled;
            restoreCapture = Cursor.lockState == CursorLockMode.Locked;
            movement.enabled = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            HideHint();
            view.Show("Инвентарь", "");
            view.ShowItems(items);
            view.Button("Закрыть   [R]", () => Close(completedReview: true));
        }

        void Close(bool completedReview = false)
        {
            if (!open) return;
            open = false;
            if (view) view.Close();
            if (completedReview && afterReview != null)
            {
                var callback = afterReview;
                afterReview = null;
                ready = false;
                HideHint();
                callback();
                return;
            }
            if (!movement || !movementWasEnabled) return;
            movement.enabled = true;
            movement.SendMessage("SetCapture", restoreCapture && Application.isFocused,
                SendMessageOptions.DontRequireReceiver);
        }

        void HideHint()
        {
            if (view) view.Prompt.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            Close();
            if (items != null)
                foreach (var item in items)
                {
                    if (!item) continue;
                    if (item.icon) Destroy(item.icon);
                    Destroy(item);
                }
            if (Instance == this) Instance = null;
        }
    }
}
