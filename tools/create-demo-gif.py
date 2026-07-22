from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "docs" / "images" / "homepc-demo.gif"
WIDTH, HEIGHT, HEADER = 1000, 650, 64


def font(size: int) -> ImageFont.FreeTypeFont | ImageFont.ImageFont:
    for candidate in ("C:/Windows/Fonts/seguisb.ttf", "C:/Windows/Fonts/segoeui.ttf"):
        if Path(candidate).exists():
            return ImageFont.truetype(candidate, size)
    return ImageFont.load_default()


def frame(path: Path, title: str, subtitle: str) -> Image.Image:
    source = Image.open(path).convert("RGB")
    canvas = Image.new("RGB", (WIDTH, HEIGHT), "#070b16")
    draw = ImageDraw.Draw(canvas)
    draw.text((28, 10), title, font=font(25), fill="#f5f7ff")
    draw.text((28, 39), subtitle, font=font(14), fill="#9ba9c5")
    available = (WIDTH, HEIGHT - HEADER)
    source.thumbnail(available, Image.Resampling.LANCZOS)
    x = (WIDTH - source.width) // 2
    y = HEADER + (HEIGHT - HEADER - source.height) // 2
    canvas.paste(source, (x, y))
    return canvas


frames = [
    frame(ROOT / "docs/images/dashboard-v2.png", "Live Windows control center", "Cloud linked, agent online, protected actions disabled"),
    frame(ROOT / "docs/images/routines-v2.png", "Build a custom routine", "Only validated allow-listed actions can be composed"),
    frame(ROOT / "docs/images/google-home-devices.png", "Control it from Google Home", "Native switches discovered through Cloud-to-cloud SYNC"),
]
frames[0].save(OUTPUT, save_all=True, append_images=frames[1:], duration=[1900, 2300, 1900], loop=0, optimize=True)
print(OUTPUT)
