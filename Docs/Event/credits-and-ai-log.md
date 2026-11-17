# 크레딧과 AI 사용 기록

- **버전**: v0.1.0
- **담당 AI**: Claude Code

행사 규칙상 제출 때 AI 사용을 공개합니다. 제3자 에셋은 라이선스와 출처를 남깁니다.

## AI 사용

| 날짜 | 도구 | 용도 | 대상 파일·범위 |
| --- | --- | --- | --- |
| 2026-11-17 | Claude Code | 기획 문서 정리, 저장소·Unity 프로젝트 생성 | `AGENTS.md`, `README.md`, `Docs/` |
| 2026-11-17 | Claude Code | 코드 기반 아트 도구 작성, MVP-1 타일 시안 생성(Pillow, 코드 생성 — 이미지 생성 AI 아님) | `tools/art/`, `Art/recipes/tiles_mvp1.py`, `Art/Export/` |
| 2026-11-17 | Claude Code | MVP-1 도트 타일·스프라이트 시안으로 재작성(코드로 픽셀 단위 작성 — 이미지 생성 AI 아님) | `tools/art/artkit.py`, `Art/recipes/tiles_mvp1.py`, `Art/Export/` |
| 2026-11-17 | Claude Code | 도시 야경 배경 시안(코드로 픽셀 단위 작성, 고정 시드 — 이미지 생성 AI 아님) | `Art/recipes/bg_city.py`, `Art/Export/backgrounds/` |
| 2026-11-17 | Claude Code | 타일·스프라이트 32px 재작성, 배경 640×360 레이어 6장 재작성(코드 생성 — 이미지 생성 AI 아님) | `Art/recipes/`, `tools/art/artkit.py`, `Art/Export/` |
| 2026-11-17 | Claude Code | 주인공 초상 96×96 표정 7종 시안(코드로 픽셀 단위 작성 — 이미지 생성 AI 아님) | `Art/recipes/portrait_hacker.py`, `Art/Export/portraits/` |
| 2026-11-17 | Claude Code | UI 아트 부품·5×7 도트 글꼴·화면 목업 시안(코드로 픽셀 단위 작성 — 이미지 생성 AI·외부 글꼴 아님) | `Art/recipes/ui_mvp.py`, `tools/art/pixfont.py`, `Art/Export/ui/` |
| 2026-11-17 | Claude Code | 메인·의뢰 선택 화면 목업, 임시 로고(코드로 픽셀 단위 작성) | `Art/recipes/ui_screens.py` |
| 2026-11-17 | Claude Code | 메인 방 일러스트·모니터 로딩·바탕화면 아트 시안(코드로 픽셀 단위 작성) | `Art/recipes/ui_scenes.py`, `Art/Export/scenes/` |
| 2026-11-17 | Claude Code(producer, Sonnet) | W-001 MVP-1 게임 코드, W-002 화면 흐름·도트 렌더링·아트 반입·저장 코드(AI 코드 생성) | `Unity/Assets/_Project/Scripts/` |
| 2026-11-17 | Claude Code(producer, Sonnet) | W-003 게임 화면 도트 렌더링·UI 구조·시작/종료 대화 코드(AI 코드 생성). 대사는 임시 문구 | `Unity/Assets/_Project/Scripts/` |
| 2026-11-17 | Claude Code | 측면 단면도 테스트 맵·오브젝트·게임 화면 목업(코드로 픽셀 단위 작성) | `Art/recipes/scene_building.py`, `Art/Export/objects/` |
| 2026-11-17 | Claude Code(producer, Sonnet) | W-004 측면 단면도 섹터 모델(맵 데이터·규칙·카메라·장면 그리기) 코드(AI 코드 생성) | `Unity/Assets/_Project/Scripts/` |
| 2026-11-17 | Claude Code(producer, Sonnet) | W-005 챕터 0 의뢰 데이터·튜토리얼 안내·챕터 폴더 코드(AI 코드 생성). 대사·안내 문구 임시 | `Unity/Assets/_Project/` |

## 제3자 에셋

| 에셋 | 출처 | 라이선스 | 사용 위치 |
| --- | --- | --- | --- |
| (없음) | | | |
