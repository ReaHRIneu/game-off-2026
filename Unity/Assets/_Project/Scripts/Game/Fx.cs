using UnityEngine;

namespace GameOff.Game
{
    /// <summary>
    /// P1 코드 연출 값 모음(W-028, vfx-direction §2·§3). 길이·거리는 모두 이 한 곳에서 바꾼다. 값은 전부 [제안].
    /// 시간은 30 FPS 틱(1틱 = 1/30초) 단위, 거리는 논리 px(640x360 기준)이다.
    /// </summary>
    public static class Fx
    {
        public const float TickSeconds = 1f / 30f;

        /// <summary>경과 시간(초)을 30 FPS 틱 번호로 바꾼다(음수면 음수).</summary>
        public static int TickOf(float seconds) => Mathf.FloorToInt(seconds * 30f);

        // 1-1 조준 점프: 조준틀이 이전 섹터에서 새 섹터로 보간 이동(틱 0, 1은 1/3·2/3 지점, 틱 2에 도착) + 도착 틱에 모서리 4개 흰색 1px 반짝
        public const int AimMoveTicks = 2;          // 0.07초
        public const int AimFlashTicks = 1;         // 도착 틱 1개

        // 1-2·1-3 Shift 누름·뗌: 설계도 겹침이 옆에서 들어오고(누름 +x) 반대쪽으로 밀리며 나감(뗌 -x)
        public const int BlueprintSlideTicks = 2;   // 0.07초
        public const int BlueprintSlidePx = 4;

        // 1-4 추적도 게이지 끝 칸 깜빡임(Shift를 누르는 동안): 주기 15틱(초당 2회), 앞 8틱 켜짐·뒤 7틱 꺼짐
        public const int GaugeBlinkPeriodTicks = 15;
        public const int GaugeBlinkOnTicks = 8;
        public const int GaugeCellPx = 4;           // 게이지 칸 묶음(4px)

        // 1-5·1-6 장면 뷰 테두리 띠
        public const float WarnRatio = 0.7f;        // 추적도 70% 이상: 빨간 디더 1px 띠
        public const int CountdownBandPx = 2;       // 카운트다운 중: 빨간 실선 2px 띠
        public const int CountdownBaseScale = 3;    // 카운트다운 숫자 기본 글자 배율
        public const int CountdownPopScale = 4;     // 숫자가 바뀐 뒤 앞 3틱 동안의 배율(3 -> 4 -> 3)
        public const int CountdownPopTicks = 3;

        // 1-7 결과 창: 아래에서 올라옴. 별은 0.15초 간격(소리와 같은 값)
        public const int ResultRisePx = 8;
        public const int ResultRiseTicks = 3;       // 0.1초
        public const float StarSeconds = 0.15f;

        // 1-8 미션 지점 체크 아이콘(9x9): 처음 2틱에 4px 올라가 머물다가 12틱(0.4초)에 사라짐
        public const int CheckTicks = 12;
        public const int CheckRisePx = 4;
        public const int CheckRiseTicks = 2;

        // 1-9 실패 지점: 빨간 반투명 사각형 0.1초 켜짐·0.1초 꺼짐 x2 = 12틱(0.4초) 뒤 재시작
        public const int FailOnTicks = 3;
        public const int FailOffTicks = 3;
        public const int FailBlinks = 2;
        public const int FailTotalTicks = (FailOnTicks + FailOffTicks) * FailBlinks;
        public const float FailSeconds = FailTotalTicks / 30f;
        public const float FailAlpha = 0.45f;

        // 1-10 가두기 성공(챕터 0): 결과 창 직전 TRAPPED 글자 도장
        public const float TrappedSeconds = 0.3f;
        public const int TrappedScale = 2;
        public const int TrappedPadding = 6;

