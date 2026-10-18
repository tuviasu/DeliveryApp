"""Turns the raw captures from screenshots.exe into the README media.

    python compose.py <raw-dir> <assets-dir>

<raw-dir>/stills + stills.json  ->  <assets-dir>/<name>.png   (framed screenshots)
<raw-dir>/tour   + tour.json    ->  <assets-dir>/demo.gif, demo.mp4

Requires Pillow; the MP4 also needs ffmpeg (on PATH or via imageio-ffmpeg).
"""

import json
import math
import shutil
import subprocess
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageFont

BG_STOPS = [(15, 23, 42), (30, 27, 75), (67, 56, 202)]  # slate-900 -> indigo-950 -> indigo-700
ACCENT = (129, 140, 248)
TITLEBAR = (248, 250, 252)
TITLE_TEXT = (51, 65, 85)

FONT_CANDIDATES = {
    "regular": ["C:/Windows/Fonts/segoeui.ttf", "/usr/share/fonts/opentype/inter/Inter-Regular.otf",
                "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"],
    "semibold": ["C:/Windows/Fonts/seguisb.ttf", "/usr/share/fonts/opentype/inter/Inter-SemiBold.otf",
                 "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"],
    "bold": ["C:/Windows/Fonts/segoeuib.ttf", "/usr/share/fonts/opentype/inter/Inter-Bold.otf",
             "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"],
}


def font(weight, size):
    for path in FONT_CANDIDATES[weight]:
        if Path(path).exists():
            return ImageFont.truetype(path, size)
    return ImageFont.load_default(size)


def gradient(size):
    """Diagonal three-stop gradient with a soft glow in the top-right corner."""
    w, h = size
    small = Image.new("RGB", (256, 256))
    px = small.load()
    for y in range(256):
        for x in range(256):
            t = (x / 255 * 0.55 + y / 255 * 0.45)
            if t < 0.5:
                a, b, k = BG_STOPS[0], BG_STOPS[1], t / 0.5
            else:
                a, b, k = BG_STOPS[1], BG_STOPS[2], (t - 0.5) / 0.5
            px[x, y] = tuple(round(a[i] + (b[i] - a[i]) * k) for i in range(3))
    img = small.resize((w, h), Image.BICUBIC)

    glow = Image.new("L", (w, h), 0)
    r = int(max(w, h) * 0.35)
    ImageDraw.Draw(glow).ellipse((w - r * 1.3, -r * 0.7, w + r * 0.7, r * 1.3), fill=90)
    glow = glow.filter(ImageFilter.GaussianBlur(r * 0.45))
    img.paste(Image.new("RGB", (w, h), ACCENT), (0, 0), glow)
    return img


def window_frame(shot, title, scale):
    """Wraps a client-area capture in a Windows 11 style title bar with rounded corners."""
    bar = round(34 * scale)
    radius = round(10 * scale)
    w, h = shot.size
    win = Image.new("RGB", (w, h + bar), TITLEBAR)
    win.paste(shot, (0, bar))

    d = ImageDraw.Draw(win)
    d.line((0, bar - 1, w, bar - 1), fill=(226, 232, 240), width=max(1, round(scale)))

    # App glyph + title
    g = round(14 * scale)
    gx, gy = round(14 * scale), (bar - g) // 2
    d.rounded_rectangle((gx, gy, gx + g, gy + g), radius=round(3 * scale), fill=(79, 70, 229))
    d.rectangle((gx + g * 0.28, gy + g * 0.36, gx + g * 0.72, gy + g * 0.72), fill="white")
    d.text((gx + g + round(10 * scale), bar / 2), title, font=font("regular", round(12.5 * scale)),
           fill=TITLE_TEXT, anchor="lm")

    # Minimise / maximise / close glyphs
    s = round(5 * scale)
    lw = max(1, round(scale))
    cy = bar // 2
    for i, kind in enumerate(("close", "max", "min")):
        cx = w - round((23 + i * 46) * scale)
        if kind == "min":
            d.line((cx - s, cy, cx + s, cy), fill=TITLE_TEXT, width=lw)
        elif kind == "max":
            d.rectangle((cx - s, cy - s, cx + s, cy + s), outline=TITLE_TEXT, width=lw)
        else:
            d.line((cx - s, cy - s, cx + s, cy + s), fill=TITLE_TEXT, width=lw)
            d.line((cx - s, cy + s, cx + s, cy - s), fill=TITLE_TEXT, width=lw)

    mask = Image.new("L", win.size, 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, w - 1, h + bar - 1), radius=radius, fill=255)
    return win, mask


