#!/usr/bin/env bash

set -Eeuo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
workspace_dir="$(cd -- "$script_dir/../.." && pwd)"
project_path="$script_dir/HeroCapture.csproj"
output_path="$workspace_dir/docs/inlay-hero.png"
capture_dir="$(mktemp -d "${TMPDIR:-/tmp}/inlay-hero.XXXXXX")"
capture_pid=""
captured_width=""
captured_height=""

cleanup() {
  if [[ -n "$capture_pid" ]] && kill -0 "$capture_pid" 2>/dev/null; then
    kill "$capture_pid" 2>/dev/null || true
    wait "$capture_pid" 2>/dev/null || true
  fi

  if [[ -n "$capture_dir" && -d "$capture_dir" ]]; then
    rm -rf -- "$capture_dir"
  fi
}
trap cleanup EXIT INT TERM

fail() {
  echo "Hero capture failed: $*" >&2
  exit 1
}

for command_name in dotnet import magick xprop; do
  command -v "$command_name" >/dev/null 2>&1 || fail "missing required command '$command_name'"
done

[[ -n "${DISPLAY:-}" ]] || fail "DISPLAY is not set; HeroCapture requires an X11 desktop"

capture_theme() {
  local theme="$1"
  local info_path="$capture_dir/$theme.info"
  local log_path="$capture_dir/$theme.log"
  local root_path="$capture_dir/$theme-root.png"
  local client_path="$capture_dir/$theme-client.png"
  local capture_path="$capture_dir/$theme.png"

  echo "Capturing $theme theme..."
  INLAY_HERO_CAPTURE_INFO="$info_path" \
    dotnet run --no-build --project "$project_path" -- "$theme" >"$log_path" 2>&1 &
  capture_pid=$!

  local attempt
  for attempt in {1..100}; do
    [[ -s "$info_path" ]] && break
    if ! kill -0 "$capture_pid" 2>/dev/null; then
      cat "$log_path" >&2
      fail "$theme HeroCapture process exited before its window was ready"
    fi
    sleep 0.1
  done
  [[ -s "$info_path" ]] || fail "timed out waiting for the $theme HeroCapture window"

  local window_id window_x window_y
  read -r window_id window_x window_y <"$info_path"
  [[ "$window_id" =~ ^0x[0-9a-fA-F]+$ ]] || fail "HeroCapture returned an invalid X11 window ID"
  [[ "$window_x" =~ ^-?[0-9]+$ && "$window_y" =~ ^-?[0-9]+$ ]] || \
    fail "HeroCapture returned an invalid window position"

  import -window "$window_id" "$client_path"
  local client_width client_height
  read -r client_width client_height < <(
    magick identify -format '%w %h\n' "$client_path"
  )

  local extents
  extents="$(xprop -id "$window_id" _NET_FRAME_EXTENTS 2>/dev/null \
    | sed -n 's/.*= *//p' | tr -d ',')"
  local frame_left frame_right frame_top frame_bottom
  read -r frame_left frame_right frame_top frame_bottom <<<"$extents"
  [[ "$frame_left" =~ ^[0-9]+$ && "$frame_right" =~ ^[0-9]+$ && \
     "$frame_top" =~ ^[0-9]+$ && "$frame_bottom" =~ ^[0-9]+$ ]] || \
    fail "the window manager did not report frame extents"

  local frame_width=$((client_width + frame_left + frame_right))
  local frame_height=$((client_height + frame_top + frame_bottom))

  import -window root "$root_path"
  magick "$root_path" \
    -crop "${frame_width}x${frame_height}+${window_x}+${window_y}" \
    +repage "$capture_path"

  kill "$capture_pid" 2>/dev/null || true
  wait "$capture_pid" 2>/dev/null || true
  capture_pid=""

  captured_width="$frame_width"
  captured_height="$frame_height"
}

echo "Building HeroCapture..."
dotnet build "$project_path" --nologo

capture_theme light
light_width="$captured_width"
light_height="$captured_height"

capture_theme dark
dark_width="$captured_width"
dark_height="$captured_height"

[[ "$light_width" == "$dark_width" && "$light_height" == "$dark_height" ]] || \
  fail "light and dark captures have different dimensions"

mask_scale=4
mask_width=$((light_width * mask_scale))
mask_height=$((light_height * mask_scale))
top_x=$((light_width * 10 / 13))
bottom_x=$((light_width * 3 / 26))

magick -size "${mask_width}x${mask_height}" xc:black \
  -fill white \
  -draw "polygon 0,0 $((top_x * mask_scale)),0 $((bottom_x * mask_scale)),$mask_height 0,$mask_height" \
  -resize "${light_width}x${light_height}" \
  "$capture_dir/hero-mask.png"

magick "$capture_dir/dark.png" "$capture_dir/light.png" "$capture_dir/hero-mask.png" \
  -composite "$capture_dir/inlay-hero.png"

mkdir -p "$(dirname -- "$output_path")"
mv -- "$capture_dir/inlay-hero.png" "$output_path"

echo "Updated $output_path (${light_width}x${light_height})"
