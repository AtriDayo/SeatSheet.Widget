"""Convert the generated icon to transparent PNG sizes and a multi-frame Windows ICO."""
from pathlib import Path
from PIL import Image

root = Path(__file__).resolve().parent.parent
assets = root / "Assets"
sizes = (16, 20, 24, 32, 40, 48, 64, 96, 128, 256)
with Image.open(assets / "app-icon-source.png") as original:
    image = original.convert("RGBA")
    if image.width != image.height:
        raise ValueError("The generated application icon must be square.")
    if image.getextrema()[3][0] != 0:
        raise ValueError("The source must preserve its transparent background.")
    image.resize((512, 512), Image.Resampling.LANCZOS).save(assets / "app-icon.png")
    image.save(assets / "app.ico", sizes=[(s, s) for s in sizes], bitmap_format="png")
    with Image.open(assets / "app.ico") as icon:
        assert icon.ico.sizes() == {(s, s) for s in sizes}
        for size in icon.ico.sizes():
            frame = icon.ico.getimage(size).convert("RGBA")
            assert frame.size == size and frame.getextrema()[3][0] == 0
    print(f"Verified ICO sizes: {', '.join(map(str, sizes))}; PNG: 512; source: {image.width}")