def place_with_shadow(canvas, win, mask, pos, scale):
    """Pastes a framed window onto the canvas with a soft drop shadow and hairline border."""
    x, y = pos
    blur = round(28 * scale)
    shadow = Image.new("L", canvas.size, 0)
    shadow.paste(mask.point(lambda v: v * 0.55), (x, y + round(16 * scale)))
    shadow = shadow.filter(ImageFilter.GaussianBlur(blur))
    canvas.paste(Image.new("RGB", canvas.size, (2, 6, 23)), (0, 0), shadow)

    edge = Image.new("L", win.size, 0)
    ImageDraw.Draw(edge).rounded_rectangle((0, 0, win.width - 1, win.height - 1),
                                           radius=round(10 * scale), outline=255, width=max(1, round(scale)))
    canvas.paste(win, (x, y), mask)
    canvas.paste(Image.new("RGB", win.size, (148, 163, 184)), (x, y), edge.point(lambda v: v * 0.5))


# ── Stills ──────────────────────────────────────────────────────────────────

def compose_still(raw_path, title, out_path, max_width=1600):
    shot = Image.open(raw_path).convert("RGB")
    scale = 1.5  # raw captures are rendered at 144 DPI
    win, mask = window_frame(shot, title, scale)
    pad = round(56 * scale)
    canvas = gradient((win.width + pad * 2, win.height + pad * 2))
    place_with_shadow(canvas, win, mask, (pad, pad - round(8 * scale)), scale)
    if canvas.width > max_width:
        canvas = canvas.resize((max_width, round(canvas.height * max_width / canvas.width)), Image.LANCZOS)
    canvas.save(out_path, optimize=True)


# ── Video ───────────────────────────────────────────────────────────────────

W, H = 1600, 900
FPS = 30
FADE_MS = 350


def fit(img, box_w, box_h):
    k = min(box_w / img.width, box_h / img.height, 1.0)
    return img.resize((round(img.width * k), round(img.height * k)), Image.LANCZOS) if k < 1 else img


def title_card(background, heading, sub, extra=None):
    c = background.copy()
    d = ImageDraw.Draw(c)
    g = 96
    gx, gy = (W - g) // 2, H // 2 - 190
    d.rounded_rectangle((gx, gy, gx + g, gy + g), radius=24, fill=(79, 70, 229))
    d.polygon([(gx + 26, gy + 38), (gx + 48, gy + 26), (gx + 70, gy + 38), (gx + 48, gy + 50)], fill="white")
    d.polygon([(gx + 26, gy + 44), (gx + 45, gy + 55), (gx + 45, gy + 74), (gx + 26, gy + 63)], fill="white")
    d.polygon([(gx + 70, gy + 44), (gx + 51, gy + 55), (gx + 51, gy + 74), (gx + 70, gy + 63)], fill="white")
    d.text((W / 2, H / 2 + 10), heading, font=font("bold", 76), fill="white", anchor="mm")
    d.text((W / 2, H / 2 + 80), sub, font=font("regular", 30), fill=(199, 210, 254), anchor="mm")
    if extra:
        d.text((W / 2, H / 2 + 140), extra, font=font("semibold", 24), fill=ACCENT, anchor="mm")
    return c


def tour_frame(background, shot, title, caption, progress):
    c = background.copy()
    scale = 1.0
    framed_shot = fit(shot, W - 200, H - 230 - 34)
    win, mask = window_frame(framed_shot, title, scale)
    x = (W - win.width) // 2
    y = 40 + (H - 230 - win.height) // 2
    place_with_shadow(c, win, mask, (x, y), scale)

    d = ImageDraw.Draw(c)
    d.text((W / 2, H - 92), caption, font=font("semibold", 32), fill="white", anchor="mm")
    d.text((60, H - 36), "DeliveryApp", font=font("bold", 22), fill=(199, 210, 254), anchor="lm")
    bar_x0, bar_x1, bar_y = 240, W - 60, H - 36
    d.rounded_rectangle((bar_x0, bar_y - 3, bar_x1, bar_y + 3), radius=3, fill=(51, 65, 85))
    d.rounded_rectangle((bar_x0, bar_y - 3, bar_x0 + (bar_x1 - bar_x0) * progress, bar_y + 3), radius=3, fill=ACCENT)
    return c


