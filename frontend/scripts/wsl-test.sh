#!/usr/bin/env bash
# test:wsl — frontend test runner optimizado para WSL.
#
# Problema: con el repo en /mnt/d (disco Windows via 9p/drvfs), leer node_modules
# desde WSL es lentisimo. jsdom no termina de bootear dentro del timeout de 60s
# que vitest tiene hardcodeado para arrancar workers (falla con
# "[vitest-pool-runner]: Timeout waiting for worker to respond") y la suite
# entera tarda muchisimo. En Windows puro no pasa nada (acceso NTFS nativo).
#
# Solucion: mantener un mirror del frontend en el filesystem nativo de Linux
# (ext4/tmpfs via $HOME/.cache) y correr vitest ahi.
#   - Primer run: copia todo el frontend incluyendo node_modules (~3 min).
#   - Runs siguientes: solo sincroniza los archivos fuente cambiados (segundos).
#   - Si package.json / package-lock.json cambian, re-sincroniza node_modules.
#
# Fuera de WSL este script corre vitest in-place (comportamiento normal para
# el companero que trabaja en Windows puro).
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CACHE_BASE="${XDG_CACHE_HOME:-$HOME/.cache}/ticketstart"
MIRROR="$CACHE_BASE/frontend-test"

# No WSL: run in place.
if ! grep -qi microsoft /proc/version 2>/dev/null; then
  cd "$ROOT"
  exec npx vitest run "$@"
fi

echo "[test:wsl] Sincronizando frontend a filesystem Linux ($MIRROR)..."
mkdir -p "$MIRROR"

# Sync de fuente (chico, rapido). node_modules/dist/.git se manejan aparte.
rsync -a --delete \
  --exclude node_modules \
  --exclude dist \
  --exclude .git \
  "$ROOT/" "$MIRROR/"

# node_modules: re-sync completo solo si falta o el lockfile cambio.
# El marker .lockfile-synced guarda la ultima vez que se sincronizo.
LOCK_MARKER="$MIRROR/node_modules/.lockfile-synced"
if [ ! -f "$LOCK_MARKER" ] ||
   [ "$ROOT/package.json" -nt "$LOCK_MARKER" ] ||
   [ "$ROOT/package-lock.json" -nt "$LOCK_MARKER" ]; then
  echo "[test:wsl] Sincronizando node_modules (primera vez o lockfile cambio)..."
  rsync -a --delete "$ROOT/node_modules/" "$MIRROR/node_modules/"
  touch "$LOCK_MARKER"
fi

cd "$MIRROR"
exec npx vitest run "$@"
