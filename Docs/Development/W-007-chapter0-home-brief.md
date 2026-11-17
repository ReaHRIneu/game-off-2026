# W-007 새 챕터 0(주인공 건물 침입자 저지) 재구현 지시서

- **작성일**: 2026-11-18
- **작성**: Claude Code(메인, 설계) / **수행**: producer
- **근거 문서**: `Docs/Planning/03-01-level-design.md` v0.4.1 **§1-A~§1-C-2**(정본: 배치표·미션·단계 안내), `Docs/Planning/07-01-scenario.md` v0.1.0 §5(대사), `Docs/Planning/02-05-mission-design.md`(미션 종류), 결정 D-069~D-072
- **시안**: `Art/Export/preview/ch0_home1_map_x2.png`, `ch0_home2_map_x2.png`, `runner_x4.png`, 배치 좌표 `Art/recipes/scene_home.py`
- **따를 규칙**: `AGENTS.md`(보안 규칙, **규칙 8: 빌드하지 않음**)

## 목표

W-005의 SANDBOX 챕터 0을 **주인공 건물 침입자 저지 챕터 0**으로 바꾼다. 화면 속 인물은 이제 **침입자 = 이후 조력자**(D-070) 한 사람이다. 섹터 모델 규칙(문·계단·엘리베이터·차단막·조준·추적도)은 그대로 쓰고, 아래 새 기능만 더한다.

## 범위

### 1. 화면 속 인물 교체

- 모든 의뢰의 대상 그림을 `sprites/runner_right`·`runner_left`로 바꾼다(테스트 의뢰 포함). `courier_*`는 지우지 않음

### 2. 새 기능

| 기능 | 내용 |
| --- | --- |
| 실패 지점 | 레벨 데이터의 오브젝트 종류 하나. 인물 중심이 그 x를 같은 층에서 지나가면 **실패** → 결과 창 없이 그 의뢰를 **바로 처음부터 다시 시작**(브리핑 건너뜀, 기존 재시작과 같음). 다시 시작 직후 안내 상자에 `THEY REACHED YOU. TRY AGAIN.`을 2초 표시 |
| 해치(아래로 이어지는 연결) | 계단과 같은 연결 규칙(지나가는 순간 유효 상태가 열림이면 연결 섹터로 이동, 도착 방향 지정). 그림 `objects/hatch_closed`·`hatch_open`, 설계도 겹침 `objects/bp_hatch_open`. 그림은 바닥선에 놓임(높이 10px, 시안 `preview()` 좌표) |
| 미션 종류 목록화 | 미션을 "최종 + 단말 A·B 고정"에서 **목록**으로 바꾼다. 종류: `ENTER_SECTOR`(최종 — 인물이 그 섹터에 들어감), `PASS_OBJECT`(단말 통과, 기존), `NEVER_ENTER_FLOOR`(그 층에 들어가는 순간 실패 확정), `TRACE_LIMIT`(추적도가 기준을 넘는 순간 실패 확정). 기존 테스트 의뢰는 목록 형식으로 옮김 |
| 미션 표시 | 실패 확정 미션은 체크 상자에 **빨간 X**(코드로 그림, 팔레트 `R`)와 흐린 글자. `TRACE_LIMIT`은 이름 뒤에 현재 값 `(42)` 표시 |
| 별 | 기존 규칙(D-030) 그대로: 완료한 나머지 미션 수로 계산(실패 확정 = 미완료) |
| 추적도 감소 | 탐색 미션(`PASS_OBJECT`) 완료 때만 감소. 솜씨 미션(`NEVER_ENTER_FLOOR`, `TRACE_LIMIT`)은 감소 없음 `[제안 — 02-05 §2]` |

### 3. 의뢰 데이터 (03-01 §1-C-1·§1-C-2 표 그대로)

| 항목 | 0-1 | 0-2 |
| --- | --- | --- |
| 배경 | `scenes/ch0_home1_base.png` | `scenes/ch0_home2_base.png` |
| 건물 이름(머리글) | `HOME BLDG` | `HOME BLDG` |
| 의뢰 제목 / 의뢰인 | `NIGHT INTRUDER` / `HOME SYSTEM` | `LOCKDOWN` / `HOME SYSTEM` |
| 요약 `[임시]` | `SOMEONE BROKE IN.` / `KEEP THEM AWAY FROM YOU.` / `TRAP THEM BELOW.` | `THEY GOT OUT.` / `STOP THE ELEVATOR.` / `SEND THEM TO THE BOILER ROOM.` |
| 미션 | `TRAP THE INTRUDER`(ENTER_SECTOR B1 열 2), `RECORD THE FACE`(PASS_OBJECT 보안 단말), `KEEP THEM OFF 2F`(NEVER_ENTER_FLOOR 층 0) | `TRAP THE INTRUDER`(ENTER_SECTOR B1 열 3), `TRACE <= 40`(TRACE_LIMIT 40), `KEEP THEM OFF 3F`(NEVER_ENTER_FLOOR 층 0) |
| 장치 | 계단(1,3) 현실 열림 → 설계도 닫힘, 해치(1,2) 닫힘 → 열림 | 엘리베이터(1,1) 작동 → 멈춤(연결 (0,1), 도착 왼쪽), 레이저(1,2) 섹터 전체 켜짐 → 꺼짐, 해치(1,3) 닫힘 → 열림 |
| 실패 지점 | (0,0) x 120 | (0,0) x 120 |
| 튜토리얼 | 켬(게임오버 없음 — 추적도 최대 경고는 기존대로) | 켬 |

- 바탕화면 폴더 이름 `CH0_HOME`. SANDBOX 의뢰 2개는 목록에서 빼고 에셋은 지우지 않음
- 대상이 B1 함정 섹터에 들어가면 최종 미션 완료 → 종료 대화 → 결과

### 4. 대사 (07-01 §5 표 그대로)

화자 `SYSTEM`(왼쪽 말풍선), `???`(왼쪽 말풍선, 조력자 — 이름 공개 전), `ME`. 표정은 표의 값.

### 5. 단계 안내 (03-01 §1-C-1·§1-C-2의 문구·조건 그대로)

- 0-1 ③의 `←` 배지는 `↑↓` 처럼 화살표 글자로 그림(→ 처리와 같은 방식)

## 금지

빌드, 지시서 밖 규칙 추가, PNG 수정(빨간 X는 코드 그리기 허용), 문구·대사 창작, 커밋·push, 개인 정보·로컬 경로·키 기록

## 완료 기준

1. 컴파일 오류 0
2. 작동 확인(자동): 기존 항목(의미가 사라진 SANDBOX 항목은 빼고 보고) + 실패 지점 도달 → 즉시 재시작·안내 문구, 해치 이동(열림일 때만), 0-1 ★1·★3 흐름, 0-1 2층 진입 시 `KEEP THEM OFF 2F` 실패 확정(빨간 X)·별 반영, 0-2 엘리베이터 멈춤·레이저·중계기·해치 흐름, `TRACE <= 40` 실패 확정과 현재 값 표시, 탐색 미션만 추적도 감소, 인물 그림 교체
3. 캡처 `Docs/QA/captures/W-007/`: 0-1 시작(안내 1), 0-1 해치 조준(안내 3), 실패 후 재시작 안내, 0-1 결과, 0-2 레이저 앞, 0-2 미션 실패 X 표시, 0-2 결과 — Game 뷰 1장 이상
4. 미확인은 「미확인」으로 보고. 작동 확인을 「테스트 통과」로 보고하지 않음
