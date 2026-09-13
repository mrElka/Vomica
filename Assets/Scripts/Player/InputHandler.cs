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

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;

        float x = 0f;
        float y = 0f;

        if (keyboard != null)
        {
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                x -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                x += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
                y -= 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
                y += 1f;

            JumpPressed = keyboard.spaceKey.wasPressedThisFrame;
            DashPressed = keyboard.leftShiftKey.wasPressedThisFrame;
            InteractPressed = keyboard.eKey.wasPressedThisFrame;
            InventoryPressed = keyboard.iKey.wasPressedThisFrame;
            PausePressed = keyboard.escapeKey.wasPressedThisFrame;
        }
        else
        {
            JumpPressed = false;
            DashPressed = false;
            InteractPressed = false;
            InventoryPressed = false;
            PausePressed = false;
        }

        MovementInput = Vector2.ClampMagnitude(new Vector2(x, y), 1f);
        AttackPressed = mouse != null && mouse.leftButton.wasPressedThisFrame;

        if (InventoryPressed)
            Debug.Log("I pressed");
    }
}
