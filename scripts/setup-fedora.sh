#!/bin/sh
# Reproduces the Fedora desktop setup for the editor-migration branch:
#   packages (git, gh, Vulkan/Mesa + vulkan-tools), .NET SDK in ~/.dotnet, Prowl built in ~/Projects/Prowl,
#   this repo in ~/Projects/sandbox-game (branch editor-migration), ~/Projects/SandboxEditor symlink, ~/Projects/run-editor.sh.
# Usage: sh scripts/setup-fedora.sh            (re-runnable; skips what is already there)
# Env:   DOTNET_SDK=10.0.402  PROWL_REF=2510d8fc  SKIP_DNF=1
# Prowl is pinned to 2510d8fc (same as the laptop and external/Prowl). Current Prowl main (tested 2026-10-23, d13446a6)
# breaks the game: Color(byte,byte,byte,byte) became an obsolete error (Town.cs) and the editor migrates the project
# format (rewrites texture .meta files), which the pinned laptop editor can't share. Bump both together when upgrading.
set -e
DOTNET_SDK=${DOTNET_SDK:-10.0.402}
PROWL_REF=${PROWL_REF:-2510d8fc}
P=$HOME/Projects
mkdir -p "$P"

if [ -z "$SKIP_DNF" ]; then
  # Mesa Vulkan covers AMD/Intel; NVIDIA needs the proprietary driver (RPM Fusion akmod-nvidia) for its Vulkan ICD.
  sudo dnf install -y git gh mesa-vulkan-drivers vulkan-loader vulkan-tools curl libicu
fi

# .NET SDK (Fedora's dotnet-sdk-10.0 package lags the Microsoft feature band; global.json wants $DOTNET_SDK).
if ! "$HOME/.dotnet/dotnet" --list-sdks 2>/dev/null | grep -q "^$DOTNET_SDK "; then
  curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
  bash /tmp/dotnet-install.sh --version "$DOTNET_SDK" --install-dir "$HOME/.dotnet"
fi
export PATH=$HOME/.dotnet:$PATH DOTNET_ROOT=$HOME/.dotnet

# Prowl engine + editor + CLI
[ -d "$P/Prowl/.git" ] || git clone https://github.com/ProwlEngine/Prowl.git "$P/Prowl"
git -C "$P/Prowl" fetch -q origin
git -C "$P/Prowl" checkout -q "$PROWL_REF"
(cd "$P/Prowl" && nice -n 10 dotnet build Prowl.Editor/Prowl.Editor.csproj && nice -n 10 dotnet build Prowl.Cli/Prowl.Cli.csproj)

# Game repo (private: needs `gh auth login` first)
if [ ! -d "$P/sandbox-game/.git" ]; then
  gh repo clone Jax-Danger/sandbox-game "$P/sandbox-game" -- -b editor-migration
else
  git -C "$P/sandbox-game" checkout -q editor-migration && git -C "$P/sandbox-game" pull --ff-only
fi
ln -sfn "$P/sandbox-game/SandboxEditor" "$P/SandboxEditor"

cat > "$P/run-editor.sh" <<'EOF'
#!/bin/sh
# Launches the Prowl editor on SandboxEditor (symlink -> ~/Projects/sandbox-game/SandboxEditor), detached.
# Log: ~/Projects/editor.log (previous run kept as editor.prev.log). Crash dumps: ~/Projects/dumps.
export DISPLAY=${DISPLAY:-:0} WAYLAND_DISPLAY=${WAYLAND_DISPLAY:-wayland-0} XDG_RUNTIME_DIR=${XDG_RUNTIME_DIR:-/run/user/$(id -u)}
[ -z "$XAUTHORITY" ] && XAUTHORITY=$(ls $XDG_RUNTIME_DIR/xauth_* 2>/dev/null | head -1) && export XAUTHORITY
export PATH=$HOME/.dotnet:$PATH DOTNET_ROOT=$HOME/.dotnet
# Prefer the discrete NVIDIA GPU for Vulkan (falls back to the default order if not present).
[ -f /usr/share/vulkan/icd.d/nvidia_icd.json ] && export __NV_PRIME_RENDER_OFFLOAD=1 MESA_VK_DEVICE_SELECT=10de:*
export DOTNET_DbgEnableMiniDump=1 DOTNET_DbgMiniDumpType=2 DOTNET_DbgMiniDumpName=$HOME/Projects/dumps/editor.%p.dmp DOTNET_EnableCrashReport=1
mkdir -p $HOME/Projects/dumps
[ -f $HOME/Projects/editor.log ] && mv $HOME/Projects/editor.log $HOME/Projects/editor.prev.log
# Newer Prowl puts the editor in Build/Editor/<Config>/<tfm>/; older builds in Build/Editor.
ED=$(dirname "$(find $HOME/Projects/Prowl/Build/Editor -maxdepth 3 -name Prowl.Editor -type f | head -1)")
cd "$ED"
setsid nohup sh -c './Prowl.Editor --project $HOME/Projects/SandboxEditor; rc=$?; echo "[run-editor] editor exited rc=$rc ($( [ $rc -gt 128 ] && kill -l $((rc-128)) )) at $(date)"' > $HOME/Projects/editor.log 2>&1 < /dev/null &
EOF
chmod +x "$P/run-editor.sh"

command -v vulkaninfo >/dev/null && vulkaninfo --summary 2>/dev/null | grep -E "deviceName|driverName" || true
echo
echo "Done. Start the editor: ~/Projects/run-editor.sh   (log: ~/Projects/editor.log)"
echo "Hills and Town map scenes are git-ignored (large): with the editor open run"
echo "  cd ~/Projects/SandboxEditor && ./bake-map.sh hills && ./bake-map.sh town"
echo "Then just 'git pull' in ~/Projects/sandbox-game to update."
