"""Assemble RenderPreview PNG frames into the README GIF (requires Pillow)."""
from pathlib import Path
from PIL import Image

root = Path(__file__).resolve().parents[1]
paths = sorted((root / "output/readme-frames").glob("*.png"))
assert len(paths) == 120, "Render all 120 frames first"
frames = [Image.open(p).convert("RGB") for p in paths]
samples = Image.new("RGB", (480 * 6, 480))
for column, index in enumerate([0, 20, 40, 50, 70, 90]):
    samples.paste(frames[index], (column * 480, 0))
palette = samples.quantize(colors=256, method=Image.Quantize.MEDIANCUT)
frames = [f.quantize(palette=palette, dither=Image.Dither.NONE) for f in frames]
out = root / "assets/odd-preview.gif"
out.parent.mkdir(exist_ok=True)
frames[0].save(out, save_all=True, append_images=frames[1:],
               duration=[70, 60, 70] * 40, loop=0, optimize=True, disposal=1)
with Image.open(out) as check:
    assert check.size == (480, 480) and check.info["loop"] == 0
    total = 0
    for i in range(check.n_frames):
        check.seek(i)
        check.load()
        total += check.info["duration"]
    assert total == 8000, total
    print(f"Verified {check.n_frames} frames, {total / 1000}s, {out.stat().st_size:,} bytes")
