"""Galmuri 한글 픽셀 글꼴을 비트맵 글리프 시트로 굽는다(W-034, D-124).

글꼴(SIL OFL 1.1)은 저장소에 넣지 않는다. 공식 배포(GitHub quiple/galmuri 릴리스)를 새 빈 폴더에 받아 두고 그 폴더를 --font-dir 로 넘긴다.
TTF 를 원래 픽셀 크기(ppem)로, 안티에일리어싱 없이 렌더링한다(Galmuri9 = ppem 10, Galmuri7 = ppem 8, Galmuri11 = ppem 12).

문자 집합: ASCII(0x20~0x7E) + 화살표·기호 + KS X 1001 완성형 한글 2,350자 + 문구 표 한국어 칸에 실제로 쓰인 글자.
결과(Art/Export/font/):
  galmuri9_atlas.png    흰색(불투명)/투명 글리프 시트
  galmuri9_glyphs.json  글리프 정보 [코드, x, y, w, h, 가로 오프셋, 세로 오프셋(셀 위 기준), 진행폭]
  LICENSE-Galmuri-OFL.txt  글꼴 라이선스 원문(OFL 1.1, 파생 비트맵과 함께 둔다)

사용: python -I tools/art/bake_galmuri.py --font-dir <받은 폴더> [--name Galmuri9] [--ppem 10]
"""
import argparse
import json
import shutil
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

REPO = Path(__file__).resolve().parents[2]
STRINGS_CSV = REPO / "Unity" / "Assets" / "_Project" / "Resources" / "Strings.csv"
OUT_DIR = REPO / "Art" / "Export" / "font"

DEFAULT_PPEM = {"Galmuri7": 8, "Galmuri9": 10, "Galmuri11": 12}
EXTRA_SYMBOLS = "←↑→↓·…‘’“”※★☆♥♡○●□■△▲▽▼◇◆～°±×÷"


def ks_x_1001_hangul() -> list[str]:
    """KS X 1001 완성형 한글 2,350자(EUC-KR 0xB0A1~0xC8FE)."""
    out = []
    for b1 in range(0xB0, 0xC9):
        for b2 in range(0xA1, 0xFF):
            try:
                out.append(bytes([b1, b2]).decode("euc_kr"))
            except UnicodeDecodeError:
                pass
    return out


def chars_in_table() -> set[str]:
    """문구 표(Strings.csv)의 한국어 칸에 쓰인 글자. 표가 없거나 비어 있으면 빈 집합."""
    if not STRINGS_CSV.exists():
        return set()
    import csv
    used: set[str] = set()
    with STRINGS_CSV.open(encoding="utf-8-sig", newline="") as f:
        for row in csv.DictReader(f):
            used.update((row.get("ko") or "").replace("\n", ""))
    return used


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--font-dir", required=True, help="Galmuri TTF 와 LICENSE.txt 가 있는 폴더(저장소 밖)")
    ap.add_argument("--name", default="Galmuri9")
    ap.add_argument("--ppem", type=int, default=0)
    args = ap.parse_args()

    ppem = args.ppem or DEFAULT_PPEM.get(args.name, 10)
    font_dir = Path(args.font_dir)
    ttf = font_dir / f"{args.name}.ttf"
    font = ImageFont.truetype(str(ttf), ppem)
    ascent, descent = font.getmetrics()
    line_h = ascent + descent

    hangul = ks_x_1001_hangul()
    assert len(hangul) == 2350, len(hangul)
    chars = [chr(c) for c in range(0x20, 0x7F)] + list(EXTRA_SYMBOLS) + hangul
    extra = sorted(chars_in_table() - set(chars) - {"\n", "\r", "\t"})
    chars += extra

    # 글꼴에 없는 글자는 건너뛴다(.notdef 모양이 그려지지 않게 cmap 으로 확인)
    from fontTools.ttLib import TTFont  # Pillow 와 별도 도구라 없으면 cmap 확인을 생략한다
    cmap = set(TTFont(str(ttf)).getBestCmap().keys())
    missing = [c for c in chars if ord(c) not in cmap]
    chars = [c for c in chars if ord(c) in cmap]

    cell = line_h + 4
    placed = []  # (code, bitmap(Image 'L' 크롭), xoff, yoff, adv)
    for c in chars:
        im = Image.new("L", (cell * 2, cell), 0)
        dr = ImageDraw.Draw(im)
        dr.fontmode = "1"  # 안티에일리어싱 끔
        ox, base = 4, 2 + ascent  # 셀 위 = base - ascent = 2
        dr.text((ox, base), c, font=font, fill=255, anchor="ls")
        adv = int(round(font.getlength(c)))
        bb = im.getbbox()
        if bb is None:
            placed.append((ord(c), None, 0, 0, adv))
            continue
        placed.append((ord(c), im.crop(bb), bb[0] - ox, bb[1] - 2, adv))

    # 선반 방식 배치(한 글리프 사이 1px 여백)
    atlas_w = 1024
    x = y = row_h = 0
    pos = []
    for code, bm, xo, yo, adv in placed:
        if bm is None:
            pos.append((code, 0, 0, 0, 0, xo, yo, adv))
            continue
        w, h = bm.size
        if x + w + 1 > atlas_w:
            x, y, row_h = 0, y + row_h + 1, 0
        pos.append((code, x, y, w, h, xo, yo, adv))
        x += w + 1
        row_h = max(row_h, h)
    atlas_h = 1
    while atlas_h < y + row_h + 1:
        atlas_h *= 2
    atlas = Image.new("RGBA", (atlas_w, atlas_h), (255, 255, 255, 0))
    for (code, gx, gy, w, h, xo, yo, adv), (_, bm, _, _, _) in zip(pos, placed):
        if bm is None:
            continue
        for py in range(h):
            for px in range(w):
                if bm.getpixel((px, py)):
                    atlas.putpixel((gx + px, gy + py), (255, 255, 255, 255))

    OUT_DIR.mkdir(parents=True, exist_ok=True)
    stem = args.name.lower()
    atlas.save(OUT_DIR / f"{stem}_atlas.png")
    meta = {
        "font": args.name, "ppem": ppem, "ascent": ascent, "descent": descent, "lineHeight": line_h,
        "atlas": f"{stem}_atlas.png", "atlasW": atlas_w, "atlasH": atlas_h,
        "fields": ["code", "x", "y", "w", "h", "xoff", "yoff", "advance"],
        "glyphs": [list(g) for g in pos],
    }
    (OUT_DIR / f"{stem}_glyphs.json").write_text(json.dumps(meta, ensure_ascii=False, separators=(",", ":")), encoding="utf-8")
    lic = font_dir / "LICENSE.txt"
    if lic.exists():
        shutil.copyfile(lic, OUT_DIR / "LICENSE-Galmuri-OFL.txt")

    print(f"{args.name} ppem {ppem}: ascent {ascent}, descent {descent}, 줄 높이 {line_h}")
    print(f"글자 {len(pos)}개(한글 2,350 + 표에서 추가 {len(extra)}자), 시트 {atlas_w}x{atlas_h}")
    if missing:
        print("글꼴에 없어 건너뜀:", "".join(missing))
    return 0


if __name__ == "__main__":
    sys.exit(main())