def build_timeline(raw_dir):
    """Returns [(frame_image, hold_ms)], with crossfades between scenes."""
    tour = json.loads((raw_dir / "tour.json").read_text(encoding="utf-8"))
    background = gradient((W, H))
    total = sum(s["holdMs"] for s in tour)

    composed = []
    elapsed = 0
    for s in tour:
        elapsed += s["holdMs"]
        shot = Image.open(raw_dir / "tour" / s["file"]).convert("RGB")
        composed.append((tour_frame(background, shot, s["title"], s["caption"], elapsed / total), s["holdMs"],
                         s["title"]))

    intro = title_card(background, "DeliveryApp", "Desktop Logistics & Delivery Control Center")
    outro = title_card(background, "DeliveryApp", "WPF · .NET 8 · 3-tier architecture",
                       "Download the latest release on GitHub")

    scenes = [(intro, 2200, "__intro")] + composed + [(outro, 2800, "__outro")]
    timeline = []
    for i, (img, hold, key) in enumerate(scenes):
        if i > 0 and key != scenes[i - 1][2]:
            prev = scenes[i - 1][0]
            steps = 6
            for k in range(1, steps + 1):
                t = 0.5 - 0.5 * math.cos(math.pi * k / (steps + 1))
                timeline.append((Image.blend(prev, img, t), FADE_MS // steps))
        timeline.append((img, hold))
    return timeline


def write_gif(mp4_path, out_path, width=900, fps=12):
    """Makes the README GIF from the MP4: one shared palette and frame-diff rectangles
    keep it ~40% smaller than a per-frame quantized GIF at the same quality."""
    filters = (f"fps={fps},scale={width}:-1:flags=lanczos,split[a][b];"
               "[a]palettegen=max_colors=192:stats_mode=diff[p];"
               "[b][p]paletteuse=dither=bayer:bayer_scale=4:diff_mode=rectangle")
    subprocess.run([ffmpeg_exe(), "-y", "-loglevel", "error", "-i", str(mp4_path), "-vf", filters, str(out_path)],
                   check=True)


def ffmpeg_exe():
    exe = shutil.which("ffmpeg")
    if exe:
        return exe
    import imageio_ffmpeg  # pip install imageio-ffmpeg
    return imageio_ffmpeg.get_ffmpeg_exe()


def write_mp4(timeline, out_path):
    cmd = [ffmpeg_exe(), "-y", "-loglevel", "error", "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{W}x{H}",
           "-r", str(FPS), "-i", "-", "-c:v", "libx264", "-preset", "slow", "-crf", "22", "-pix_fmt", "yuv420p",
           "-movflags", "+faststart", str(out_path)]
    proc = subprocess.Popen(cmd, stdin=subprocess.PIPE)
    carry = 0.0
    for img, ms in timeline:
        exact = ms * FPS / 1000 + carry
        n = max(1, round(exact))
        carry = exact - n
        data = img.tobytes()
        for _ in range(n):
            proc.stdin.write(data)
    proc.stdin.close()
    if proc.wait() != 0:
        raise SystemExit("ffmpeg failed")


def main():
    raw_dir, out_dir = Path(sys.argv[1]), Path(sys.argv[2])
    out_dir.mkdir(parents=True, exist_ok=True)

    stills = raw_dir / "stills.json"
    if stills.exists():
        for s in json.loads(stills.read_text(encoding="utf-8")):
            compose_still(raw_dir / "stills" / s["file"], s["title"], out_dir / s["file"])
            print("still", s["file"])

    if (raw_dir / "tour.json").exists():
        timeline = build_timeline(raw_dir)
        write_mp4(timeline, out_dir / "demo.mp4")
        print("video demo.mp4")
        write_gif(out_dir / "demo.mp4", out_dir / "demo.gif")
        print("video demo.gif")


if __name__ == "__main__":
    main()
