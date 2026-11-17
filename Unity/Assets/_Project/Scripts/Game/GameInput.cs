using UnityEngine.InputSystem;

namespace GameOff.Game
{
    /// <summary>한 프레임의 입력 상태. 방향키·Enter·Esc·클릭은 이번 프레임에 눌렸는지, LeftShift는 누르는 중인지.</summary>
    public struct InputFrame
    {
        public bool LeftShift;
        public bool Left, Right, Up, Down;
        public bool Enter, Esc;
    }

    public static class GameInput
    {
        /// <summary>자동 확인용 덮어쓰기. null이 아니면 실제 키보드 대신 이 값을 쓴다.</summary>
        public static System.Func<InputFrame> Override;

        public static InputFrame Read()
        {
            return Override != null ? Override() : ReadReal();
        }

        /// <summary>Input System의 실제 키보드·마우스 상태를 읽는다.</summary>
        public static InputFrame ReadReal()
        {
            var f = new InputFrame();
            var k = Keyboard.current;
            if (k != null)
            {
                // 물리 키 기준: leftShiftKey만 본다(rightShiftKey는 읽지 않음).
                f.LeftShift = k.leftShiftKey.isPressed;
                f.Left = k.leftArrowKey.wasPressedThisFrame;
                f.Right = k.rightArrowKey.wasPressedThisFrame;
                f.Up = k.upArrowKey.wasPressedThisFrame;
                f.Down = k.downArrowKey.wasPressedThisFrame;
                f.Enter = k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame;
                f.Esc = k.escapeKey.wasPressedThisFrame;
            }
            return f;
        }
    }
}
