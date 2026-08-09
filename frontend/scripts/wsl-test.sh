#!/usr/bin/env bash
# test — frontend test runner con workaround dinamico para WSL.
#
# Antecedente: con el repo en /mnt/d (disco Windows montado via 9p/drvfs),
# leer node_modules desde WSL es lentisimo. jsdom no termina de bootear
# dentro del timeout de 60s que vitest tiene hardcodeado para arrancar
# workers (falla con "[vitest-pool-runner]: Timeout waiting for worker to
# respond") y la suite entera tarda muchisimo. En Windows puro no pasa nada
# (acceso NTFS nativo).
#
# Solucion: detectar el filesystem type del directorio del repo. Si NO es
# nativo (9p/drvfs, o cualquier montaje lento), mantener un mirror del
# frontend en el filesystem nativo de Linux (ext4/tmpfs via $HOME/.cache)
# y correr vitest ahi.
#   - Primer run: copia todo el frontend incluyendo node_modules (~3 min).
#   - Runs siguientes: solo sincroniza los archivos fuente cambiados (segundos).
#   - Si package.json / package-lock.json cambian, re-sincroniza node_modules.
# Si ya es un filesystem nativo (ext4, etc.) o estas en Windows puro sin
# WSL, corre vitest in-place sin mirror. El mismo comando sirve para ambos
# entornos: se auto-ajusta segun donde viva el repo.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CACHE_BASE="${XDG_CACHE_HOME:-$HOME/.cache}/ticketstart"
MIRROR="$CACHE_BASE/frontend-test"

# Filesystems nativos de Linux: acceso rapido, no hacen falta mirror.
# Nota: stat -f -c %T reporta ext4 como "ext2/ext3" en algunos kernels;
# findmnt reporta "ext4". Cubrir ambos.
NATIVE_FS='ext2 ext3 ext4 ext2/ext3 btrfs xfs zfs tmpfs overlay f2fs jfs nilfs2 reiserfs'

# Detecta el filesystem type del directorio del repo.
# findmnt da nombres limpios (ext4, 9p); stat es el fallback portable
# (en Windows puro con git-bash puede no soportar -f y devolver vacio,
# lo que lleva al camino in-place).
detect_fs() {
  if command -v findmnt >/dev/null 2>&1; then
    findmnt -no FSTYPE --target "$ROOT" 2>/dev/null && return
  fi
  if command -v stat >/dev/null 2>&1; then
    stat -f -c %T "$ROOT" 2>/dev/null
  fi
}

FS_TYPE="$(detect_fs || true)"

# Sin FS detectado (ej. Windows puro con git-bash, sin stat -f): correr
# in-place. Igual para filesystems nativos: no hace falta mirror.
if [ -z "$FS_TYPE" ] || echo "$NATIVE_FS" | grep -qw "$FS_TYPE"; then
  cd "$ROOT"
  exec npx vitest run "$@"
fi

echo "[test] Filesystem '$FS_TYPE' no nativo ($ROOT) — sincronizando a filesystem Linux ($MIRROR)..."
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
  echo "[test] Sincronizando node_modules (primera vez o lockfile cambio)..."
  rsync -a --delete "$ROOT/node_modules/" "$MIRROR/node_modules/"
  touch "$LOCK_MARKER"
fi

cd "$MIRROR"
exec npx vitest run "$@"
