# Inlay

A small templating text editor built on AvaloniaEdit/AvaloniaUI. Inline templates hold a list of choices and show one selected value. Click a template object to edit its choices.

![Inlay editing an example document in light and dark themes](docs/inlay-hero.png)

Built as a demo of my prior work with C# and AvaloniaUI.

## Test it

```sh
dotnet run --project Inlay.csproj
```
Or launch through VSCode.

## Architecture

The application follows MVVM, and implements with AvaloniaUI, ReactiveUI, and a customized templating extension built on top of AvaloniaEdit.

## Document format

Files use JSON. Text and templates are stored as ordered parts, so template locations do not depend on character offsets.

```json
{
  "formatVersion": 1,
  "lineLength": {
    "show": false,
    "enforce": false,
    "softLimit": 80,
    "hardLimit": 120
  },
  "content": [
    { "type": "text", "text": "Hello " },
    {
      "type": "template",
      "options": ["Ada", "Grace"],
      "selectedIndex": 0
    },
    { "type": "text", "text": "!" }
  ]
}
```

`selectedIndex` is `-1` when the template has no active choice. The editor displays `_____` in that case. Choices cannot be empty strings.

`lineLength` is per document and optional, defaulting to the values above. `show` draws the limit columns and `enforce` wraps the text at `hardLimit`. Both limits run from 1 to 500, and `hardLimit` cannot be below `softLimit`.


## License

Inlay is licensed under the GNU General Public License v3.0 or later. See [LICENSE](LICENSE) for details.
