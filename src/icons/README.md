# iClock Native Icon Set

This directory contains the master multi-resolution icon assets for iClock.

## Files

| File | Resolution | Target Usage |
|------|------------|--------------|
| `icon-16x16.png` | 16 × 16 | System tray at 100% DPI (Standard display) |
| `icon-20x20.png` | 20 × 20 | System tray at 125% DPI |
| `icon-24x24.png` | 24 × 24 | System tray at 150% DPI |
| `icon-32x32.png` | 32 × 32 | System tray at 200% DPI / Small Taskbar |
| `icon-40x40.png` | 40 × 40 | System tray at 250% DPI |
| `icon-48x48.png` | 48 × 48 | Standard Windows desktop / Taskbar icon |
| `icon-64x64.png` | 64 × 64 | High-DPI Windows desktop / Alt+Tab |
| `icon-128x128.png` | 128 × 128 | Windows Explorer Large Icons |
| `icon-256x256.png` | 256 × 256 | Windows Explorer Extra Large Icons / Website Favicon |
| `icon-512x512.png` | 512 × 512 | High-res marketing & documentation |
| `app.ico` | Multi-res | Packed Windows Icon containing all 9 resolutions (16px ~ 256px) |

## Generation

The icon set features:
- Alpha channel transparency outside the navy squircle (#131C43).
- Hybrid `.ico` architecture: 32bpp uncompressed DIB layers for 16px ~ 128px (ensuring 100% GDI+ / `Icon.ToBitmap()` compatibility with zero artifacts) and PNG compression for 256px (Explorer Extra Large icons).
