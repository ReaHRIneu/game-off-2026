"""Audio/recipes/*.py 를 실행해 Audio/Export/ 에 BGM WAV·확인용 파일을 만든다.

사용: python -P tools/audio/build_audio.py [레시피이름 ...]
(numpy 가 사용자 영역에 설치된 환경에서는 -I 대신 -P 로 실행한다.)
"""
import importlib.util
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPO / "tools" / "audio"))
import synth as sy  # noqa: E402

PAGE = """<!doctype html>
<html lang="ko"><head><meta charset="utf-8"><title>BGM 청취 확인</title>
<style>body{font-family:sans-serif;background:#181c24;color:#dde;max-width:760px;margin:2em auto;padding:0 1em}
audio{width:100%%;margin:.3em 0 1.2em}img{width:100%%}</style></head><body>
<h1>BGM 청취 확인(코드 합성)</h1>
<p>x3 = 루프 본을 3번 이어 붙인 파일(이음매는 길이의 1/3, 2/3 지점). 아래 루프 재생은 브라우저에 따라 이음매에 아주 짧은 틈이 생길 수 있습니다(게임 안에서는 다르게 재생).</p>
%s
</body></html>
"""
ITEM = """<h2>%(n)s</h2>
<p>x3(이음매 확인)</p><audio controls src="%(n)s_x3.wav"></audio>
<p>루프 본(반복 재생)</p><audio controls loop src="../%(n)s.wav"></audio>
<img src="%(n)s_wave.png" alt="%(n)s 파형">
"""


def main(names):
    recipes = sorted((REPO / "Audio" / "recipes").glob("bgm_*.py"))
    if names:
        recipes = [r for r in recipes if r.stem in names]
    if not recipes:
        print("실행할 레시피가 없습니다.")
        return 1
    done = []
    for r in recipes:
        spec = importlib.util.spec_from_file_location(r.stem, r)
        mod = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(mod)
        made = mod.build()
        rep = mod.REPORT
        done.append(r.stem)
        print(f"[{r.stem}] {len(made)}개 생성")
        for p in made:
            print("  ", p.relative_to(REPO).as_posix())
        print(f"   길이 {rep['seconds']:.3f}초 / {rep['samples']}샘플, {rep['bpm']} BPM, {rep['bars']}마디")
        print(f"   최고점 {rep['peak_db']:.2f} dBFS, RMS {rep['rms_db']:.2f} dBFS, 클리핑 {rep['clip_samples']}")
        print(f"   이음매 끝-처음 차이 {rep['seam_diff']:.5f} (평균 샘플간 변화 {rep['typical_step']:.5f})")
        print(f"   이음매 앞뒤 10ms RMS 비 {rep['rms_ratio_db']:+.2f} dB "
              f"(마디 경계 평균 {rep['rms_ratio_bar_mean']:+.2f} / 최대 {rep['rms_ratio_bar_max']:+.2f} dB)")
        print(f"   스펙트럼 변화 이음매 {rep['spec_seam'][0]:.2f}/{rep['spec_seam'][1]:.2f} dB, "
              f"마디 경계 평균 {rep['spec_bar_mean']:.2f} / 최대 {rep['spec_bar_max']:.2f} dB")
    # 청취 페이지(전체 곡 대상)
    names_all = sorted(p.stem for p in sy.PREVIEW.glob("bgm_*_x3.wav"))
    names_all = [n[: -len("_x3")] for n in names_all]
    html = PAGE % "".join(ITEM % {"n": n} for n in names_all)
    (sy.PREVIEW / "index.html").write_text(html, encoding="utf-8")
    print("   Audio/Export/preview/index.html 갱신")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
