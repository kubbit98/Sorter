#!/usr/bin/env python3
"""Resets the manual test environment of Sorter.

Fills TestFolder/src with sample files of every supported type (plus edge cases)
and recreates TestFolder/dst with empty destination folders A-E.
Matches TestFolder/test_config.json ("Load default config for testing" in the app).

Requirements: Pillow, pillow-heif, ffmpeg. LibreOffice is optional (text-based PDF).
Pillow builds without AVIF support (e.g. Fedora's python3-pillow) fall back to ffmpeg for AVIF.
Usage: python generate_test_files.py [--clean]
--clean is required when src or dst already contain files, they will be deleted.
"""
import shutil
import subprocess
import sys
import tempfile
import zipfile
from pathlib import Path

import pillow_heif
from PIL import Image, ImageDraw, ImageFont, features

pillow_heif.register_heif_opener()
PILLOW_AVIF = features.check("avif")

EXIF_ORIENTATION = 0x0112
VIDEO_INPUT = ["-f", "lavfi", "-i", "testsrc2=size=640x360:rate=25:duration=5",
               "-f", "lavfi", "-i", "sine=frequency=440:beep_factor=4:duration=5"]
AUDIO_INPUT = ["-f", "lavfi", "-i", "sine=frequency=440:beep_factor=4:duration=5"]
H264_AAC = ["-c:v", "libx264", "-pix_fmt", "yuv420p", "-c:a", "aac"]

VIDEOS = {
    "video_h264_aac.mp4": H264_AAC + ["-movflags", "+faststart"],
    "video_h264_aac.mov": H264_AAC,
    "video_h264_aac.m4v": H264_AAC,
    "video_h264_aac.qt": H264_AAC + ["-f", "mov"],
    "video_h264_aac.ts": H264_AAC,
    "video_vp9_opus.webm": ["-c:v", "libvpx-vp9", "-crf", "40", "-b:v", "0", "-deadline", "realtime",
                            "-cpu-used", "8", "-c:a", "libopus"],
    "video_theora_vorbis.ogv": ["-c:v", "libtheora", "-q:v", "6", "-c:a", "libvorbis"],
    "video_mpeg4_mp3.avi": ["-c:v", "mpeg4", "-q:v", "5", "-c:a", "libmp3lame"],
    "video_h263_aac.3gp": ["-s", "352x288", "-c:v", "h263", "-c:a", "aac", "-ar", "16000", "-ac", "1"],
    "video_wmv2_wma.wmv": ["-c:v", "wmv2", "-c:a", "wmav2"],
    "video_wmv2_wma.asf": ["-c:v", "wmv2", "-c:a", "wmav2"],
    "video_flv_mp3.flv": ["-c:v", "flv", "-c:a", "libmp3lame", "-ar", "44100"],
    "video_mpeg2_mp2.mpg": ["-c:v", "mpeg2video", "-q:v", "5", "-c:a", "mp2"],
}
SVG_SAMPLE = """<svg xmlns="http://www.w3.org/2000/svg" width="800" height="600" viewBox="0 0 800 600">
  <rect width="800" height="600" fill="#2a5aa0"/>
  <polygon points="400,30 350,110 450,110" fill="white"/>
  <text x="400" y="140" font-family="sans-serif" font-size="28" fill="white" text-anchor="middle">TOP</text>
  <text x="400" y="320" font-family="sans-serif" font-size="56" fill="white" text-anchor="middle">SVG vector</text>
</svg>
"""
AUDIOS = {
    "audio.mp3": ["-c:a", "libmp3lame"],
    "audio.wav": ["-c:a", "pcm_s16le"],
    "audio_vorbis.ogg": ["-c:a", "libvorbis"],
    "audio_vorbis.oga": ["-c:a", "libvorbis", "-f", "ogg"],
    "audio_aac.m4a": ["-c:a", "aac"],
}


def font(size):
    return ImageFont.load_default(size=size)


