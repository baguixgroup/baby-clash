using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

namespace BabyClash
{
    /// <summary>
    /// Feeds one baby from the Input System. Each baby gets its own copy of the controls asset, masked to its
    /// keyboard half (WASD+Space+LeftShift or Arrows+Enter+RightShift) plus one gamepad slot, so two players can
    /// share a keyboard. The only gameplay component that knows about devices: the CPU brain replaces it for bots.
    /// </summary>
    [RequireComponent(typeof(BabyController))]
    public sealed class BabyInputHandler : MonoBehaviour
    {
        public const string KeyboardLeftScheme = "KeyboardLeft";
        public const string KeyboardRightScheme = "KeyboardRight";
        public const string GamepadScheme = "Gamepad";
        const string MoveActionPath = "Baby/Move";
        const string ContextActionPath = "Baby/Action";
        const string DashActionPath = "Baby/Dash";

        [Tooltip("Shared controls asset (Assets/BabyClash/Input/BabyClashControls.inputactions).")]
        [SerializeField] InputActionAsset actionsTemplate;

        [Tooltip("Keyboard half for this baby: KeyboardLeft (WASD + Space + Left Shift) or KeyboardRight (Arrows + Enter + Right Shift).")]
        [SerializeField] string keyboardScheme = KeyboardLeftScheme;

        [Tooltip("Which connected gamepad drives this baby (0 = first). -1 = keyboard only.")]
        [SerializeField] int gamepadSlot;

        bool soloKeyboard;
        bool useKeyboard = true;

        readonly List<InputDevice> deviceList = new List<InputDevice>(2);
        BabyController controller;
        InputActionAsset actions;
        InputAction moveAction;
        InputAction contextAction;
        InputAction dashAction;

        /// <summary>Scene/prefab wiring (editor tooling).</summary>
        public void Configure(InputActionAsset template, string scheme, int gamepad)
        {
            actionsTemplate = template;
            keyboardScheme = scheme;
            gamepadSlot = gamepad;
        }

        /// <summary>Single-player convenience: both halves of the keyboard drive this baby.</summary>
        public void SetSoloKeyboard(bool solo)
        {
            soloKeyboard = solo;
            if (actions != null)
                ApplyBindingMask();
        }

        /// <summary>Chooses this player's devices: a keyboard half (null = none) and a gamepad (-1 = none).</summary>
        public void AssignDevices(string scheme, int gamepad)
        {
            useKeyboard = scheme != null;
            if (scheme != null)
                keyboardScheme = scheme;
            gamepadSlot = gamepad;
            if (actions == null)
                return;
            ApplyBindingMask();
            AssignDevices();
        }

        void Awake() => controller = GetComponent<BabyController>();

        void OnEnable()
        {
            if (actions == null && !CreateActions())
                return;

            AssignDevices();
            InputSystem.onDeviceChange += HandleDeviceChange;
            actions.Enable();
        }

        void OnDisable()
        {
            InputSystem.onDeviceChange -= HandleDeviceChange;
            if (actions != null)
                actions.Disable();
            if (controller != null)
                controller.SetMoveInput(Vector2.zero);
        }

        void OnDestroy()
        {
            if (actions != null)
                Destroy(actions);
        }

        void Update()
        {
            if (actions == null)
                return;
            // Paused: menus own the buttons.
            if (Time.timeScale <= 0f)
            {
                controller.SetMoveInput(Vector2.zero);
                return;
            }

            Vector2 move = moveAction.ReadValue<Vector2>();
            TouchControls touch = TouchControls.Active;
            if (touch != null && controller.PlayerIndex == 0)
            {
                if (touch.Move.sqrMagnitude > move.sqrMagnitude)
                    move = touch.Move;
                if (touch.ConsumeAction())
                    controller.PressAction();
                if (touch.ConsumeDash())
                    controller.PressDash();
            }
            controller.SetMoveInput(move);
            if (contextAction.WasPressedThisFrame())
                controller.PressAction();
            if (dashAction.WasPressedThisFrame())
                controller.PressDash();
        }

        bool CreateActions()
        {
            if (actionsTemplate == null)
            {
                Debug.LogError($"{name}: no InputActionAsset assigned; this baby can't be controlled.", this);
                return false;
            }

            actions = Instantiate(actionsTemplate);
            ApplyBindingMask();
            moveAction = actions.FindAction(MoveActionPath, true);
            contextAction = actions.FindAction(ContextActionPath, true);
            dashAction = actions.FindAction(DashActionPath, true);
            return true;
        }

        void ApplyBindingMask()
        {
            actions.bindingMask = soloKeyboard
                ? InputBinding.MaskByGroups(KeyboardLeftScheme, KeyboardRightScheme, GamepadScheme)
                : InputBinding.MaskByGroups(keyboardScheme, GamepadScheme);
        }

        void HandleDeviceChange(InputDevice device, InputDeviceChange change)
        {
            switch (change)
            {
                case InputDeviceChange.Added:
                case InputDeviceChange.Removed:
                case InputDeviceChange.Reconnected:
                case InputDeviceChange.Disconnected:
                    AssignDevices();
                    break;
            }
        }

        void AssignDevices()
        {
            deviceList.Clear();
            if (useKeyboard && Keyboard.current != null)
                deviceList.Add(Keyboard.current);
            if (gamepadSlot >= 0 && gamepadSlot < Gamepad.all.Count)
                deviceList.Add(Gamepad.all[gamepadSlot]);

            actions.devices = new ReadOnlyArray<InputDevice>(deviceList.ToArray());
        }
    }
}
