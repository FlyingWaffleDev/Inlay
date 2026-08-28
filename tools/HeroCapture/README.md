# HeroCapture

HeroCapture is a simple shell script/C# combo that rebuilds the split-theme screenshot used near the top of the main README. It opens the product-launch example document in light and dark modes, triggers the central template flyout, captures both windows, and combines them along a diagonal.

## Run it from VS Code

Open the Command Palette and choose `Tasks: Run Task`, then select `update Inlay hero image`.

The task replaces [`docs/inlay-hero.png`](../../docs/inlay-hero.png) after both captures complete. Keep the desktop visible and avoid moving windows or opening menus while it runs. The popup is a separate X11 window, so the script captures the relevant part of the desktop rather than the application window alone.

You can run the same command from a terminal:

```sh
./tools/HeroCapture/capture.sh
```

## Change the example

Edit [`examples/product-launch.itd`](../../examples/product-launch.itd). HeroCapture copies the file into its build output and loads it with `JsonTemplateDocumentService`, so invalid document data stops the task before it replaces the existing hero image.

The capture tool opens the second (visually centered) template in the file. If the document structure changes, update the `ElementAt(1)` selection in [`Program.cs`](Program.cs).

## Requirements

The current capture process targets the project's Linux desktop setup. It needs:

- an active X11 session with `DISPLAY` set
- KDE with the Breeze Light and Breeze Dark color schemes installed
- the .NET SDK
- ImageMagick's `import` and `magick` commands
- `xprop`

HeroCapture places its window at a fixed position. The script reads the actual client size and KDE frame extents before cropping, so changes to the capture dimensions or window decoration size do not require matching edits in the script.

Temporary screenshots stay outside the repository and are removed when the task finishes or fails. The existing hero image is replaced only after the light capture, dark capture, mask, and composite all succeed.
