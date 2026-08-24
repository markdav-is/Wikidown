#!/bin/sh
# Installs the wikidown CLI. If a .NET runtime is already on PATH, uses the
# small NuGet global-tool package (dotnet tool install -g Wikidown.Cli). If
# not, downloads a self-contained single-file binary from this repo's
# GitHub Releases — no .NET installation required either way.
#
# Usage: curl -fsSL https://wikidown.org/install.sh | sh
set -eu

REPO="markdav-is/Wikidown"
INSTALL_DIR="${WIKIDOWN_INSTALL_DIR:-$HOME/.wikidown/bin}"

say() { printf '%s\n' "$1" >&2; }
die() { say "error: $1"; exit 1; }

if command -v dotnet >/dev/null 2>&1; then
  say "Found dotnet — installing Wikidown.Cli as a global tool..."
  # dotnet tool install already no-ops cleanly (exit 0) if it's installed --
  # `dotnet tool update` is the documented, separate way to move versions.
  dotnet tool install -g Wikidown.Cli || die "dotnet tool install failed"
  say "Installed. Run 'wikidown init' to set up this repo."
  exit 0
fi

say "No dotnet found — installing a self-contained wikidown binary..."

# ── resolve RID ──────────────────────────────────────────────────────────
os="$(uname -s)"
arch="$(uname -m)"

case "$os" in
  Linux)  plat=linux ;;
  Darwin) plat=osx ;;
  *)      die "unsupported OS '$os' — install the .NET SDK and run: dotnet tool install -g Wikidown.Cli" ;;
esac

case "$arch" in
  x86_64|amd64)   cpu=x64 ;;
  aarch64|arm64)  cpu=arm64 ;;
  *)              die "unsupported architecture '$arch' — install the .NET SDK and run: dotnet tool install -g Wikidown.Cli" ;;
esac

rid="${plat}-${cpu}"
asset="wikidown-${rid}.tar.gz"

# ── find the newest cli-v* release (GitHub's own "latest" endpoint mixes
#    in this repo's other release tracks, e.g. vsix-v*, so it can't be
#    trusted here) ───────────────────────────────────────────────────────
say "Resolving latest release for $rid..."
tag="$(curl -fsSL "https://api.github.com/repos/$REPO/releases" \
  | grep -o '"tag_name": *"cli-v[^"]*"' \
  | head -n 1 \
  | sed 's/.*"\(cli-v[^"]*\)"/\1/')"
[ -n "$tag" ] || die "couldn't find a cli-v* release on GitHub"

url="$(curl -fsSL "https://api.github.com/repos/$REPO/releases/tags/$tag" \
  | grep -o "\"browser_download_url\": *\"[^\"]*${asset}\"" \
  | head -n 1 \
  | sed 's/.*"\(https:[^"]*\)"/\1/')"
[ -n "$url" ] || die "release $tag has no asset named $asset"

# ── download + extract ──────────────────────────────────────────────────
tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT

say "Downloading $tag ($rid)..."
curl -fsSL "$url" -o "$tmp/$asset"
tar -xzf "$tmp/$asset" -C "$tmp"

mkdir -p "$INSTALL_DIR"
mv "$tmp/wikidown" "$INSTALL_DIR/wikidown"
chmod +x "$INSTALL_DIR/wikidown"

# macOS quarantines files downloaded via a browser/LaunchServices-aware
# client with com.apple.quarantine, which Gatekeeper then blocks on first
# run. A plain curl download usually doesn't set it, but strip it
# defensively in case some environment does.
if [ "$plat" = osx ]; then
  xattr -d com.apple.quarantine "$INSTALL_DIR/wikidown" 2>/dev/null || true
fi

# ── add to PATH ──────────────────────────────────────────────────────────
case ":$PATH:" in
  *":$INSTALL_DIR:"*) ;;
  *)
    rc="$HOME/.profile"
    case "${SHELL:-}" in
      */zsh)  rc="$HOME/.zshrc" ;;
      */bash) rc="$HOME/.bashrc" ;;
    esac
    if ! grep -qF "$INSTALL_DIR" "$rc" 2>/dev/null; then
      printf '\nexport PATH="%s:$PATH"\n' "$INSTALL_DIR" >> "$rc"
    fi
    say "Added $INSTALL_DIR to PATH in $rc — restart your shell, or run:"
    say "  export PATH=\"$INSTALL_DIR:\$PATH\""
    ;;
esac

say "Installed wikidown ($tag, $rid) to $INSTALL_DIR"
say "Run 'wikidown init' to set up this repo."