def make_image(width, height, title):
    horizontal = Image.linear_gradient("L").rotate(90).resize((width, height))
    vertical = Image.linear_gradient("L").resize((width, height))
    img = Image.merge("RGB", (horizontal, vertical, Image.new("L", (width, height), 140)))
    draw = ImageDraw.Draw(img)
    short = min(width, height)
    step = max(width, height) // 10
    for x in range(0, width, step):
        draw.line([(x, 0), (x, height)], fill="white", width=max(1, step // 80))
    for y in range(0, height, step):
        draw.line([(0, y), (width, y)], fill="white", width=max(1, step // 80))
    # arrow marking the top edge, makes wrong rotation obvious
    cx, s = width // 2, short // 8
    draw.polygon([(cx, s // 4), (cx - s // 2, s // 4 + s // 2), (cx + s // 2, s // 4 + s // 2)], fill="white")
    draw.text((cx, s // 4 + s // 2 + 5), "TOP", font=font(s // 3), fill="white", anchor="ma")
    stroke = max(1, short // 200)
    draw.text((width // 2, height // 2), title, font=font(short // 12), fill="black",
              anchor="mm", stroke_width=stroke, stroke_fill="white")
    draw.text((width // 2, height // 2 + short // 9), f"{width}x{height}", font=font(short // 16),
              fill="black", anchor="mm", stroke_width=stroke, stroke_fill="white")
    return img


def make_frames(width, height, title, count=12):
    frames = []
    r = min(width, height) // 12
    for i in range(count):
        frame = make_image(width, height, f"{title} {i + 1}/{count}")
        x = r + (width - 2 * r) * i // (count - 1)
        ImageDraw.Draw(frame).ellipse([x - r, height - 3 * r, x + r, height - r], fill=(220, 40, 40))
        frames.append(frame)
    return frames


def save_animation(frames, path, **kwargs):
    frames[0].save(path, save_all=True, append_images=frames[1:], duration=150, loop=0, **kwargs)


def save_rotated_exif6(displayed, path, **kwargs):
    """Stores pixels rotated 90° CCW with EXIF orientation 6, like phone cameras do.
    For HEIC pillow-heif additionally writes the rotation as an irot box (as iPhones do)."""
    exif = Image.Exif()
    exif[EXIF_ORIENTATION] = 6
    displayed.rotate(90, expand=True).save(path, exif=exif.tobytes(), **kwargs)


def ffmpeg(inputs, args, path):
    subprocess.run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", *inputs, *args, str(path)], check=True)


def save_avif(img, path):
    if PILLOW_AVIF:
        img.save(path, quality=60)
        return
    with tempfile.TemporaryDirectory() as tmp:
        png = Path(tmp) / "source.png"
        img.save(png)
        ffmpeg(["-i", str(png)], ["-c:v", "libaom-av1", "-still-picture", "1", "-cpu-used", "6",
                                  "-crf", "32", "-pix_fmt", "yuv420p"], path)


def required_ffmpeg_encoders():
    encoders = {"libx264", "aac"}
    for args in [*VIDEOS.values(), *AUDIOS.values()]:
        encoders.update(args[i + 1] for i, a in enumerate(args) if a in ("-c:v", "-c:a"))
    if not PILLOW_AVIF:
        encoders.add("libaom-av1")
    return encoders


def missing_requirements():
    """Checked before anything is deleted, so a missing tool doesn't leave a half-generated folder."""
    problems = []
    if not features.check("webp"):
        problems.append("Pillow without WebP support")
    if not pillow_heif.libheif_info().get("HEIF"):
        problems.append("pillow-heif without HEIF encoder")
    if not shutil.which("ffmpeg"):
        return problems + ["ffmpeg not found in PATH"]
    output = subprocess.run(["ffmpeg", "-hide_banner", "-encoders"], capture_output=True, text=True).stdout
    available = {line.split()[1] for line in output.splitlines() if len(line.split()) > 1}
    missing = sorted(required_ffmpeg_encoders() - available)
    if missing:
        problems.append(f"ffmpeg without encoders: {', '.join(missing)}")
    return problems


def generate_photos(d):
    make_image(4000, 3000, "JPG large").save(d / "jpg_large.jpg", quality=85)
    make_image(640, 480, "JPG small").save(d / "jpg_small.jpg", quality=85)
    save_rotated_exif6(make_image(3000, 4000, "JPG portrait EXIF 6"), d / "jpg_portrait_exif_orientation6.jpg", quality=85)
    make_image(2000, 1500, "JPEG extension").save(d / "photo.jpeg", quality=85)
    make_image(1600, 1200, "Uppercase JPG").save(d / "IMG_0001.JPG", quality=85)
    make_image(1600, 1200, "JFIF extension").save(d / "photo.jfif", format="JPEG", quality=85)
    make_image(1600, 1200, "JPE extension").save(d / "photo.jpe", format="JPEG", quality=85)
    (d / "vector.svg").write_text(SVG_SAMPLE, encoding="utf-8")
    make_image(2400, 1800, "PNG large").save(d / "png_large.png")
    make_image(500, 500, "PNG small").save(d / "png_small.png")
    make_image(3000, 2000, "WEBP large").save(d / "webp_large.webp", quality=80)
    save_animation(make_frames(600, 400, "WEBP anim"), d / "webp_animated.webp", quality=70)
    save_animation(make_frames(400, 300, "GIF small"), d / "gif_animated_small.gif")
    save_animation(make_frames(1200, 900, "GIF large"), d / "gif_animated_large.gif")
    save_animation(make_frames(500, 375, "APNG"), d / "apng_animated.apng", format="PNG")
    save_avif(make_image(2400, 1600, "AVIF"), d / "avif_large.avif")
    make_image(1200, 900, "BMP").save(d / "bmp_large.bmp")
    # 32-bit BMP with alpha, a variant browsers don't display reliably (the app converts BMP to PNG)
    alpha = make_image(600, 450, "BMP 32-bit alpha").convert("RGBA")
    alpha.putalpha(Image.linear_gradient("L").rotate(90).resize(alpha.size))
    alpha.save(d / "bmp_alpha_32bit.bmp")
    make_image(800, 600, "Duplicate name (photos)").save(d / "duplicate_name.jpg", quality=85)


def generate_heic(d):
    make_image(4032, 3024, "HEIC large").save(d / "heic_large.heic", quality=80)
    make_image(640, 480, "HEIC small").save(d / "heic_small.heic", quality=80)
    save_rotated_exif6(make_image(3024, 4032, "HEIC portrait irot+EXIF 6"), d / "heic_portrait_irot_exif6.heic", quality=80)
    make_image(1000, 750, "Uppercase HEIC").save(d / "IMG_0002.HEIC", quality=80)
    make_image(1600, 1200, "HEIF extension").save(d / "photo.heif", format="HEIF", quality=80)
    # .hif is used by Canon, Fujifilm and Sony cameras
    make_image(1600, 1200, "HIF (camera)").save(d / "DSC00001.HIF", format="HEIF", quality=80)


def generate_videos(d):
    for name, args in VIDEOS.items():
        ffmpeg(VIDEO_INPUT, args, d / name)


def generate_audio(d):
    for name, args in AUDIOS.items():
        ffmpeg(AUDIO_INPUT, args, d / name)


def generate_text(d):
    (d / "notes.txt").write_text("Short note.\nZażółć gęślą jaźń – Polish diacritics test.\n", encoding="utf-8")
    (d / "empty.txt").write_text("")
    lines = [f"{i:05d} Lorem ipsum dolor sit amet, consectetur adipiscing elit." for i in range(1, 401)]
    (d / "long_text_over_preview_limit.txt").write_text("\n".join(lines) + "\n")
    (d / "data.csv").write_text("id;name;value\n1;alpha;3.14\n2;beta;2.72\n3;gamma;1.41\n")
    (d / "server.log").write_text("".join(
        f"2026-11-16 12:{m:02d}:00 [{lvl}] Request {m} handled\n"
        for m, lvl in zip(range(30), ["INFO", "WARN", "ERROR"] * 10)))
    (d / "README.md").write_text("# Heading\n\nSome **bold** text and a list:\n\n- one\n- two\n\n```js\nconsole.log('hi');\n```\n")
    (d / "page.html").write_text("<!doctype html>\n<html><body><h1>Test page</h1><p>Paragraph <b>bold</b>.</p>"
                                 "<script>alert('should not run');</script></body></html>\n")
    (d / "script.js").write_text("function add(a, b) {\n    return a + b;\n}\nconsole.log(add(2, 3));\n")
    (d / "styles.css").write_text("body {\n    margin: 0;\n    font-family: sans-serif;\n}\n")


def generate_documents(d):
    pages = [make_image(1240, 1754, f"Scanned page {i}") for i in range(1, 4)]
    pages[0].save(d / "document_scanned_3_pages.pdf", save_all=True, append_images=pages[1:], resolution=150)
    soffice = shutil.which("soffice") or shutil.which("libreoffice")
    if not soffice:
        print("LibreOffice not found, skipping text PDF")
        return
    with tempfile.TemporaryDirectory() as tmp:
        tmp = Path(tmp)
        body = "".join(f"<h1 style='page-break-before: always'>Page {i}</h1><p>{'Lorem ipsum dolor sit amet. ' * 40}</p>"
                       for i in range(1, 4))
        (tmp / "document_text.html").write_text(f"<html><body>{body}</body></html>")
        subprocess.run([soffice, f"-env:UserInstallation=file://{tmp}/profile", "--headless",
                        "--convert-to", "pdf:writer_web_pdf_Export", "--outdir", str(tmp), str(tmp / "document_text.html")],
                       check=True, capture_output=True)
        shutil.move(tmp / "document_text.pdf", d / "document_text.pdf")


def generate_edge_cases(d):
    make_image(800, 600, "Diacritics in name").save(d / "zdjęcie z wakacji ąęśćżźół.jpg", quality=85)
    make_image(800, 600, "Hash and percent").save(d / "hash#and%percent.png")
    make_image(800, 600, "Duplicate name (edge)").save(d / "duplicate_name.jpg", quality=85)
    make_image(1200, 900, "TIFF").save(d / "image.tiff")
    (d / "not_an_image.jpg").write_bytes(b"This is not a JPEG file.\n" * 50)
    (d / "not_an_image.heic").write_bytes(b"This is not a HEIC file.\n" * 50)
    buffer = d / "truncated.jpg"
    make_image(2000, 1500, "Truncated").save(buffer, quality=85)
    buffer.write_bytes(buffer.read_bytes()[: buffer.stat().st_size // 3])
    (d / "README_no_extension").write_text("File without extension.\n")
    (d / ".txt").write_text("File named only '.txt'.\n")
    (d / ".DS_Store").write_bytes(b"\x00\x00\x00\x01Bud1")
    with zipfile.ZipFile(d / "archive.zip", "w") as archive:
        archive.writestr("inside.txt", "Unsupported container.\n")
    ffmpeg(VIDEO_INPUT, ["-t", "2", "-c:v", "libx264", "-pix_fmt", "yuv420p", "-c:a", "aac"], d / "video_unsupported.mkv")


def main():
    root = Path(__file__).resolve().parent
    src, dst = root / "src", root / "dst"
    if problems := missing_requirements():
        sys.exit("Missing requirements, nothing was changed:\n- " + "\n- ".join(problems))
    existing = [p for d in (src, dst) if d.exists() for p in d.rglob("*") if p.is_file()]
    if existing and "--clean" not in sys.argv:
        sys.exit(f"{len(existing)} files found in {src} or {dst}, use --clean to delete them")
    for d in (src, dst):
        if d.exists():
            shutil.rmtree(d)
    for folder in "ABCDE":
        (dst / folder).mkdir(parents=True)
    steps = {
        "01_photos": generate_photos, "02_heic": generate_heic, "03_video": generate_videos,
        "04_audio": generate_audio, "05_text": generate_text, "06_documents": generate_documents,
        "07_edge_cases": generate_edge_cases,
    }
    for folder, generate in steps.items():
        print(f"Generating {folder}...")
        path = src / folder
        path.mkdir(parents=True)
        generate(path)
    print(f"Done: {sum(1 for p in src.rglob('*') if p.is_file())} files in {src}, empty folders A-E in {dst}")


if __name__ == "__main__":
    main()
