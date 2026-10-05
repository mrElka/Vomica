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
    public bool RunHeld { get; private set; }
    public bool ToggleViewPressed { get; private set; }

    /// <summary>
    /// Номер слота оружия, нажатый в этом кадре. -1 = ничего не нажато.
    /// 1..9 — соответствующая клавиша.
    /// </summary>
    public int WeaponSlotPressed { get; private set; } = -1;

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;

        float x = 0f;
        float y = 0f;

        // Сброс разовых событий
        WeaponSlotPressed = -1;

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
            ToggleViewPressed = keyboard.vKey.wasPressedThisFrame;

            RunHeld = keyboard.leftCtrlKey.isPressed;

            // ==== Слоты оружия: 1..9 ====
            if (keyboard.digit1Key.wasPressedThisFrame) WeaponSlotPressed = 1;
            else if (keyboard.digit2Key.wasPressedThisFrame) WeaponSlotPressed = 2;
            else if (keyboard.digit3Key.wasPressedThisFrame) WeaponSlotPressed = 3;
            else if (keyboard.digit4Key.wasPressedThisFrame) WeaponSlotPressed = 4;
            else if (keyboard.digit5Key.wasPressedThisFrame) WeaponSlotPressed = 5;
            else if (keyboard.digit6Key.wasPressedThisFrame) WeaponSlotPressed = 6;
            else if (keyboard.digit7Key.wasPressedThisFrame) WeaponSlotPressed = 7;
            else if (keyboard.digit8Key.wasPressedThisFrame) WeaponSlotPressed = 8;
            else if (keyboard.digit9Key.wasPressedThisFrame) WeaponSlotPressed = 9;
        }
        else
        {
            JumpPressed = false;
            DashPressed = false;
            InteractPressed = false;
            InventoryPressed = false;
            PausePressed = false;
            ToggleViewPressed = false;
            RunHeld = false;
        }

        MovementInput = Vector2.ClampMagnitude(new Vector2(x, y), 1f);
        AttackPressed = mouse != null && mouse.rightButton.wasPressedThisFrame;

        if (InventoryPressed)
            Debug.Log("I pressed");
    }
}