## Pixel anchor

- Character frame: 128x128 subject area on PixelLab's padded transparent square canvas (expected final canvas near 256x256, matching the existing 240/244px battle-character convention).
- Portrait: 128x128 transparent bust.
- Status and equipment icons: 32x32 transparent.
- View: high top-down for battlefield rotations and animation; forward/three-quarter bust for portrait.
- Palette: outline `#17151b`, charcoal `#29262b`, soot brown `#49372e`, dull iron `#697176`, old parchment `#c8b896`, dried blood `#7b3034`, restrained Limbo violet `#6d3f68`, muted skin `#8a6953`, warm highlight `#d7c9aa`.
- Outline: hard one-pixel-equivalent near-black outline; no anti-aliasing.
- Shading: three-tone clusters, restrained highlights, no gradients, no bloom.
- Silhouette lock: soot hood, iron half-mask, patched penitential robe over quilted jack, cracked bell-hook staff, and false reliquary must remain readable in every frame.
- Import: Sprite (2D and UI), alpha transparency, Filter Mode Point, no mipmaps, uncompressed, 64 pixels per unit for battlefield sprites and 100 pixels per unit for UI art.
