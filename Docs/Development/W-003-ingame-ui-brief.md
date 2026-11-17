# W-003 게임 화면 UI 구조 변경·도트 아트 적용·시작/종료 대화 지시서

- **작성일**: 2026-11-17
- **작성**: Claude Code(메인, 설계) / **수행**: producer
- **근거 문서**: `Docs/Planning/05-01-ui-layout.md` v0.4.0(배치·대화·시스템 키, D-042~D-051), `Docs/Planning/02-01-system-design.md` v0.3.0(규칙), `Docs/Planning/05-02-screen-flow.md` v0.2.0(흐름)
- **목표 화면(시안)**: `Art/Export/preview/ui_mockup_mission_x2.png`, `Art/Export/preview/ui_mockup_log_x2.png`
- **따를 규칙**: 저장소 루트 `AGENTS.md`, 특히 공개 저장소 보안 규칙

## 목표

의뢰(게임) 화면을 IMGUI 임시 도형에서 **W-002의 640×360 도트 캔버스**로 옮기고, UI 배치를 05-01 구조로 바꾼다. 의뢰 시작·종료 때 문자 메시지 형식 대화를 넣는다. **게임 규칙은 바꾸지 않는다.**

## 범위

### 1. 화면 배치 (목업 기준, 좌표는 `Art/recipes/ui_mvp.py`의 `mockup()`과 같게)

| 영역 | 내용 |
| --- | --- |
| 창 틀·제목줄 | `ui/window_frame` 9-slice, 제목 `SHIFT://REMOTE  -  REQUEST 01`(의뢰 번호는 데이터에서), 오른쪽 위 일시정지 아이콘 + `ESC` 배지(D-051) |
| 왼쪽 장면 패널 | `ui/panel`, 머리글 `FLOOR VIEW / CAM 03`. 격자를 **32px 타일**(`tiles/reality_*`)로 그림. 의뢰 대상 `sprites/courier_left/right`(위·아래 이동 중에는 마지막 좌우 방향 유지), 조준틀 `sprites/cursor` |
| 설계도 겹침 | 왼쪽 Shift를 누르는 동안 조준한 칸 위에 `tiles/blueprint_*`를 겹친다(설계도 상태의 칸 종류·방향에 맞는 그림). 현실 타일은 그대로 아래에 보인다(D-015) |
| 오른쪽 위 공용 패널 | 탭 `[MISSION] [LOG]`(`ui/tab_active`·`tab_inactive`), 탭 전환 아이콘 + `]` 배지. **`]` 키**(물리 위치, `Key.RightBracket`)로 탭 전환(D-050) |
| MISSION 탭 | 미션 목록(`ui/checkbox_empty`·`checkbox_done`), 최종 미션 표시 `(FINAL)` |
| LOG 탭 | 문자 메시지 말풍선(`ui/bubble_left` 의뢰인, `ui/bubble_right` 해커), 말풍선 위에 이름·시각(D-046). 넘치면 오래된 말풍선부터 위로 밀려 사라짐 |
| 오른쪽 아래 얼굴 패널 | 96×96 초상 `portraits/hacker_<표정>`(D-042, D-049), 이름 `HACKER`·`???` |
| 아래 줄 | 추적도 게이지(목업 `gauge()`처럼 칸 나뉜 빨간 막대, 코드로 그려도 됨)·명령 기록 줄(`> LSHIFT` 등) |
| 카운트다운 | 장면 패널 오른쪽 위에 큰 숫자(5×7 글자 3배, 빨강 `R`) |
| 일시정지·결과·게임오버 | 장면 패널 가운데 `ui/window_frame` 창 + `ui/button`·`button_selected` 메뉴(방향키 선택·Enter). 결과 창에 별(`ui/star_full`·`star_empty`) |

### 2. 시작·종료 대화 (D-045, D-046)

