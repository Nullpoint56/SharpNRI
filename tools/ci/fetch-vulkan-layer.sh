#!/usr/bin/env bash
# Fetch the Khronos validation layer that matches the Vulkan headers NRI builds against.
#
#   tools/ci/fetch-vulkan-layer.sh <dest> [sdk-version]
#
# Ubuntu 24.04's vulkan-validationlayers package is 1.3.275 and LunarG's apt repository stops at
# 1.4.313; NRI v190 pins Vulkan-Headers 1.4.365 and enables what the driver offers, so an older
# layer reports every struct and extension it does not know as a validation ERROR (CLAUDE.md: Vulkan loader and validation layers).
# The only current Linux binary is inside LunarG's SDK tarball (~370 MB), from which this takes
# the one library and its manifest, strips the library, and lays them out so that
#
#   export VK_ADD_LAYER_PATH=<dest>/share/vulkan/explicit_layer.d
#
# puts the layer in front of the system one. The loader itself stays the system's: that is the
# configuration users have, and the one patches/0004 exists for.
set -euo pipefail
dest="${1:?dest}"
ver="${2:-${VULKAN_SDK_VERSION:-1.4.363.0}}"
manifest="$dest/share/vulkan/explicit_layer.d/VkLayer_khronos_validation.json"
if [[ -f "$manifest" && -f "$dest/lib/libVkLayer_khronos_validation.so" ]]; then
    echo "==> validation layer present in $dest ($(grep -o '"api_version": "[^"]*"' "$manifest"))"
    exit 0
fi
tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT
url="https://sdk.lunarg.com/sdk/download/$ver/linux/vulkansdk-linux-x86_64-$ver.tar.xz"
echo "==> downloading $url"
curl -fsSL -o "$tmp/sdk.tar.xz" "$url"
echo "==> extracting the layer"
tar -xJf "$tmp/sdk.tar.xz" -C "$tmp" --wildcards \
    "*/x86_64/lib/libVkLayer_khronos_validation.so" \
    "*/x86_64/share/vulkan/explicit_layer.d/VkLayer_khronos_validation.json"
mkdir -p "$dest/lib" "$dest/share/vulkan/explicit_layer.d"
cp "$tmp/$ver/x86_64/lib/libVkLayer_khronos_validation.so" "$dest/lib/"
cp "$tmp/$ver/x86_64/share/vulkan/explicit_layer.d/VkLayer_khronos_validation.json" "$manifest"
strip --strip-unneeded "$dest/lib/libVkLayer_khronos_validation.so" || true
echo "==> validation layer $ver in $dest ($(du -h "$dest/lib/libVkLayer_khronos_validation.so" | cut -f1))"
