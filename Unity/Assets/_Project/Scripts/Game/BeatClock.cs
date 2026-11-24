namespace GameOff.Game
{
    /// <summary>
    /// 박자 시계 값(W-035, D-112 시간 위상 어긋남, 02-04 §2-3). 챕터 2 레벨에서만 쓰며, 값을 바꿔 보려면 이 파일 한 곳만 고친다.
    /// 시계는 게임 시간(GameSim.Step)으로만 흐르므로 일시정지·튜토리얼 멈춤 중에는 멈춘다.
    /// </summary>
    public static class BeatClock
    {
        /// <summary>1박 길이(초) [제안]. 주기 = Beats x BeatSeconds = 2.4초.</summary>
        public const float BeatSeconds = 0.6f;
        /// <summary>주기 박 수 [제안]. 4박 앞당김 = 원래와 같으므로 Shift + 숫자는 1~MaxPhase.</summary>
        public const int Beats = 4;
        public const int MaxPhase = 3;
        /// <summary>Shift 중 추적도 상승 = 기본 x 레벨 배율 x (1 + 이 값 x 앞당긴 박 수) [제안, 03-02에서 확정].</summary>
        public const float PhaseTrackingPerBeat = 0.25f;
        /// <summary>프레스 폭의 절반(px) [제안]. 대상 중심이 프레스 가운데에서 이 거리 안이면 「아래에 있음」.</summary>
        public const float PressHalfWidth = 12f;

        /// <summary>프레스 주기 표 [제안]: 올라감, 올라감, 내려옴, 내려옴. true = 내려옴.</summary>
        static readonly bool[] PressDownTable = { false, false, true, true };

        /// <summary>그 박(음수 포함, 몇 번째 박이든)에서 프레스가 내려와 있는가.</summary>
        public static bool PressDownAt(int beat) => PressDownTable[((beat % Beats) + Beats) % Beats];

        /// <summary>주기 안의 번호(0~3).</summary>
        public static int Slot(int beat) => ((beat % Beats) + Beats) % Beats;
    }
}