        // 3 게임 시작 CRT 켜짐(D-109 ⑤): 검은 페이드 아웃 -> 가로선 펼침 -> 위아래로 열림
        public const float CrtFadeSeconds = 0.4f;
        public const float CrtLineSeconds = 0.15f;
        public const float CrtOpenSeconds = 0.3f;
        public const float CrtTotalSeconds = CrtFadeSeconds + CrtLineSeconds + CrtOpenSeconds;
        public const int CrtFadeTicks = 12;
        public const int CrtLineTicks = 4;
        public const int CrtOpenTicks = 9;
        public const int CrtEdgeRows = 4;           // 열리는 가장자리 빛 번짐 줄 수(디더 3줄 + 밝은 선 1줄)

        // W-032 스테이지 선택(D-121): 일반 모드 팝업이 아이콘 오른쪽에서 가로로 펼쳐짐(입력은 막지 않음), 개발 모드 자리 LOCKED 표시 시간
        public const int PopupOpenTicks = 4;        // 0.13초
        public const int ColumnSlideTicks = 4;      // 열 밀기(챕터 포커스 <-> 스테이지 포커스) 0.13초
        public const float DevLockedSeconds = 0.6f;

        // W-030 조작 피드백(D-118). 표지·링은 장면 클립 안, 장치 위 L8 표시 층. 값은 모두 [제안]
        // F1 즉시 확인: Shift를 누른 순간 바뀐 장치 위(그림 위 4px)에 상태 표지, Shift를 누르는 동안 계속. 나타날 때 2틱 테두리 흰색 + 장치 그림 테두리 1px 흰색
        public const int MarkerFlashTicks = 2;
        public const int MarkerGapPx = 4;           // 장치 그림 위쪽과 표지 아래 사이
        public const int MarkerStepPx = 12;         // 표지끼리 겹치면 위로 올리는 한 줄 높이
        public const int MarkerPadX = 2;            // 글자 좌우 안쪽 여백(테두리 1px 별도)
        public const int MarkerHeightPx = 11;       // 글자 7px + 위아래 1px 여백 + 테두리 1px씩
        // F5 효과 없음: 조준틀 위쪽 가운데에 회색 표지 0.6초(누르는 동안 다시 띄우지 않음)
        public const float NoEffectSeconds = 0.6f;
        public const string NoEffectText = "NO EFFECT";
        public const string RailHintText = "PRESS ← ↑ → ↓";
        // F3 작동 순간: 민트색 사각 링이 2px -> 10px로 퍼지며 사라짐 0.3초(9틱) + 명령 기록 한 줄
        public const int UseRingTicks = 9;
        public const int UseRingStartPx = 2;
        public const int UseRingEndPx = 10;
        public const int UseRingMax = 12;           // 동시에 그리는 링 수 상한
        public const int UseRingSamePlacePx = 6;    // 같은 층에서 이 거리 안이면 같은 위치로 보고 새 링이 이전 링을 끝낸다

        /// <summary>명령 기록에 남기는 사건 한 줄(">" 는 기록 쪽이 붙인다).</summary>
        public static string UseText(UseKind k)
        {
            switch (k)
            {
                case UseKind.DoorPassed: return "DOOR PASSED";
                case UseKind.ShutterPassed: return "SHUTTER PASSED";
                case UseKind.HatchPassed: return "HATCH PASSED";
                case UseKind.StairsSkipped: return "STAIRS SKIPPED";
                case UseKind.LaserPassed: return "LASER PASSED";
                case UseKind.CameraEvaded: return "CAMERA EVADED";
                case UseKind.ElevatorUsed: return "ELEVATOR USED";
                case UseKind.ElevatorSkipped: return "ELEVATOR SKIPPED";
                case UseKind.RoomBridged: return "ROOM BRIDGED";
                default: return "CARRIED";
            }
        }

        /// <summary>결과 창이 올라오는 중의 아래 어긋남(px). 시작 8, 끝 0(틱 3부터).</summary>
        public static int ResultOffset(float windowSeconds)
        {
            int k = TickOf(windowSeconds);
            if (k < 0) return ResultRisePx;
            if (k >= ResultRiseTicks) return 0;
            return Mathf.RoundToInt(ResultRisePx * (1f - k / (float)ResultRiseTicks));
        }
    }
}