- 흐름: 의뢰 진입 → **브리핑 대화**(LOG 탭 자동 표시, 게임 정지) → 대화 끝 → MISSION 탭으로 돌아가며 시작 대기 3초 후 플레이 → 최종 미션 완료 → **종료 대화** → 결과 창
- 게임오버 때는 종료 대화 없이 게임오버 창(D-031 처음부터 재시작은 기존대로, 재시작 때 브리핑 대화는 **건너뛴다** `[제안]`)
- 대사 넘기기: `Enter`. 대화 중 LOG 탭 아래쪽에 `NEXT` 버튼 + `ENTER` 배지
- 플레이 중 `]`로 LOG 탭을 열면 브리핑 기록이 보인다
- 대사 데이터: 의뢰별 데이터 에셋(줄마다 화자 `CLIENT`/`ME`, 시각 글자, 내용, 해커 표정). 테스트 의뢰의 **임시 대사**(시나리오 미정 — 사용자가 나중에 교체):

| 단계 | 화자 | 시각 | 내용 | 해커 표정 |
| --- | --- | --- | --- | --- |
| 브리핑 | CLIENT | 21:04 | PACKAGE IS LATE. OUR COURIER IS STUCK. | neutral |
| 브리핑 | ME | 21:04 | ON IT. | neutral |
| 브리핑 | CLIENT | 21:05 | NO TRACE. PLEASE. | neutral |
| 브리핑 | ME | 21:05 | NO PROMISES. | disgust |
| 종료 | CLIENT | 21:09 | GOT IT. PAYMENT SENT. | neutral |
| 종료 | ME | 21:09 | LOGGING OFF. | joy |

### 3. 얼굴 표정 규칙 `[제안]`

- 대화 중: 대사 데이터의 해커 표정
- 플레이 중: 추적도 70 미만 `neutral`, 70 이상 `surprise`, 카운트다운 중 `fear`
- 결과: ★3 `joy`, 그 외 `neutral`. 게임오버: `sadness`
- 표정 바뀜은 즉시(연출 없음)

### 4. 정리

- 기존 IMGUI 게임 화면 코드는 지우고 도트 캔버스 하나로 그린다(메뉴 화면과 같은 렌더링 경로)
- 글꼴 아틀라스에 `_` 글리프가 추가되었다. `GameOff/Import Art`를 다시 실행해 글자 좌표를 갱신한다(QA T-017)
- 시간·임계값(표정 70 등)은 설정 에셋에 둔다

## 금지 사항

- 게임 규칙(층 어긋남·조준 점프·대상 이동·추적도·미션·별) 변경
- PNG 수정·새 그림 그리기(부족하면 질문 목록에)
- 대사 내용 창작(위 임시 대사만 사용)
- 커밋·push, 개인 정보·로컬 절대 경로·키 기록

## 완료 기준

1. 컴파일 오류 0
2. 작동 확인: 기존 63개 항목 다시 통과 + 추가 — 브리핑 대화 Enter 진행 후 플레이 시작, `]` 탭 전환(MISSION ↔ LOG), Shift 중 설계도 타일 겹침, 표정 전환(추적도 70·카운트다운·결과·게임오버), 종료 대화 → 결과, 재시작 시 브리핑 건너뜀
3. 캡처 `Docs/QA/captures/W-003/`: 브리핑 대화, 플레이(MISSION 탭 + Shift 겹침), LOG 탭(플레이 중), 카운트다운, 종료 대화, 결과, 게임오버, 일시정지
4. 게임 화면 캡처는 가능하면 실제 Game 뷰 출력(정수 배율·레터박스 포함)으로 1장 이상 — W-002에서 미확인이었던 부분
5. Web 빌드 1회, 빌드 결과 로그(`result=`)를 확인해 보고
6. 미확인 항목은 「미확인」으로 보고. 작동 확인을 「테스트 통과」로 보고하지 않음

## 보고 형식

W-002와 같다.
