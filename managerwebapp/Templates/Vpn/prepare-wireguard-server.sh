#!/usr/bin/env bash

set -euo pipefail

if [ "${EUID}" -ne 0 ]; then
  echo "This script must be run as root." >&2
  exit 1
fi

export DEBIAN_FRONTEND=noninteractive

apt-get update
apt-get install -y wireguard wireguard-tools

echo "Installed WireGuard server tools. This control node is ready to host its VPN configuration."
