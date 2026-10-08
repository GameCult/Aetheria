"""Build Assets/Fonts/Emoji/Twemoji.ttf from the pinned Twemoji release.

The font reproduces ONLY inside the pinned Linux container (PINNED_IMAGE, Python PINNED_PYTHON); on any
other platform the script refuses to run and writes nothing. It does not run under `py -3` on Windows.
Run it on Yggdrasil (or any docker host) from the repository root:
    docker run --rm -e EMOJI_FONT_IMAGE=python:3.13-slim -v "$PWD":/src -w /src python:3.13-slim sh -c \\
      "pip install -r tools/emoji/requirements.txt && python tools/emoji/fetch_emoji_font.py [--jobs N]"

Downloads the jdecked/twemoji release source archive and Unicode's emoji-test.txt (both sha256 pinned
below), runs nanoemoji (glyf_colr_0) over the SVGs, and writes the font, the licence text, the smoke's
corpus (emoji-singles.txt, from emoji-test.txt), the list of emoji Twemoji lacks (emoji-unsupported.txt)
and emoji-font.lock. The font is the only artifact Unity consumes; nothing here is hand-made art.
A run whose font-determining pins match the lock must reproduce the font's sha256, or the script exits 1
and leaves the committed font and lock untouched; the lock never follows the machine.
"""
import argparse, hashlib, os, platform, re, shutil, subprocess, sys, tarfile, tempfile, urllib.request
from importlib import metadata
from pathlib import Path

TWEMOJI_VERSION = "v17.0.3"
TWEMOJI_URL = "https://github.com/jdecked/twemoji/archive/refs/tags/v17.0.3.tar.gz"
TWEMOJI_SHA256 = "a0855654b633045ae2337537e77f1bb4361162f7fcd910e613eaab1d6d9c5fca"
# 2026-06-01T22:21:37Z, the release's publication time: fixes head.created/modified for reproducibility.
SOURCE_DATE_EPOCH = "1780352497"
FAMILY = "Twemoji"
# Unicode's emoji-test.txt for the Emoji version Twemoji v17.0.3 targets: the smoke's corpus.
EMOJI_TEST_VERSION = "17.0"
EMOJI_TEST_URL = "https://unicode.org/Public/17.0.0/emoji/emoji-test.txt"
EMOJI_TEST_SHA256 = "1d8a944f88d7952f7ef7c5167fef3c67995bcae24543949710231b03a201acda"
# The one environment the font reproduces in. The docker command passes the image tag in; a container
# cannot read its own image name.
PINNED_PLATFORM = "linux"
PINNED_PYTHON = "3.13"
PINNED_IMAGE = "python:3.13-slim"

ROOT = Path(__file__).resolve().parents[2]
FONT_DIR = ROOT / "Assets" / "Fonts" / "Emoji"
SINGLES = ROOT / "tools" / "emoji" / "emoji-singles.txt"
UNSUPPORTED = ROOT / "tools" / "emoji" / "emoji-unsupported.txt"


def sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for block in iter(lambda: f.read(1 << 20), b""):
            h.update(block)
    return h.hexdigest()


def download(cache, url, name, pinned):
    path = cache / name
    if not path.exists():
        cache.mkdir(parents=True, exist_ok=True)
        urllib.request.urlretrieve(url, path)
    got = sha256(path)
    if got != pinned:
        path.unlink()
        sys.exit(f"{name} sha256 {got} != pinned {pinned}")
    return path


def single_code_points(emoji_test):
    """Every emoji of the Emoji version that is one code point, bare or with its FE0F presentation
    selector (so (c) and (r) are in), of any qualification status including components."""
    found = set()
    for line in emoji_test.read_text(encoding="utf-8").splitlines():
        if not line or line.startswith("#"):
            continue
        points = [int(p, 16) for p in line.split(";", 1)[0].split() if int(p, 16) != 0xFE0F]
        if len(points) == 1:
            found.add(points[0])
    return sorted(found)


