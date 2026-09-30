#!/bin/bash

set -euo pipefail

script_directory="$(cd "$(dirname "$0")" && pwd)"
project_directory="$(cd "$script_directory/../.." && pwd)"
source_file="$script_directory/TransparentWindowMac.mm"
bundle_directory="$project_directory/Assets/Plugins/macOS/TransparentWindowMac.bundle"
output_file="$bundle_directory/Contents/MacOS/TransparentWindowMac"

mkdir -p "$(dirname "$output_file")"

xcrun clang++ \
    -std=c++14 \
    -fobjc-arc \
    -fblocks \
    -arch arm64 \
    -arch x86_64 \
    -mmacosx-version-min=10.13 \
    -bundle \
    -framework AppKit \
    -framework QuartzCore \
    "$source_file" \
    -o "$output_file"

echo "Built $output_file ($(lipo -archs "$output_file"))"
