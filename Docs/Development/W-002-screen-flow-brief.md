# W-002 메인·의뢰 선택 화면과 화면 흐름 연결 지시서

- **작성일**: 2026-11-17
- **작성**: Claude Code(메인, 설계) / **수행**: producer
- **근거 문서**: `Docs/Planning/05-02-screen-flow.md` **v0.2.0**(흐름·화면 정본, D-056~D-059), `Docs/Planning/05-01-ui-layout.md` v0.4.0(시스템 키 표시 D-051), `Docs/Art/art-pipeline.md` v0.6.0(반입 규칙)
- **목표 화면(시안, v0.2.0 기준)**: `Art/Export/preview/scene_main_off_x2.png`(메인), `scene_main_boot_x2.png`(모니터 켜짐·로딩), `scene_desktop_x2.png`(바탕화면), `scene_desktop_panel_x2.png`(상세 패널), `scene_desktop_connecting_x2.png`(게임 진입 로딩). `screen_*.png`(도시 배경 메인·게시판)는 **폐기된 이전 시안**
- **따를 규칙**: 저장소 루트 `AGENTS.md` 전체, 특히 공개 저장소 보안 규칙

## 목표

게임을 실행하면 **메인 → 의뢰 선택 → 의뢰(기존 W-001 게임) → 결과 → 의뢰 선택**으로 이어지는 화면 흐름을 만든다. 메인·의뢰 선택·크레딧 화면은 도트 아트(코드로 만든 PNG)로 그린다.

## 범위

### 1. 아트 반입 기반

- `Art/Export/`의 `backgrounds/`, `ui/`, `tiles/`, `sprites/`, `portraits/` PNG를 `Unity/Assets/_Project/Art/` 아래 같은 하위 폴더로 복사한다. `preview/`는 복사하지 않는다.
- 임포트 설정을 경로 기준으로 자동 적용하는 Editor 스크립트(AssetPostprocessor)를 만든다: Sprite(2D and UI), PPU 32, Filter Point, Compression None, Mip Map 끔. 9-slice 테두리는 `Art/Export/manifest.json`의 `nine_slice_LTRB` 값을 쓴다.
- 다시 복사할 수 있도록 복사 메뉴(`GameOff/Import Art`)를 만든다.

### 2. 도트 렌더링

- 기준 해상도 **640×360**, **정수 배율**로 확대, 남는 부분은 검은 레터박스. 필터링·서브픽셀 위치 금지(그림 1픽셀 = 화면 정수 픽셀).
- 구현 방식(Pixel Perfect Camera + SpriteRenderer, 640×360 RenderTexture 등)은 producer가 고르고 보고에 적는다.
- 글자: `Art/Export/ui/font5x7_atlas.png`와 manifest의 `glyphs` 좌표로 간단한 비트맵 글자 그리기 기능을 만든다(대문자만 있음. 입력 문자열은 대문자로 바꿔 그림).

### 3. 화면

| 화면 | 내용 | 조작 |
| --- | --- | --- |
| 메인 | 배경 레이어 6장 겹침(`city_layer0~5`), 로고 `ui/logo_temp.png`, 부제, 메뉴 START·CREDITS(`ui/button`·`button_selected`), 아래 키 안내 | ↑↓ 선택, Enter 결정 |
| 의뢰 선택 | 창 틀·패널, 왼쪽 의뢰 카드 4장(상태·별), 오른쪽 상세(요약·미션·최고 별)·ACCEPT 버튼, 아래 키 안내 | ↑↓ 선택, Enter 수락(잠긴 의뢰는 수락 불가), ESC 메인 |
| 크레딧 | 창 틀 안에 글자: `MADE FOR GITHUB GAME OFF 2026`, `TOOLS: UNITY 6, CLAUDE CODE`, `AI DISCLOSURE: CODE AND PIXEL ART MADE WITH AI ASSISTANCE (CLAUDE CODE)`, `CREATOR: TBD` | ESC 메인 |
| 의뢰(기존) | W-001 게임 화면 그대로(아직 IMGUI 도형 화면이어도 됨) | 기존 |

- 시스템 키 안내는 시안처럼 `badge_*` 그림 + 설명 글자로 화면 아래에 표시한다(D-051). `↑↓`, `ENTER`, `ESC`
- 시안과 픽셀 단위로 같을 필요는 없다. 배치·색·부품은 시안을 따른다.

### 4. 의뢰 목록과 진행 저장

- 의뢰 목록 데이터 에셋(ScriptableObject)을 만든다. 항목: id, 의뢰인, 제목, 요약 문장, 미션 이름, 레벨 데이터 참조(없으면 잠긴 빈 자리).
- #01 = 기존 `TestRequest`(의뢰인 `COURIER GUILD`, 제목 `LATE PACKAGE`, 요약 3줄은 시안 문구). #02~#04 = 레벨 없음, 화면에는 `???`·LOCKED.
- 저장: 의뢰별 최고 별만 PlayerPrefs에 저장. 저장 읽기·쓰기는 예외 처리해 실패해도 진행. 잠금 해제: 앞 의뢰 ★1 이상이면 다음 의뢰 열림(레벨이 있는 의뢰만 실제 수락 가능).

