using UnityEngine;
using UnityEngine.InputSystem;

/// Что делает:
/// 1. Читает клавиатуру и мышь через Unity Input System (new input system).
/// 2. Отдаёт другим скриптам готовые флаги: движение, атака, прыжок, рывок, бег, пауза и т.д.
/// 3. Не хранит игровое состояние и не управляет персонажем — только преобразует «сырое» нажатие
///    в понятные свойства.
public class InputHandler : MonoBehaviour
{
    public Vector2 MovementInput { get; private set; }
    public bool AttackPressed { get; private set; }
    public bool InteractPressed { get; private set; }
    public bool PausePressed { get; private set; }


    public bool JumpPressed { get; private set; }
    public bool DashPressed { get; private set; }
    public bool RunHeld { get; private set; }
    public bool ToggleViewPressed { get; private set; }


    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;

        if (keyboard == null || PauseMenu.IsPaused)
        {
            ResetAll();
            return;
        }

        ReadMovement(keyboard);
        ReadActionKeys(keyboard);
        ReadMouse(mouse);
    }

    /// <summary>Считывает WASD и стрелки, собирает нормализованный вектор движения.</summary>
    private void ReadMovement(Keyboard keyboard)
    {
        float x = 0f;
        float y = 0f;

        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) x -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) x += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) y -= 1f;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) y += 1f;

        MovementInput = Vector2.ClampMagnitude(new Vector2(x, y), 1f);
    }
    /// <summary>Считывает разовые и удерживаемые действия с клавиатуры.</summary>
    private void ReadActionKeys(Keyboard keyboard)
    {
        JumpPressed = keyboard.spaceKey.wasPressedThisFrame;
        DashPressed = keyboard.leftShiftKey.wasPressedThisFrame;
        InteractPressed = keyboard.eKey.wasPressedThisFrame;
        PausePressed = keyboard.escapeKey.wasPressedThisFrame;
        ToggleViewPressed = keyboard.vKey.wasPressedThisFrame;

        RunHeld = keyboard.leftCtrlKey.isPressed;
    }
    /// <summary>Считывает атаку с мыши (левая или правая кнопка).</summary>
    private void ReadMouse(Mouse mouse)
    {
        AttackPressed = mouse != null &&
                        (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame);
    }

 
    private void ResetAll()
    {
        MovementInput = Vector2.zero;

        AttackPressed = false;
        InteractPressed = false;
        PausePressed = false;
        JumpPressed = false;
        DashPressed = false;
        RunHeld = false;
        ToggleViewPressed = false;
    }
}