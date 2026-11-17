# W-006 장면 화면 다듬기 지시서

- **작성일**: 2026-11-18
- **작성**: Claude Code(메인) / **수행**: producer
- **근거**: W-005 검수 결과(QA T-029), `Docs/Development/W-005-chapter0-brief.md`
- **따를 규칙**: `AGENTS.md`(보안 규칙, **규칙 8: 빌드하지 않음**)

## 범위

1. **맵 세로·가로 가운데 배치**: 맵이 장면 뷰(412×260)보다 작은 방향은 카메라를 고정하고 맵을 뷰 가운데에 그린다(남는 부분은 지금처럼 어두운 바탕). 큰 방향은 기존 카메라 규칙 그대로. 화면 밖 대상 표시·조준틀·오브젝트 좌표도 같은 오프셋으로
2. **건물 이름 표시 위치**: 맵 배경 PNG에서 건물 이름 글자를 뺐다(`Art/Export/scenes/ch0_1_base.png`, `ch0_2_base.png`, `building_test_base.png` 갱신 — `GameOff/Import Art`로 다시 반입). 대신 장면 패널 머리글을 `FLOOR VIEW / <건물 이름>`으로 그린다. 건물 이름은 레벨 데이터 필드(0-1 `SANDBOX BLDG A`, 0-2 `SANDBOX BLDG B`, 테스트 의뢰 `LOGIX CO. BLDG`)
3. **의뢰 요약 문구** `[임시]` — 상세 패널 요약 칸:
   - 0-1: `TRAINING SIM A.` / `OPEN THE DOORS FOR THE COURIER.` / `LEARN TO AIM AND SHIFT.`
   - 0-2: `TRAINING SIM B.` / `STAIRS, SEALED ROOMS AND RELAYS.` / `SOME TERMINALS HIDE OFF THE PATH.`

## 금지

빌드, 규칙 변경, PNG 수정, 위 밖 문구, 커밋·push, 개인 정보·로컬 경로·키 기록

## 완료 기준

1. 컴파일 오류 0
2. 작동 확인: 기존 항목 전부 + 0-1(맵 높이 152) 세로 가운데 배치, 0-2 세로 가운데, 테스트 맵(800×344)은 기존 카메라 동작 유지, 머리글 건물 이름, 요약 문구 표시
3. 캡처 `Docs/QA/captures/W-006/`: 0-1 안내 1단계(가운데 배치·머리글), 0-2 시작, 바탕화면 상세 패널(0-1 요약)
4. 미확인은 「미확인」으로 보고
