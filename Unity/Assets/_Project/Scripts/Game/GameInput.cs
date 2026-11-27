using UnityEngine.InputSystem;

namespace GameOff.Game
{
    /// <summary>한 프레임의 입력 상태. 방향키·Enter·Esc·클릭은 이번 프레임에 눌렸는지, LeftShift는 누르는 중인지.</summary>
    public struct InputFrame
    {
        public bool LeftShift;
        public bool Left, Right, Up, Down;
        public bool Enter, Esc;
        public bool Guide;     // W-049: 'G' 키 — 바탕화면(팝업이 닫힌 상태)에서 도움말(GUIDE) 열기
        public bool Bracket;   // ']' 키(물리 위치): 미션·로그 탭 전환(D-050)
        public int Digit;      // W-035: 이번 프레임에 눌린 숫자 1~3(위쪽 숫자 줄·숫자 패드의 물리 키, 0이면 없음). Shift와 함께 쓰이는 것은 GameView·GameSim이 정한다
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
                f.Bracket = k.rightBracketKey.wasPressedThisFrame;
                f.Guide = k.gKey.wasPressedThisFrame;
                // 물리 키 기준(W-035): Shift + 숫자는 배열에서 ! @ # 로 바뀌지만 Input System은 키 위치(Key.Digit1 등)로 읽는다. 문자 입력(OnGUI)은 쓰지 않는다
                if (k.digit1Key.wasPressedThisFrame || k.numpad1Key.wasPressedThisFrame) f.Digit = 1;
                else if (k.digit2Key.wasPressedThisFrame || k.numpad2Key.wasPressedThisFrame) f.Digit = 2;
                else if (k.digit3Key.wasPressedThisFrame || k.numpad3Key.wasPressedThisFrame) f.Digit = 3;
            }
            return f;
        }
    }
}
