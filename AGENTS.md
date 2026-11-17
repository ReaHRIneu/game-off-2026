# game-off-2026 에이전트 안내

이 파일은 AI 에이전트 종류와 관계없이 이 저장소에 적용하는 규칙입니다. `CLAUDE.md` 등 도구별 파일은 이 파일만 가리킵니다.

## ⚠️ 공개 저장소 보안 규칙 (최우선)

이 저장소는 **Public**입니다. GitHub Game Off 2026 참가 조건이기 때문입니다. 커밋은 즉시 외부에 공개될 수 있고, push 뒤에는 기록을 지우기 어렵습니다.

- 다음 내용은 **절대 기록하지 않습니다**: 실명·연락처·주소 등 개인 정보, **API 키·토큰·비밀번호**, **개인용 코드**(사용자의 개인 도구·스크립트·로컬 설정, 개인 위키 저장소의 코드).
- 에이전트 로컬 설정(`.claude/`, `.codex/`, `.agents/`)은 `.gitignore`로 제외합니다.
- **커밋 전마다 `git diff --cached`를 읽고** 위 항목이 섞이지 않았는지 확인합니다.
- 로컬 절대 경로(사용자 홈·드라이브 경로)를 문서에 적지 않습니다. 저장소 기준 상대 경로를 씁니다.

## 프로젝트 정보

- 프로젝트명: game-off-2026 (게임 제목 미정)
- 행사: [GitHub Game Off 2026](https://itch.io/jam/game-off-2026), 주제 **SHIFT**
- 제출 마감: 2026-12-01 13:37 PST (한국 시간 2026-12-02 06:37)
- GitHub 저장소: https://github.com/ReaHRIneu/game-off-2026 (Public)
- Unity 프로젝트: `Unity/` 폴더, Editor 버전은 `Unity/ProjectSettings/ProjectVersion.txt` 기준(6000.6.0f1), 템플릿 Universal 2D
- Unity CLI 버전: 1.0.0-beta.13 (생성 시 확인)
- Unity Pipeline: 생성 시 `--with-pipeline`로 추가. Editor 실행 후 `unity pipeline list`로 확인 필요(미확인)
- 배포: Unity Web 빌드 → itch.io

## 폴더

| 경로 | 내용 |
| --- | --- |
| `Docs/Planning/` | 기획 문서(정본). `decisions.md`는 결정 기록부 |
| `Docs/Event/` | 행사 규칙 요약·제출 점검·크레딧과 AI 사용 기록 |
| `Docs/QA/` | `test-backlog.md` 등 QA 기록 |
| `Unity/` | Unity 프로젝트 |
| `history/` | 작업 기록 |

## 작업 규칙

1. 응답·주석·문서는 한국어로 씁니다. `README.md`에는 해외 평가자를 위한 영문 요약을 둡니다.
2. 기획 결정은 `Docs/Planning/decisions.md`에 `D-ID`로 기록합니다. 에이전트는 스스로 `확정`으로 기록하지 않습니다.
3. 원본에 없는 내용을 문서에 쓸 때는 `[제안]` 또는 `[생성]`으로 표시합니다.
4. 문서 버전은 `MAJOR.MINOR.PATCH`이며 `v1.0.0` 승격은 사용자가 먼저 언급할 때만 합니다.
5. `Library/`, `Temp/`, `Obj/`, `Logs/`, `UserSettings/`, 빌드 산출물은 커밋하지 않습니다.
6. 로컬 커밋은 확인 없이 해도 됩니다. **push는 사용자가 명시적으로 요청할 때만** 합니다.
7. Unity 명령은 `unity --help`와 하위 명령 `--help`를 먼저 읽습니다.
8. 개발 중 테스트: 작업마다 컴파일 오류 0과 바꾼 기능 1회 작동 확인만 합니다. 실질 테스트는 `Docs/QA/test-backlog.md`에 `T-ID`로 쌓고 사용자가 통합 QA를 요청할 때 실행합니다. 작동 확인만 한 작업을 「테스트 통과」로 보고하지 않습니다.
9. AI로 만든 코드·이미지·사운드는 `Docs/Event/credits-and-ai-log.md`에 기록합니다. 행사 제출 때 AI 사용을 공개해야 합니다.
10. 제3자 에셋은 라이선스와 출처를 같은 기록에 남깁니다. 재배포가 안 되는 에셋은 저장소에 넣지 않고 README에 필요한 에셋을 적습니다.
11. 게임의 대부분은 잼 기간(2026-11-01 이후)에 새로 만듭니다. 이전 프로젝트 코드를 가져오면 출처와 범위를 README에 밝힙니다.

## 프로젝트 명령

- 테스트: 미설정
- 빌드: 미설정(Web 빌드 경로는 MVP-1에서 확정)
- UI 화면 확인/스크린샷: 미설정
