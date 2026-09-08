#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
Charcoal map filter for GIMP 2.10 (Python-Fu).

Matches the dialog stack:
  1. Colors → Saturation          scale=0
  2. Colors → Levels (Value)      0 / 0.52 / 194 → 0 / 119
  3. Colors → Hue-Chroma          hue=-53.37, chroma=8.14, lightness=0
  4. Filters → Artistic → Cartoon mask=25.872, pct-black=0.105
  5. Filters → Noise → HSV Noise  Overlay, dulling=2, hue=96.8, sat=0, value=0.369

Install (Windows GIMP 2.10):
  Copy this file to:
    %APPDATA%\\GIMP\\2.10\\plug-ins\\gimp-charcoal-map-filter.py
  Restart GIMP.

Run:
  Filters → Map → Charcoal map filter
  Filters → Map → Charcoal map filter (folder)...
"""

from gimpfu import *
import os

# --- verified dialog values ---
SAT_SCALE = 0.0

LEVELS_CHANNEL = HISTOGRAM_VALUE
LEVELS_IN_LOW = 0.0
LEVELS_IN_HIGH = 194.0 / 255.0
LEVELS_CLAMP_IN = False
LEVELS_GAMMA = 0.52
LEVELS_OUT_LOW = 0.0
LEVELS_OUT_HIGH = 119.0 / 255.0
LEVELS_CLAMP_OUT = False

HUE_CHROMA_HUE = -53.37
HUE_CHROMA_CHROMA = 8.14
HUE_CHROMA_LIGHTNESS = 0.0

CARTOON_MASK = 25.872
CARTOON_PCT_BLACK = 0.105

NOISE_HOLDNESS = 2
NOISE_HUE = 96.8
NOISE_SAT = 0.0
NOISE_VALUE = 0.369


def _apply_saturation(image, drawable):
    """Colors → Saturation scale=0 (Native)."""
    # Exact path if GEGL graph helper is installed
    if hasattr(pdb, "python_gegl"):
        pdb.python_gegl(image, drawable, "saturation scale=0.0")
        return

    # Built-in equivalent: full desaturation
    pdb.gimp_drawable_desaturate(drawable, DESATURATE_LUMINANCE)


def _apply_hue_chroma(image, drawable):
    """Colors → Hue-Chroma hue=-53.37 chroma=8.14 lightness=0."""
    if hasattr(pdb, "python_gegl"):
        pdb.python_gegl(
            image,
            drawable,
            "hue-chroma hue=%s chroma=%s lightness=%s"
            % (HUE_CHROMA_HUE, HUE_CHROMA_CHROMA, HUE_CHROMA_LIGHTNESS),
        )
        return

    # Fallback when no GEGL helper: Colorize approximation of LCH tint.
    # Gray + chroma → Colorize. LCH hue -53.37 ≈ 306.63° on 0–360 wheel.
    colorize_hue = (HUE_CHROMA_HUE % 360.0 + 360.0) % 360.0
    pdb.gimp_colorize(drawable, colorize_hue, HUE_CHROMA_CHROMA, HUE_CHROMA_LIGHTNESS)
    pdb.gimp_message(
        "Charcoal filter: Hue-Chroma used Colorize fallback.\n"
        "For an exact match, install a GEGL Graph Python helper "
        "that registers pdb.python_gegl, then re-run."
    )


def _apply_levels(drawable):
    """Colors → Levels, Channel=Value — exact PDB match including gamma."""
    pdb.gimp_drawable_levels(
        drawable,
        LEVELS_CHANNEL,
        LEVELS_IN_LOW,
        LEVELS_IN_HIGH,
        LEVELS_CLAMP_IN,
        LEVELS_GAMMA,
        LEVELS_OUT_LOW,
        LEVELS_OUT_HIGH,
        LEVELS_CLAMP_OUT,
    )


def _apply_cartoon(image, drawable):
    pdb.plug_in_cartoon(image, drawable, CARTOON_MASK, CARTOON_PCT_BLACK)


def _apply_hsv_noise_overlay(image, drawable):
    """HSV Noise with Overlay blend (layer duplicate + merge)."""
    noise = pdb.gimp_layer_new_from_drawable(drawable, image)
    pdb.gimp_image_insert_layer(image, noise, None, -1)
    pdb.gimp_item_set_name(noise, "hsv-noise-overlay")

    pdb.plug_in_hsv_noise(
        image,
        noise,
        NOISE_HOLDNESS,
        NOISE_HUE,
        NOISE_SAT,
        NOISE_VALUE,
    )

    # GIMP 2.10 layer modes
    try:
        pdb.gimp_layer_set_mode(noise, LAYER_MODE_OVERLAY)
    except Exception:
        pdb.gimp_layer_set_mode(noise, OVERLAY_MODE)

    pdb.gimp_layer_set_opacity(noise, 100.0)
    return pdb.gimp_image_merge_down(image, noise, EXPAND_AS_NECESSARY)


def apply_charcoal_stack(image, drawable):
    pdb.gimp_image_undo_group_start(image)
    try:
        pdb.gimp_layer_add_alpha(drawable)

        _apply_saturation(image, drawable)
        _apply_levels(drawable)
        _apply_hue_chroma(image, drawable)
        _apply_cartoon(image, drawable)
        drawable = _apply_hsv_noise_overlay(image, drawable)

        pdb.gimp_displays_flush()
    finally:
        pdb.gimp_image_undo_group_end(image)

    return drawable


def charcoal_map_current(image, drawable):
    apply_charcoal_stack(image, drawable)


def charcoal_map_folder(directory, pattern):
    if not directory or not os.path.isdir(directory):
        pdb.gimp_message("Folder not found: %s" % directory)
        return

    exts = [".png", ".jpg", ".jpeg", ".tif", ".tiff", ".bmp"]
    files = []
    for name in sorted(os.listdir(directory)):
        low = name.lower()
        if pattern and pattern not in name:
            continue
        if any(low.endswith(ext) for ext in exts):
            files.append(os.path.join(directory, name))

    if not files:
        pdb.gimp_message("No images found in: %s" % directory)
        return

    for path in files:
        image = pdb.gimp_file_load(path, path)
        drawable = image.active_layer
        apply_charcoal_stack(image, drawable)
        drawable = image.active_layer
        pdb.gimp_file_save(image, drawable, path, path)
        pdb.gimp_image_delete(image)

    pdb.gimp_message("Charcoal filter applied to %d file(s)." % len(files))


register(
    "python-fu-charcoal-map-filter",
    "Apply charcoal map filter stack to the active layer",
    "Saturation → Levels(Value) → Hue-Chroma(-53.37) → Cartoon → HSV Noise Overlay",
    "DetailedMaps",
    "DetailedMaps",
    "2026",
    "Charcoal map filter",
    "RGB*",
    [
        (PF_IMAGE, "image", "Input image", None),
        (PF_DRAWABLE, "drawable", "Input drawable", None),
    ],
    [],
    charcoal_map_current,
    menu="<Image>/Filters/Map",
)

register(
    "python-fu-charcoal-map-filter-folder",
    "Batch-apply charcoal map filter to a folder of images (overwrites files)",
    "Same stack as Charcoal map filter, applied to every matching image in a folder",
    "DetailedMaps",
    "DetailedMaps",
    "2026",
    "Charcoal map filter (folder)...",
    "",
    [
        (PF_DIRNAME, "directory", "Folder", os.path.expanduser("~")),
        (PF_STRING, "pattern", "Filename must contain (optional)", ""),
    ],
    [],
    charcoal_map_folder,
    menu="<Image>/Filters/Map",
)

main()
