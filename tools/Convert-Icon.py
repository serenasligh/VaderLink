"""
Convert-Icon.py  —  VaderLink icon generator
=============================================

Converts a source image (JPEG, PNG, etc.) into three .ico files used by
VaderLink's system tray:

    icon_connected.ico     — full-colour original
    icon_disconnected.ico  — desaturated (greyscale)
    icon_error.ico         — red-tinted

Each .ico contains four embedded sizes: 16×16, 32×32, 48×48, and 256×256.

Prerequisites
-------------
  pip install Pillow

Usage
-----
  1. Place this script anywhere convenient.
  2. Run it from the command line, passing the path to your source image:

       python Convert-Icon.py "C:\\path\\to\\your-image.jpg"

  3. Three .ico files will be written to:

       <VaderLink repo>\\src\\VaderLink\\Resources\\

     If the script can't find the Resources folder automatically, pass it
     explicitly as the second argument:

       python Convert-Icon.py icon.jpg "C:\\path\\to\\src\\VaderLink\\Resources"

Rebuilding the exe
------------------
  After running this script, rebuild VaderLink so the new icon is embedded
  in the executable:

       dotnet build src\\VaderLink\\VaderLink.csproj -c Release

"""

import sys
import os
from pathlib import Path


def find_resources_dir(script_path: Path) -> Path | None:
    """Walk upward from this script to find src/VaderLink/Resources."""
    candidate = script_path.parent
    for _ in range(6):  # search up to 6 levels up
        resources = candidate / "src" / "VaderLink" / "Resources"
        if resources.is_dir():
            return resources
        candidate = candidate.parent
    return None


def make_icons(source_path: Path, out_dir: Path) -> None:
    try:
        from PIL import Image, ImageEnhance, ImageOps
    except ImportError:
        print("ERROR: Pillow is not installed. Run:  pip install Pillow")
        sys.exit(1)

    sizes = [(16, 16), (32, 32), (48, 48), (256, 256)]

    print(f"Loading source image: {source_path}")
    src = Image.open(source_path).convert("RGBA")

    # ── Connected: full-colour ────────────────────────────────────────────────
    connected = src.copy()
    _save_ico(connected, out_dir / "icon_connected.ico", sizes)
    print(f"  Written: icon_connected.ico")

    # ── Disconnected: desaturated to ~40 % brightness ─────────────────────────
    grey = ImageOps.grayscale(src.convert("RGB")).convert("RGBA")
    grey.putalpha(src.getchannel("A"))          # restore original alpha
    dim  = ImageEnhance.Brightness(grey).enhance(0.55)
    _save_ico(dim, out_dir / "icon_disconnected.ico", sizes)
    print(f"  Written: icon_disconnected.ico")

    # ── Error: red-tinted ─────────────────────────────────────────────────────
    red = _tint(src, (255, 60, 60))
    _save_ico(red, out_dir / "icon_error.ico", sizes)
    print(f"  Written: icon_error.ico")


def _tint(image: "Image.Image", colour: tuple[int, int, int]) -> "Image.Image":
    """Multiply each RGB channel by a colour tint, preserving alpha."""
    from PIL import Image
    r, g, b, a = image.split()
    tr, tg, tb = colour
    r = r.point(lambda x: int(x * tr / 255))
    g = g.point(lambda x: int(x * tg / 255))
    b = b.point(lambda x: int(x * tb / 255))
    return Image.merge("RGBA", (r, g, b, a))


def _save_ico(image: "Image.Image", path: Path, sizes: list[tuple[int, int]]) -> None:
    """Save an RGBA image as a multi-size .ico file."""
    frames = []
    for w, h in sizes:
        frame = image.resize((w, h), resample=3)  # LANCZOS = 1 in old Pillow, Image.LANCZOS in new
        frames.append(frame)
    frames[0].save(
        path,
        format="ICO",
        sizes=sizes,
        append_images=frames[1:],
    )


def main() -> None:
    if len(sys.argv) < 2:
        print(__doc__)
        print("Usage: python Convert-Icon.py <source-image> [<output-dir>]")
        sys.exit(1)

    source = Path(sys.argv[1])
    if not source.exists():
        print(f"ERROR: Source image not found: {source}")
        sys.exit(1)

    if len(sys.argv) >= 3:
        out_dir = Path(sys.argv[2])
    else:
        out_dir = find_resources_dir(Path(__file__).resolve())
        if out_dir is None:
            print(
                "ERROR: Could not find src/VaderLink/Resources/ automatically.\n"
                "Pass it as the second argument:\n"
                "  python Convert-Icon.py icon.jpg \"path\\to\\src\\VaderLink\\Resources\""
            )
            sys.exit(1)

    out_dir.mkdir(parents=True, exist_ok=True)
    print(f"Output directory: {out_dir}")

    make_icons(source, out_dir)
    print("\nDone! Rebuild VaderLink to embed the new icon in the exe:")
    print("  dotnet build src\\VaderLink\\VaderLink.csproj -c Release")


if __name__ == "__main__":
    main()
