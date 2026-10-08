"""Build Assets/Fonts/Emoji/Twemoji.ttf from the pinned Twemoji release.

Run with the venv that has tools/emoji/requirements.txt installed:
    py -3 tools/emoji/fetch_emoji_font.py [--cache DIR] [--jobs N]

Downloads the jdecked/twemoji release source archive (sha256 pinned below), runs nanoemoji
(glyf_colr_0) over its SVGs, and writes the font, the licence text, the single-code-point corpus
the editor smoke reads, and emoji-font.lock. The font is the only artifact Unity consumes; nothing
here is hand-made art. A second run with the same pins must reproduce the font's sha256.
"""
import argparse, hashlib, os, re, shutil, subprocess, sys, tarfile, tempfile, urllib.request
from importlib import metadata
from pathlib import Path

TWEMOJI_VERSION = "v17.0.3"
TWEMOJI_URL = "https://github.com/jdecked/twemoji/archive/refs/tags/v17.0.3.tar.gz"
TWEMOJI_SHA256 = "a0855654b633045ae2337537e77f1bb4361162f7fcd910e613eaab1d6d9c5fca"
# 2026-06-01T22:21:37Z, the release's publication time: fixes head.created/modified for reproducibility.
SOURCE_DATE_EPOCH = "1780352497"
FAMILY = "Twemoji"

ROOT = Path(__file__).resolve().parents[2]
FONT_DIR = ROOT / "Assets" / "Fonts" / "Emoji"
SINGLES = ROOT / "tools" / "emoji" / "emoji-singles.txt"


def sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for block in iter(lambda: f.read(1 << 20), b""):
            h.update(block)
    return h.hexdigest()


def download(cache):
    archive = cache / f"twemoji-{TWEMOJI_VERSION}.tar.gz"
    if not archive.exists():
        cache.mkdir(parents=True, exist_ok=True)
        urllib.request.urlretrieve(TWEMOJI_URL, archive)
    got = sha256(archive)
    if got != TWEMOJI_SHA256:
        archive.unlink()
        sys.exit(f"archive sha256 {got} != pinned {TWEMOJI_SHA256}")
    return archive


def licence_text(root):
    readme = (root / "README.md").read_text(encoding="utf-8")
    attribution = re.search(r"## Attribution Requirements\n\n(.*?)\n\n## ", readme, re.S).group(1)
    graphics = re.search(r"Graphics licensed under CC-BY 4.0: \S+", readme).group(0)
    copyright_lines = [l for l in (root / "LICENSE").read_text(encoding="utf-8").splitlines() if l.startswith("Copyright")]
    return (
        f"Twemoji {TWEMOJI_VERSION}: {TWEMOJI_URL}\n"
        + "\n".join(copyright_lines) + "\n" + graphics + "\n\n"
        "Change notice (CC BY 4.0 section 3(a)(1)(B)): the SVG graphics are converted to a COLRv0 colour\n"
        "font (Twemoji.ttf) by tools/emoji/fetch_emoji_font.py using nanoemoji. No artwork is edited.\n\n"
        "Attribution Requirements (upstream README)\n\n" + attribution + "\n\n"
        "LICENSE-GRAPHICS (upstream, verbatim)\n"
        "=======================================================================\n\n"
        + (root / "LICENSE-GRAPHICS").read_text(encoding="utf-8")
    )


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--cache", type=Path, default=Path(tempfile.gettempdir()) / "aetheria-emoji-cache")
    ap.add_argument("--jobs", type=int, default=max(1, min(4, (os.cpu_count() or 2) // 2)))
    args = ap.parse_args()

    archive = download(args.cache)
    work = args.cache / f"build-{TWEMOJI_VERSION}"
    if work.exists():
        shutil.rmtree(work)
    work.mkdir(parents=True)
    with tarfile.open(archive) as tar:
        tar.extractall(work / "src", filter="data")
    src = next((work / "src").iterdir())
    svgs = sorted((src / "assets" / "svg").glob("*.svg"))

    env = dict(os.environ, SOURCE_DATE_EPOCH=SOURCE_DATE_EPOCH)
    # nanoemoji shells out to ninja and picosvg by name; they live beside this interpreter.
    env["PATH"] = str(Path(sys.executable).parent) + os.pathsep + env["PATH"]
    build = work / "build"
    # 4000 paths overflow the Windows command line, so the inputs travel in an absl flagfile.
    flagfile = work / "inputs.flags"
    flagfile.write_text("".join(f"{p.as_posix()}\n" for p in svgs), encoding="utf-8")
    run = [sys.executable, "-c", "from nanoemoji.nanoemoji import main; main()", "--color_format", "glyf_colr_0",
           "--family", FAMILY, "--output_file", f"{FAMILY}.ttf", "--build_dir", str(build), "--noexec_ninja",
           f"--flagfile={flagfile}"]
    subprocess.run(run, check=True, env=env, cwd=work)
    subprocess.run(["ninja", "-j", str(args.jobs), "-C", str(build)], check=True, env=env)

    FONT_DIR.mkdir(parents=True, exist_ok=True)
    out = FONT_DIR / f"{FAMILY}.ttf"
    shutil.copyfile(build / f"{FAMILY}.ttf", out)
    (FONT_DIR / "LICENSE.txt").write_text(licence_text(src), encoding="utf-8", newline="\n")

    # The smoke's corpus: every single-code-point glyph the pinned release draws.
    singles = sorted(p.stem for p in svgs if re.fullmatch(r"[0-9a-f]{4,6}", p.stem))
    SINGLES.write_text("".join(f"{s}\n" for s in singles), encoding="utf-8", newline="\n")

    tools = {n: metadata.version(n) for n in ("nanoemoji", "fonttools", "picosvg", "ninja")}
    lock = [
        f"source_url={TWEMOJI_URL}", f"source_version={TWEMOJI_VERSION}", f"source_sha256={TWEMOJI_SHA256}",
        f"svg_count={len(svgs)}", f"single_codepoint_count={len(singles)}",
        *(f"tool_{n}={v}" for n, v in tools.items()),
        f"source_date_epoch={SOURCE_DATE_EPOCH}", "color_format=glyf_colr_0",
        f"font_file={out.name}", f"font_sha256={sha256(out)}", f"singles_sha256={sha256(SINGLES)}",
    ]
    previous = (FONT_DIR / "emoji-font.lock")
    if previous.exists():
        old = dict(l.split("=", 1) for l in previous.read_text().splitlines() if "=" in l)
        if old.get("font_sha256") != sha256(out):
            print(f"NOT REPRODUCED: font_sha256 {sha256(out)} != lock {old.get('font_sha256')}")
        else:
            print("reproduced: font_sha256 equals the lock")
    previous.write_text("\n".join(lock) + "\n", encoding="utf-8", newline="\n")
    print("\n".join(lock))


if __name__ == "__main__":
    main()
