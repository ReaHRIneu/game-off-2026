# (가제 미정) 캐릭터 기획서

- **작성일**: 2026-11-17
- **버전**: v0.1.0
- **연관 문서**: [01-01-concept.md](./01-01-concept.md), [05-01-ui-layout.md](./05-01-ui-layout.md), [Art 파이프라인](../Art/art-pipeline.md)
- **담당 AI**: Claude Code(Opus, 조사·정리)

> 표기: `D-ID`는 사용자 결정, `[제안]`은 에이전트 제안(미확정), `[조사]`는 외부 자료에서 확인한 내용이다. 외부 자료는 직접 플레이·열람하지 않고 검색 결과와 소개문으로 확인했다.

## 1. 범위

- MVP 단계에서는 **주인공(해커) 1명만** 만든다 — D-047
- 디자인은 이 문서(캐릭터 기획)에서 레퍼런스·참고 자료 조사를 거쳐 정한다 — D-048
- 초상 규격: **96×96 도트** — D-049. 오른쪽 아래 얼굴 패널에 출력(D-042)
- 세계관: SF 디스토피아의 뒷세계 해커(D-014). 의뢰를 원격으로 수행(D-008)

## 2. 레퍼런스 조사 `[조사]` — 2026-11-17

### 2-1. 해커 게임의 주인공 표현

| 자료 | 확인 내용 | 시사점 |
| --- | --- | --- |
| Uplink(2001) — [TV Tropes](https://tvtropes.org/pmwiki/pmwiki.php/VideoGame/Uplink) | 주인공은 화면에 나오지 않고 플레이어 자신(가명)으로 취급 | 해커 게임은 주인공을 보여 주지 않는 경우가 많다 → **얼굴을 보여 주는 것 자체가 차별점** |
| Hacknet — [Game Developer](https://www.gamedeveloper.com/design/7-games-with-interesting-hacking-mechanics-all-devs-should-study) | 터미널 테마, 죽은 해커 'Bit'의 지시를 따라가는 이야기 | 인물을 이야기 장치로 쓰는 방식 참고 |
| Jack Move — [GAMINGbible 리뷰](https://gamingbible.com/reviews/jack-move-review-nintendo-switch-pc-playstation-xbox-20221025) | 도트 해커 주인공이 버려진 장비를 주워 쓰는 생활형 인물 | **고물 장비를 덧댄 차림** 같은 생활감 있는 디테일 |
| VA-11 Hall-A — [Hardcore Gaming 101](https://www.hardcoregaming101.net/va-11-hall-a/), [블로그](https://infiniterainyday.blogspot.com/2016/11/va-11-hall-and-japanese-pc-cyberpunk.html) | PC-98풍 레트로 애니 도트 초상, 대화 중심 사이버펑크 | 큰 초상 + 대화 패널 구성, 레트로 애니풍 얼굴 표현 참고 |

### 2-2. 도트 초상 제작 원칙

| 자료 | 확인 내용 | 적용 |
| --- | --- | --- |
| [96×96 캐릭터 제작 글](https://alaingalvan.medium.com/pixel-art-design-for-game-dev-32d22c83a296) 외 | 96×96 캐릭터를 연필 스케치부터 시작, 32색 팔레트(16색도 흔함) | 스케치(실루엣) → 색면 → 명암 순서 |
| [Pixel art basics](https://qqwrxtr.github.io/Minecraft-Skins/tutorials/pixel-art-basics), [androidarts](https://www.androidarts.com/pixtut/pixelart.htm) | 얼굴이 가장 많이 보이는 부분. 눈을 1픽셀 옮기면 인상이 크게 바뀜. 안티앨리어싱과 가독성의 균형 | 눈·입 픽셀을 표정 단위로 관리 |
| 같은 자료 | 한 작품 8~16색 제한이 흔함. 자동 픽셀화 필터는 지저분한 결과 | 공유 팔레트 + 손(코드) 정리. 자동 변환 쓰지 않음 |
| 기획 저장소 도트 가이드 LD 기준 | 80~160px 이상 초상: 16~48색, 재질별 3~5단계 명암 | 96×96은 LD에 해당 → 16~24색 `[제안]` |

### 2-3. 무드 참고(그림 참고용, 복제 금지)

| 자료 | 참고 포인트 |
| --- | --- |
| [pixie.haus 후드 해커 바이저 흉상](https://pixie.haus/pixelart/286204/hooded-figure-cyberpunk-visor-bust) | 후드 + 빛나는 바이저 실루엣 |
| [OpenGameArt Cyberpunk Portraits(CC0)](https://opengameart.org/content/cyberpunk-portraits) | 대화용 흉상 구도(얼굴·어깨까지) |
| [itch.io 개발 노트 — 저밀도 캐릭터 + 고해상 대화 UI](https://asimovgames.itch.io/glitchhackers/devlog/699788/sketches-of-main-character) | 필드 스프라이트는 작게, 대화 초상은 크게 나누는 방식(우리 구성: 32px 필드 + 96px 초상과 같음) |

- 위 자료는 **분위기·구도 참고용**이다. 특정 게임의 캐릭터·그림을 따라 그리지 않는다.
- CC0 자료도 직접 반입하지 않는다. 그림은 이 프로젝트 레시피로 새로 그린다(AGENTS.md 규칙 12).

## 3. 디자인 방향 후보 `[제안]`

| 안 | 콘셉트 | 장점 | 주의 |
| --- | --- | --- | --- |
| **A. 후드 + 바이저(익명형)** | 얼굴 대부분을 가리는 후드와 빛나는 바이저 | 작은 크기에서도 바로 읽힘. 의뢰 대상 스프라이트(후드 인물)와 결이 같음 | 흔한 해커 이미지. 표정 표현이 어려움 |
| **B. 맨얼굴 + 헤드셋(생활형)** | 고물 장비를 덧댄 헤드셋·케이블, 피곤한 눈 | 표정 변화(긴장·안도)를 보여 주기 쉬움 → 대화 연출에 유리 | 개성을 소품으로 확실히 줘야 함 |
| **C. 어긋난 얼굴(주제형)** | 얼굴 한쪽이 설계도 층처럼 청사진 선으로 어긋나 보이는 의안·마스크 | SHIFT 주제·설계도 층(D-016, D-017)을 인물에 직접 담음. 기억에 남음 | 도트로 표현 난이도가 가장 높음 |

## 4. 정할 것

1. 디자인 방향(A/B/C 또는 조합 — 예: B + C의 한쪽 눈 의안)
2. 기본 정보: 나이대·성별 표현·분위기(예: 냉소적, 피곤한, 장난스러운) — 정하지 않으면 중립적인 인물로 시안 제작 `[제안]`
3. MVP 표정 수 `[제안: 3종 — 기본, 긴장(추적도 높음), 안도(의뢰 성공)]`
4. 팔레트: 공유 팔레트에 초상 전용 색(피부 명암·머리·옷) 추가 범위

## 개정 이력

| 버전 | 날짜 | 변경 내용 | 담당 AI |
| --- | --- | --- | --- |
| v0.1.0 | 2026-11-17 | 최초 작성: 범위(D-047~D-049), 레퍼런스 조사, 디자인 방향 후보 3안 | Claude Code |
