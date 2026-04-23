#!/usr/bin/env bash

set -euo pipefail

SERVER_CONFIG_PATH="${SERVER_CONFIG_PATH:-/opt/asa-control/nfs/exports.conf}"
SYSTEM_EXPORTS_PATH="${SYSTEM_EXPORTS_PATH:-/etc/exports}"
NFS_SERVICE_NAME="${NFS_SERVICE_NAME:-nfs-server}"
SYSTEMCTL_BIN="${SYSTEMCTL_BIN:-/usr/bin/systemctl}"
EXPORTFS_BIN="${EXPORTFS_BIN:-/usr/sbin/exportfs}"

if [ "${EUID}" -ne 0 ]; then
  echo "This script must be run as root." >&2
  exit 1
fi

if [ ! -f "${SERVER_CONFIG_PATH}" ]; then
  echo "NFS server config not found: ${SERVER_CONFIG_PATH}" >&2
  exit 1
fi

mkdir -p "$(dirname "${SYSTEM_EXPORTS_PATH}")"

WAS_ACTIVE=0
if "${SYSTEMCTL_BIN}" is-active "${NFS_SERVICE_NAME}" --quiet; then
  WAS_ACTIVE=1
  "${SYSTEMCTL_BIN}" stop "${NFS_SERVICE_NAME}"
fi

install -m 0644 "${SERVER_CONFIG_PATH}" "${SYSTEM_EXPORTS_PATH}"
"${EXPORTFS_BIN}" -ra
"${SYSTEMCTL_BIN}" enable "${NFS_SERVICE_NAME}"

if [ "${WAS_ACTIVE}" -eq 1 ]; then
  "${SYSTEMCTL_BIN}" start "${NFS_SERVICE_NAME}"
else
  "${SYSTEMCTL_BIN}" start "${NFS_SERVICE_NAME}"
fi

"${SYSTEMCTL_BIN}" status "${NFS_SERVICE_NAME}" --no-pager --full
