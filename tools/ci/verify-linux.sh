#!/usr/bin/env bash
# Run the linux-x64 CI leg locally (WSL2 Ubuntu 24.04 or any x64 Debian/Ubuntu):
# native build -> managed build -> tests. Mirrors the linux-x64 job in .github/workflows/ci.yml.
#
#   tools/ci/verify-linux.sh               # install missing packages with sudo apt, then run
#   SKIP_APT=1 tools/ci/verify-linux.sh    # packages already present
#
# Needs: pwsh 7, a .NET 11 SDK on PATH, cmake >= 3.30, a C++17 compiler, git.
# Run it from a Linux filesystem checkout (e.g. ~/src/SharpNRI), not /mnt/c or /mnt/e: the build
# writes several GB into _build/linux-x64 and NTFS through 9p is very slow. A Windows checkout's
# runtimes/win-x64 is unaffected either way; this script only writes _build/linux-x64 and
# runtimes/linux-x64.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$root"

if [[ "$(uname -s)" != "Linux" || "$(uname -m)" != "x86_64" ]]; then
    echo "verify-linux.sh: needs Linux x86_64, got $(uname -s) $(uname -m)" >&2
    exit 1
fi

if [[ ! -f nri/Include/NRI.h ]]; then
    echo "==> initialising submodule"
    git submodule update --init --recursive
fi

if [[ "${SKIP_APT:-0}" != "1" ]]; then
    pkgs=(cmake ninja-build clang libvulkan1 mesa-vulkan-drivers vulkan-validationlayers libx11-dev libwayland-dev)
    missing=()
    for p in "${pkgs[@]}"; do
        dpkg-query -W -f='${Status}' "$p" 2>/dev/null | grep -q "install ok installed" || missing+=("$p")
    done
    if ((${#missing[@]})); then
        echo "==> apt install ${missing[*]}"
        sudo apt-get update -qq
        sudo apt-get install -y -qq --no-install-recommends "${missing[@]}"
    fi
fi

# The Khronos validation layer that matches NRI's Vulkan headers; the distribution's is too old
# (tools/ci/fetch-vulkan-layer.sh). The loader stays the distribution's.
bash tools/ci/fetch-vulkan-layer.sh _tools/vvl
export VK_ADD_LAYER_PATH="$root/_tools/vvl/share/vulkan/explicit_layer.d"

for tool in pwsh dotnet cmake git; do
    command -v "$tool" >/dev/null || { echo "verify-linux.sh: '$tool' not found on PATH" >&2; exit 1; }
done
cmake_ver="$(cmake --version | head -n1 | awk '{print $3}')"
if [[ "$(printf '%s\n3.30\n' "$cmake_ver" | sort -V | head -n1)" != "3.30" ]]; then
    echo "verify-linux.sh: NRI needs cmake >= 3.30, found $cmake_ver (pip install cmake, or Kitware's apt repo)" >&2
    exit 1
fi
case "$(dotnet --version)" in
    11.*) ;;
    *) echo "verify-linux.sh: need a .NET 11 SDK, found $(dotnet --version)" >&2; exit 1 ;;
esac

# lavapipe must be visible to the loader, otherwise device tests have nothing to run on.
if command -v vulkaninfo >/dev/null; then
    vulkaninfo --summary 2>/dev/null | grep -i -E "deviceName|driverName" || true
fi

echo "==> native"
pwsh -NoProfile -File tools/build-native.ps1 -Rid linux-x64 -Configuration Release

echo "==> managed"
dotnet build SharpNRI.slnx -c Release

echo "==> test"
dotnet test SharpNRI.slnx -c Release --no-build

echo "==> linux-x64 leg passed"
