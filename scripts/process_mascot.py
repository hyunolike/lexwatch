"""마스코트 원본 PNG(흰 배경)를 투명 배경 WebP로 바꾼다.

실행: uv run --with pillow python scripts/process_mascot.py <원본 폴더>
가장자리에서 이어진 밝은 영역만 배경으로 보고 지운다. 캐릭터 안의 흰 셔츠와 문서는
가장자리와 이어지지 않으므로 남는다. 경계는 밝기에 따라 반투명하게 풀어 계단 현상을 줄인다.
"""

from __future__ import annotations

import sys
from collections import deque
from pathlib import Path

from PIL import Image, ImageFilter

OUT = Path(__file__).resolve().parent.parent / "web" / "public" / "mascot"
# 파일 이름 -> 출력 한 변 길이(px). 화면 표시 크기의 2배.
SIZES = {
    "lex-master": 480,
    "lex-avatar": 128,
    "lex-empty-queue": 360,
    "lex-empty-inbox": 360,
    "lex-waking": 360,
    "lex-error": 360,
}
BG = 238  # 이 값 이상이면 배경 후보(세 채널 모두)
SOFT = 200  # BG와 SOFT 사이는 반투명


def background_mask(im: Image.Image) -> Image.Image:
    """가장자리에서 이어진 밝은 픽셀을 255로 표시한 마스크."""
    w, h = im.size
    px = im.load()
    mask = Image.new("L", (w, h), 0)
    mp = mask.load()
    q: deque[tuple[int, int]] = deque()
    for x in range(w):
        q.extend([(x, 0), (x, h - 1)])
    for y in range(h):
        q.extend([(0, y), (w - 1, y)])
    while q:
        x, y = q.popleft()
        if mp[x, y]:
            continue
        r, g, b = px[x, y][:3]
        if min(r, g, b) < SOFT:
            continue
        mp[x, y] = 255
        for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
            if 0 <= nx < w and 0 <= ny < h and not mp[nx, ny]:
                q.append((nx, ny))
    return mask


def process(src: Path, size: int) -> Path:
    im = Image.open(src).convert("RGB")
    mask = background_mask(im)
    # 배경 영역 안에서도 밝기에 따라 투명도를 나눠 그림자와 경계를 부드럽게 남긴다.
    gray = im.convert("L")
    alpha = Image.new("L", im.size, 255)
    ap, mp, gp = alpha.load(), mask.load(), gray.load()
    for y in range(im.height):
        for x in range(im.width):
            if mp[x, y]:
                v = gp[x, y]
                ap[x, y] = 0 if v >= BG else int(255 * (BG - v) / (BG - SOFT))
    alpha = alpha.filter(ImageFilter.GaussianBlur(0.6))
    rgba = im.copy()
    rgba.putalpha(alpha)
    bbox = alpha.point(lambda a: 255 if a > 8 else 0).getbbox()
    if bbox:
        rgba = rgba.crop(bbox)
    # 정사각 캔버스 가운데에 놓고 줄인다.
    side = max(rgba.size)
    canvas = Image.new("RGBA", (side, side), (255, 255, 255, 0))
    canvas.paste(rgba, ((side - rgba.width) // 2, (side - rgba.height) // 2))
    canvas = canvas.resize((size, size), Image.LANCZOS)
    OUT.mkdir(parents=True, exist_ok=True)
    out = OUT / f"{src.stem}.webp"
    canvas.save(out, "WEBP", quality=88, method=6)
    return out


def main() -> None:
    if len(sys.argv) != 2:
        sys.exit("사용법: process_mascot.py <원본 PNG 폴더>")
    src_dir = Path(sys.argv[1])
    for name, size in SIZES.items():
        out = process(src_dir / f"{name}.png", size)
        print(f"{out.name}\t{out.stat().st_size // 1024}KB")


if __name__ == "__main__":
    main()