### 5. 흐름 연결

- 결과 화면에서 Enter → 의뢰 선택(최고 별 갱신 반영)
- 게임오버 화면은 기존대로 Enter → 처음부터 다시(D-031)
- 일시정지 메뉴에 `REQUEST BOARD` 항목 추가(계속 / 다시 시작 / 의뢰 선택으로)
- 메인에서 ESC는 아무 동작 없음

## 2026-11-17 변경 — 화면 디자인 교체(D-056~D-059)

범위 3의 메인·의뢰 선택 화면을 아래로 바꾼다. 범위 1·2·4·5는 그대로다.

| 단계 | 그림(레이어, 뒤 → 앞) | 동작 |
| --- | --- | --- |
| 메인 | `scenes/room_layer0_wall` → `room_layer1_desk` → `room_layer2_monitor_off` → `room_layer4_hacker`. 왼쪽 위에 로고(`ui/logo_temp.png`를 정수 배율 1/2로 축소해도 됨)와 START·CREDITS 버튼, 아래 키 안내 | ↑↓ 선택, Enter 결정 |
| 모니터 켜짐·로딩 | `room_layer3_light_on`(벽 위), `room_layer2_monitor_on`, `room_layer4_hacker_lit`로 바꾸고, 모니터 화면 위에 `room_screen_boot_0~3`을 차례로 표시(합계 약 1.5초 `[제안]`). 메뉴는 숨김 | 입력 없음 |
| 바탕화면 | `scenes/desktop_wallpaper` + 의뢰 아이콘(`ui/icon_file_clear/new/locked`) 세로 배치 + 이름표(`REQ_01`…) + 별 + 작업 표시줄(키 안내·시계 `21:04` 고정 글자) | ↑↓ 아이콘 선택, Enter 상세 패널 열기, ESC 메인(모니터 꺼짐) |
| 상세 패널 | 오른쪽에서 창이 나타남(창 틀 `ui/window_frame` 9-slice + 글자 직접 그리기 — `ui/desktop_detail_panel.png`는 목업 참고용). CONNECT 버튼 + `ENTER` 배지 | Enter 한 번 더 → 게임 진입 로딩, ESC 패널 닫기. 잠긴 의뢰는 `LOCKED`만 표시하고 Enter 무시 |
| 게임 진입 로딩 | 바탕화면 가운데 `CONNECTING TO CAM 03...` 창 + 진행 막대(약 1초 `[제안]`) | 입력 없음 |
| 결과 후 | Enter → 바탕화면(상세 패널 닫힌 상태, 별 갱신) | |

- 등장 연출(패널 미끄러져 나옴 등)은 정수 픽셀 단위로 움직인다. 시간 값은 GameSettings 같은 설정 에셋에 둔다.
- 일시정지 메뉴 항목 이름은 `REQUEST BOARD` 대신 `DESKTOP`.
- 크레딧 화면은 바탕화면 그림(`desktop_wallpaper`) 위 창 하나로 표시해도 된다.
- 캡처: 메인, 로딩(중간 단계), 바탕화면, 상세 패널, 게임 진입 로딩, 크레딧

## 금지 사항

- 게임 규칙(층 어긋남·추적도·미션·별) 변경, 의뢰 게임 화면의 UI 구조 변경(그건 W-003)
- 새 그림을 직접 그리거나 PNG를 수정하는 것(그림이 부족하면 질문 목록에 적기)
- 커밋·push, 개인 정보·로컬 절대 경로·키 기록, 외부 에셋·글꼴 반입

## 완료 기준

1. 컴파일 오류 0
2. 작동 확인: 기존 W-001 확인 스크립트를 넓혀서 다음을 자동 확인 — 메인 → START → 의뢰 선택, 잠긴 의뢰 Enter 무시, #01 수락 → 게임, 클리어 결과 Enter → 의뢰 선택(별 표시 갱신), ESC → 메인, CREDITS 왕복, 일시정지 → REQUEST BOARD. 기존 37개 항목도 다시 통과해야 함
3. 캡처: 메인·의뢰 선택·크레딧 화면을 `Docs/QA/captures/W-002/`에 저장(640×360 정수 배율)
4. Web 빌드 1회 성공 확인
5. 미확인 항목은 「미확인」으로 보고. 작동 확인을 「테스트 통과」로 보고하지 않음

## 보고 형식

W-001과 같다: 바꾼 파일 목록(저장소 기준 상대 경로), 완료 기준별 실제 결과, 렌더링 방식 선택과 이유, 미확인 항목, 설계 판단 질문, QA 항목 제안.