def refuse_unless_pinned_environment():
    here = (sys.platform, f"{sys.version_info.major}.{sys.version_info.minor}", os.environ.get("EMOJI_FONT_IMAGE"))
    if here != (PINNED_PLATFORM, PINNED_PYTHON, PINNED_IMAGE):
        sys.exit(f"refusing to run: the font reproduces only on {PINNED_PLATFORM}, Python {PINNED_PYTHON}, image "
                 f"{PINNED_IMAGE} (EMOJI_FONT_IMAGE); this is {here}. Nothing was written.")


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
    refuse_unless_pinned_environment()

    archive = download(args.cache, TWEMOJI_URL, f"twemoji-{TWEMOJI_VERSION}.tar.gz", TWEMOJI_SHA256)
    emoji_test = download(args.cache, EMOJI_TEST_URL, f"emoji-test-{EMOJI_TEST_VERSION}.txt", EMOJI_TEST_SHA256)
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

    built = build / f"{FAMILY}.ttf"
    font_sha = sha256(built)
    tools = {n: metadata.version(n) for n in ("nanoemoji", "fonttools", "picosvg", "ninja")}
    font_pins = {
        "source_sha256": TWEMOJI_SHA256, **{f"tool_{n}": v for n, v in tools.items()},
        "source_date_epoch": SOURCE_DATE_EPOCH, "color_format": "glyf_colr_0",
        "python_version": platform.python_version(), "platform": PINNED_PLATFORM, "container_image": PINNED_IMAGE,
    }
    lock_path = FONT_DIR / "emoji-font.lock"
    old = dict(l.split("=", 1) for l in lock_path.read_text().splitlines() if "=" in l) if lock_path.exists() else {}
    if old and all(old.get(k) == v for k, v in font_pins.items()):
        if old.get("font_sha256") != font_sha:
            sys.exit(f"NOT REPRODUCED: font_sha256 {font_sha} != lock {old.get('font_sha256')} with identical pins. "
                     "Nothing was written.")
        print("reproduced: font_sha256 equals the lock")

    # The smoke's corpus is Unicode's list of single-code-point emoji, not the supplier's file names;
    # what Twemoji does not draw is reported, not hidden.
    drawn = {int(p.stem, 16) for p in svgs if re.fullmatch(r"[0-9a-f]+", p.stem)}
    singles = single_code_points(emoji_test)
    unsupported = [cp for cp in singles if cp not in drawn]

    FONT_DIR.mkdir(parents=True, exist_ok=True)
    out = FONT_DIR / f"{FAMILY}.ttf"
    shutil.copyfile(built, out)
    (FONT_DIR / "LICENSE.txt").write_text(licence_text(src), encoding="utf-8", newline="\n")
    SINGLES.write_text("".join(f"{cp:X}\n" for cp in singles), encoding="utf-8", newline="\n")
    UNSUPPORTED.write_text("".join(f"{cp:X}\n" for cp in unsupported), encoding="utf-8", newline="\n")
    print(f"Twemoji lacks {len(unsupported)} of {len(singles)} single-code-point emoji: "
          + " ".join(f"U+{cp:04X}" for cp in unsupported))

    lock = [
        f"source_url={TWEMOJI_URL}", f"source_version={TWEMOJI_VERSION}", f"source_sha256={TWEMOJI_SHA256}",
        f"emoji_test_version={EMOJI_TEST_VERSION}", f"emoji_test_url={EMOJI_TEST_URL}", f"emoji_test_sha256={EMOJI_TEST_SHA256}",
        f"svg_count={len(svgs)}", f"single_codepoint_count={len(singles)}", f"unsupported_count={len(unsupported)}",
        *(f"tool_{n}={v}" for n, v in tools.items()),
        f"source_date_epoch={SOURCE_DATE_EPOCH}", "color_format=glyf_colr_0",
        f"python_version={font_pins['python_version']}", f"platform={PINNED_PLATFORM}", f"container_image={PINNED_IMAGE}",
        f"font_file={out.name}", f"font_sha256={font_sha}",
        f"singles_sha256={sha256(SINGLES)}", f"unsupported_sha256={sha256(UNSUPPORTED)}",
    ]
    lock_path.write_text("\n".join(lock) + "\n", encoding="utf-8", newline="\n")
    print("\n".join(lock))


if __name__ == "__main__":
    main()
