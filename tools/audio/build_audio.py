"""Audio/recipes/*.py 를 실행해 Audio/Export/ 에 BGM·효과곡 WAV와 확인용 파일을 만든다.

사용: python -P tools/audio/build_audio.py [레시피이름 ...]
(numpy 가 사용자 영역에 설치된 환경에서는 -I 대신 -P 로 실행한다.)
레시피: bgm_*.py(반복곡, 루프 본 + x3 + 파형), sting_*.py(효과곡, 반복 없음 + 파형).
"""
import importlib.util
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPO / "tools" / "audio"))
import synth as sy  # noqa: E402

# 장면 순서와 이름(청취 페이지 표시용)
SCENES = [
    ("bgm_title", "메인(방 일러스트)·크레딧"),
    ("bgm_desk", "바탕화면·의뢰 선택·상세"),
    ("bgm_briefing", "브리핑·종료 대화"),
    ("bgm_ch0", "챕터 0 의뢰(주인공 건물, 튜토리얼)"),
    ("bgm_ch1", "챕터 1 의뢰(구시가지)"),
    ("bgm_alert", "추적도 최대·카운트다운"),
    ("sting_clear", "의뢰 성공 결과 화면(효과곡)"),
    ("sting_gameover", "게임오버(효과곡)"),
]
ORDER = [n for n, _ in SCENES]

LIMIT_80, LIMIT_40 = 10.0, 1.0

PAGE = """<!doctype html>
<html lang="ko"><head><meta charset="utf-8"><title>BGM 청취 확인</title>
<style>body{font-family:sans-serif;background:#181c24;color:#dde;max-width:760px;margin:2em auto;padding:0 1em}
audio{width:100%%;margin:.3em 0 1.2em}img{width:100%%}small{color:#9ab}</style></head><body>
<h1>BGM 청취 확인(코드 합성)</h1>
<p>장면 순서: title, desk, briefing, ch0, ch1, alert, clear, gameover.
x3 = 루프 본을 3번 이어 붙인 파일(이음매는 길이의 1/3, 2/3 지점). 아래 루프 재생은 브라우저에 따라 이음매에 아주 짧은 틈이 생길 수 있습니다(게임 안에서는 다르게 재생).</p>
%s
</body></html>
"""
ITEM_LOOP = """<h2>%(n)s <small>%(scene)s</small></h2>
<p>x3(이음매 확인)</p><audio controls src="%(n)s_x3.wav"></audio>
<p>루프 본(반복 재생)</p><audio controls loop src="../%(n)s.wav"></audio>
<img src="%(n)s_wave.png" alt="%(n)s 파형">
"""
ITEM_STING = """<h2>%(n)s <small>%(scene)s</small></h2>
<p>효과곡(1회 재생)</p><audio controls src="../%(n)s.wav"></audio>
<img src="%(n)s_wave.png" alt="%(n)s 파형">
"""


def _order_key(stem):
    return ORDER.index(stem) if stem in ORDER else len(ORDER)


def main(names):
    rdir = REPO / "Audio" / "recipes"
    recipes = sorted(list(rdir.glob("bgm_*.py")) + list(rdir.glob("sting_*.py")), key=lambda p: _order_key(p.stem))
    if names:
        recipes = [r for r in recipes if r.stem in names]
    if not recipes:
        print("실행할 레시피가 없습니다.")
        return 1
    warn = []
    for r in recipes:
        spec = importlib.util.spec_from_file_location(r.stem, r)
        mod = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(mod)
        made = mod.build()
        rep = mod.REPORT
        print(f"[{r.stem}] {len(made)}개 생성")
        for p in made:
            print("  ", p.relative_to(REPO).as_posix())
        if rep.get("kind") == "sting":
            print(f"   길이 {rep['seconds']:.3f}초 / {rep['samples']}샘플 (반복 없음)")
            print(f"   최고점 {rep['peak_db']:.2f} dBFS, RMS {rep['rms_db']:.2f} dBFS, 클리핑 {rep['clip_samples']}")
            print(f"   첫 샘플 {rep['first_abs']:.5f} / 끝 샘플 {rep['last_abs']:.5f}, "
                  f"끝 꼬리(페이드 직전 100ms) RMS {rep['tail_rms_db']:.1f} dBFS")
        else:
            print(f"   길이 {rep['seconds']:.3f}초 / {rep['samples']}샘플, {rep['bpm']} BPM, {rep['bars']}마디")
            print(f"   최고점 {rep['peak_db']:.2f} dBFS, RMS {rep['rms_db']:.2f} dBFS, 클리핑 {rep['clip_samples']}")
            print(f"   이음매 끝-처음 차이 {rep['seam_diff']:.5f} (평균 샘플간 변화 {rep['typical_step']:.5f})")
            print(f"   이음매 앞뒤 10ms RMS 비 {rep['rms_ratio_db']:+.2f} dB "
                  f"(마디 경계 평균 {rep['rms_ratio_bar_mean']:+.2f} / 최대 {rep['rms_ratio_bar_max']:+.2f} dB)")
            print(f"   스펙트럼 변화 이음매 {rep['spec_seam'][0]:.2f}/{rep['spec_seam'][1]:.2f} dB, "
                  f"마디 경계 평균 {rep['spec_bar_mean']:.2f} / 최대 {rep['spec_bar_max']:.2f} dB")
        print(f"   저역 에너지 80Hz 아래 {rep['low80_pct']:.2f}% / 40Hz 아래 {rep['low40_pct']:.3f}% "
              f"(160~2000Hz {rep['mid_pct']:.1f}%)")
        if rep["low80_pct"] > LIMIT_80 or rep["low40_pct"] > LIMIT_40:
            warn.append(r.stem)
            print(f"   !! 경고: 저역 규칙(80Hz 아래 <= {LIMIT_80}%, 40Hz 아래 <= {LIMIT_40}%) 초과")
    # 청취 페이지(Export 에 있는 전체 곡 대상, 장면 순서)
    scene = dict(SCENES)
    stems = [n for n in ORDER if (sy.EXPORT / f"{n}.wav").exists()]
    items = []
    for n in stems:
        tpl = ITEM_STING if n.startswith("sting_") else ITEM_LOOP
        items.append(tpl % {"n": n, "scene": scene[n]})
    (sy.PREVIEW / "index.html").write_text(PAGE % "".join(items), encoding="utf-8")
    print("   Audio/Export/preview/index.html 갱신")
    if warn:
        print("저역 규칙 경고 곡: " + ", ".join(warn))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
