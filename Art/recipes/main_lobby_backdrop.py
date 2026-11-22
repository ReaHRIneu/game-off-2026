"""메인 로비 창밖 정지 배경 (W-024) — 비 오는 밤 도시에서 비 줄기를 뺀 정지 그림.

- 근거: W-024 지시서 2-2 대체안. 환경 이펙트(D-103)를 메인 화면에 재사용하기 어려워(보드·방 배치에 묶임) 정지 배경으로 깐다.
- 창 유리 영역(main_lobby.WIN_GLASS)에만 도시를 그리고 나머지는 어두운 바탕(K). 비는 코드(MenuRenderer)가 움직여 그린다.
- main_lobby.py·main_lobby_drafts.py는 고치지 않고 부품만 가져다 쓴다. 출력: scenes/main_lobby_backdrop.png (640x360).
"""
import main_lobby as ml
import main_lobby_drafts as dr
from artkit import PIX, canvas, save


def build():
    with ml.ext_palette():
        im = canvas(ml.W, ml.H, PIX["K"])
        dr.city_view(im, ml.WIN_GLASS, seed=33, horizon=0.9)
        assert not ml.bad_colors(im)
        return [save(im, "scenes/main_lobby_backdrop.png",
                     {"status": "본그림", "layer": "창 뒤 정지 배경(W-024 대체안, 비 줄기 없음)", "glass": list(ml.WIN_GLASS)})]


if __name__ == "__main__":
    for p in build():
        print(p)
