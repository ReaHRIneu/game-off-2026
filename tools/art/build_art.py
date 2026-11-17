"""Art/recipes/*.py 를 모두 실행해 Art/Export/ 에 PNG 를 만든다.

사용: python -I tools/art/build_art.py [레시피이름 ...]
"""
import importlib.util
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPO / "tools" / "art"))
sys.path.insert(0, str(REPO / "Art" / "recipes"))  # 레시피끼리 부품 함수를 재사용

def main(names: list[str]) -> int:
    recipes = sorted((REPO / "Art" / "recipes").glob("*.py"))
    if names:
        recipes = [r for r in recipes if r.stem in names]
    if not recipes:
        print("실행할 레시피가 없습니다.")
        return 1
    for r in recipes:
        spec = importlib.util.spec_from_file_location(r.stem, r)
        mod = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(mod)
        made = mod.build()
        print(f"[{r.stem}] {len(made)}개 생성")
        for p in made:
            print("  ", p.relative_to(REPO).as_posix())
    return 0

if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
