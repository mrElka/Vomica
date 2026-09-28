using UnityEngine;
using UnityEngine.InputSystem;

public class InputHandler : MonoBehaviour
{
    public Vector2 MovementInput { get; private set; }

    public bool AttackPressed { get; private set; }
    public bool InteractPressed { get; private set; }
    public bool InventoryPressed { get; private set; }
    public bool PausePressed { get; private set; }
    public bool JumpPressed { get; private set; }
    public bool DashPressed { get; private set; }

    /// <summary>Удержание клавиши бега (LeftCtrl).</summary>
    public bool RunHeld { get; private set; }

    /// <summary>Переключение вида от первого и третьего лица (V).</summary>
    public bool ToggleViewPressed { get; private set; }

    /// <summary>Нажатая цифра 1..9 как индекс слота 0..8. -1 — ничего не нажато.</summary>
    public int WeaponSlotPressed { get; private set; }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;

        float x = 0f;
        float y = 0f;

        if (keyboard != null)
        {
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) x -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) x += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) y -= 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) y += 1f;

            JumpPressed = keyboard.spaceKey.wasPressedThisFrame;
            DashPressed = keyboard.leftShiftKey.wasPressedThisFrame;
            InteractPressed = keyboard.eKey.wasPressedThisFrame;
            InventoryPressed = keyboard.iKey.wasPressedThisFrame;
            PausePressed = keyboard.escapeKey.wasPressedThisFrame;

            // Бег — удержание LeftCtrl
            RunHeld = keyboard.leftCtrlKey.isPressed;

            ToggleViewPressed = keyboard.vKey.wasPressedThisFrame;
            WeaponSlotPressed = ReadWeaponSlot(keyboard);
        }
        else
        {
            JumpPressed = false;
            DashPressed = false;
            InteractPressed = false;
            InventoryPressed = false;
            PausePressed = false;
            RunHeld = false;
            ToggleViewPressed = false;
            WeaponSlotPressed = -1;
        }

        MovementInput = Vector2.ClampMagnitude(new Vector2(x, y), 1f);
        AttackPressed = mouse != null && mouse.leftButton.wasPressedThisFrame;
    }

    private static int ReadWeaponSlot(Keyboard keyboard)
    {
        if (keyboard.digit1Key.wasPressedThisFrame) return 0;
        if (keyboard.digit2Key.wasPressedThisFrame) return 1;
        if (keyboard.digit3Key.wasPressedThisFrame) return 2;
        if (keyboard.digit4Key.wasPressedThisFrame) return 3;
        if (keyboard.digit5Key.wasPressedThisFrame) return 4;
        if (keyboard.digit6Key.wasPressedThisFrame) return 5;
        if (keyboard.digit7Key.wasPressedThisFrame) return 6;
        if (keyboard.digit8Key.wasPressedThisFrame) return 7;
        if (keyboard.digit9Key.wasPressedThisFrame) return 8;

        return -1;
    }
}
