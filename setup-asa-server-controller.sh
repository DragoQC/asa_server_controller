#!/usr/bin/env bash

export LC_ALL=C.UTF-8
export LANG=C.UTF-8
export LANGUAGE=C.UTF-8

set -euo pipefail

RESET='\033[0m'
SUCCESS_COLOR='\033[38;5;82m'
INFO_COLOR='\033[38;5;250m'
WARN_COLOR='\033[38;5;220m'
ERROR_COLOR='\033[38;5;196m'
SECTION_COLOR='\033[38;5;141m'
GIT_COLOR='\033[38;5;45m'
DOTNET_COLOR='\033[38;5;39m'

log_manager() {
  echo -e "${SECTION_COLOR}[AsaServerController]${RESET} $1"
}

log_git() {
  echo -e "${GIT_COLOR}[Git]${RESET} $1"
}

log_dotnet() {
  echo -e "${DOTNET_COLOR}[Dotnet]${RESET} $1"
}

log_ok() {
  echo -e "${SUCCESS_COLOR}✔ $1${RESET}"
}

log_info() {
  echo -e "${INFO_COLOR}ℹ $1${RESET}"
}

log_error() {
  echo -e "${ERROR_COLOR}✖ $1${RESET}"
}

USER_NAME="${USER_NAME:-asa_manager_web_app}"
GROUP_NAME="${GROUP_NAME:-$USER_NAME}"
BASE_DIR="${BASE_DIR:-/opt/asa-control}"
VPN_DIR="${VPN_DIR:-$BASE_DIR/vpn}"
NFS_DIR="${NFS_DIR:-$BASE_DIR/nfs}"
WG_INTERFACE_NAME="${WG_INTERFACE_NAME:-wg0}"
WG_CONFIG_PATH="${WG_CONFIG_PATH:-$VPN_DIR/${WG_INTERFACE_NAME}.conf}"
WG_SYSTEM_CONFIG_DIR="${WG_SYSTEM_CONFIG_DIR:-/etc/wireguard}"
WG_SYSTEM_CONFIG_PATH="${WG_SYSTEM_CONFIG_PATH:-$WG_SYSTEM_CONFIG_DIR/${WG_INTERFACE_NAME}.conf}"
WEBAPP_ROOT="${WEBAPP_ROOT:-$BASE_DIR/webapp}"
REPO_DIR="${REPO_DIR:-$WEBAPP_ROOT/src}"
PUBLISH_DIR="${PUBLISH_DIR:-$WEBAPP_ROOT/publish}"
SERVICE_NAME="${SERVICE_NAME:-asa-webapp}"
SERVICE_FILE="/etc/systemd/system/${SERVICE_NAME}.service"
SUDOERS_FILE="/etc/sudoers.d/${USER_NAME}-cluster"
REPO_URL="${REPO_URL:-https://github.com/DragoQC/asa_server_controller.git}"
REPO_BRANCH="${REPO_BRANCH:-main}"
DOTNET_CHANNEL="${DOTNET_CHANNEL:-10.0}"
DOTNET_ROOT="${DOTNET_ROOT:-/usr/share/dotnet}"
DOTNET_BIN="${DOTNET_BIN:-/usr/local/bin/dotnet}"
APP_PROJECT_RELATIVE_PATH="asa_server_controller/asa_server_controller.csproj"
APP_DLL_NAME="asa_server_controller.dll"
APP_URL="${APP_URL:-http://0.0.0.0:8010}"
APP_HOME="${APP_HOME:-$BASE_DIR}"
CLUSTER_PREP_SCRIPT_TEMPLATE_RELATIVE_PATH="asa_server_controller/Templates/Cluster/prepare-cluster-server.sh"
CLUSTER_PREP_SCRIPT_PATH="${VPN_DIR}/prepare-cluster-server.sh"
NFS_APPLY_SCRIPT_TEMPLATE_RELATIVE_PATH="asa_server_controller/Templates/Cluster/apply-nfs-server.sh"
NFS_APPLY_SCRIPT_PATH="${NFS_DIR}/apply-nfs-server.sh"

if [ "${EUID}" -ne 0 ]; then
  log_error "This script must be run as root."
  exit 1
fi

run_as_app_user() {
  runuser -u "${USER_NAME}" -- "$@"
}

run_as_app_user_bash() {
  runuser -u "${USER_NAME}" -- bash -lc "$1"
}

log_manager "ASA Server Controller Installer"

log_manager "Installing dependencies..."
apt update
apt install -y git curl wget tar ca-certificates sudo
log_ok "Installed dependencies."

if ! getent group "${GROUP_NAME}" >/dev/null 2>&1; then
  groupadd --system "${GROUP_NAME}"
fi

if ! id -u "${USER_NAME}" >/dev/null 2>&1; then
  useradd \
    --system \
    --gid "${GROUP_NAME}" \
    --home-dir "${APP_HOME}" \
    --create-home \
    --shell /bin/bash \
    "${USER_NAME}"
  log_ok "Created user ${USER_NAME}."
else
  log_info "User ${USER_NAME} already exists."
fi

mkdir -p \
  "${BASE_DIR}" \
  "${VPN_DIR}" \
  "${NFS_DIR}" \
  "${WEBAPP_ROOT}" \
  "${PUBLISH_DIR}"

chown -R "${USER_NAME}:${GROUP_NAME}" "${BASE_DIR}"
chmod 0755 "${BASE_DIR}"
chmod 0775 "${VPN_DIR}"
chmod 0775 "${NFS_DIR}"
log_ok "Prepared ${BASE_DIR}."

mkdir -p "${WG_SYSTEM_CONFIG_DIR}"
if [ ! -f "${WG_CONFIG_PATH}" ]; then
  install -o "${USER_NAME}" -g "${GROUP_NAME}" -m 0664 /dev/null "${WG_CONFIG_PATH}"
fi

ln -sfn "${WG_CONFIG_PATH}" "${WG_SYSTEM_CONFIG_PATH}"
log_ok "Prepared WireGuard config path ${WG_CONFIG_PATH} -> ${WG_SYSTEM_CONFIG_PATH}."

cat <<EOF > "${SUDOERS_FILE}"
${USER_NAME} ALL=(root) NOPASSWD: ${CLUSTER_PREP_SCRIPT_PATH}
${USER_NAME} ALL=(root) NOPASSWD: ${NFS_APPLY_SCRIPT_PATH}
${USER_NAME} ALL=(root) NOPASSWD: /usr/bin/systemctl is-active wg-quick@${WG_INTERFACE_NAME} --quiet
${USER_NAME} ALL=(root) NOPASSWD: /usr/bin/systemctl status wg-quick@${WG_INTERFACE_NAME} --no-pager --full
${USER_NAME} ALL=(root) NOPASSWD: /usr/bin/systemctl enable wg-quick@${WG_INTERFACE_NAME}
${USER_NAME} ALL=(root) NOPASSWD: /usr/bin/systemctl start wg-quick@${WG_INTERFACE_NAME}
${USER_NAME} ALL=(root) NOPASSWD: /usr/bin/systemctl stop wg-quick@${WG_INTERFACE_NAME}
${USER_NAME} ALL=(root) NOPASSWD: /usr/bin/systemctl restart wg-quick@${WG_INTERFACE_NAME}
EOF
chmod 0440 "${SUDOERS_FILE}"
visudo -cf "${SUDOERS_FILE}"
log_ok "Granted ${USER_NAME} access to run the cluster server prep script and query/start/stop/restart wg-quick@${WG_INTERFACE_NAME}."

if [ ! -x "${DOTNET_BIN}" ] || ! "${DOTNET_BIN}" --list-sdks 2>/dev/null | grep -q "^${DOTNET_CHANNEL%%.*}\\."; then
  log_dotnet "Installing latest .NET SDK from channel ${DOTNET_CHANNEL}..."
  TEMP_INSTALL_SCRIPT="$(mktemp)"
  curl -fsSL https://dot.net/v1/dotnet-install.sh -o "${TEMP_INSTALL_SCRIPT}"
  bash "${TEMP_INSTALL_SCRIPT}" --channel "${DOTNET_CHANNEL}" --install-dir "${DOTNET_ROOT}"
  rm -f "${TEMP_INSTALL_SCRIPT}"
  ln -sf "${DOTNET_ROOT}/dotnet" "${DOTNET_BIN}"
  log_ok "Installed latest .NET SDK from channel ${DOTNET_CHANNEL}."
else
  log_ok ".NET SDK ${DOTNET_CHANNEL} already installed."
fi

export DOTNET_ROOT
export PATH="/usr/local/bin:${PATH}"

log_git "Fetching repository..."
if [ ! -d "${REPO_DIR}/.git" ]; then
  rm -rf "${REPO_DIR}"
  mkdir -p "$(dirname "${REPO_DIR}")"
  run_as_app_user git clone --branch "${REPO_BRANCH}" "${REPO_URL}" "${REPO_DIR}"
  log_ok "Cloned ${REPO_URL}."
else
  run_as_app_user git -C "${REPO_DIR}" fetch --all --prune
  run_as_app_user git -C "${REPO_DIR}" checkout "${REPO_BRANCH}"
  run_as_app_user git -C "${REPO_DIR}" reset --hard "origin/${REPO_BRANCH}"
  log_ok "Updated local repository copy."
fi

log_dotnet "Publishing control web app..."
rm -rf "${PUBLISH_DIR}"
mkdir -p "${PUBLISH_DIR}"
chown -R "${USER_NAME}:${GROUP_NAME}" "${WEBAPP_ROOT}"

run_as_app_user_bash "export DOTNET_ROOT='${DOTNET_ROOT}'; export PATH='${DOTNET_ROOT}:/usr/local/bin:/usr/bin:/bin'; cd '${REPO_DIR}'; '${DOTNET_BIN}' publish '${APP_PROJECT_RELATIVE_PATH}' -c Release -o '${PUBLISH_DIR}'"

mkdir -p "${PUBLISH_DIR}/Data"

if [ -f "${REPO_DIR}/${CLUSTER_PREP_SCRIPT_TEMPLATE_RELATIVE_PATH}" ]; then
  cp "${REPO_DIR}/${CLUSTER_PREP_SCRIPT_TEMPLATE_RELATIVE_PATH}" "${CLUSTER_PREP_SCRIPT_PATH}"
  chown root:root "${CLUSTER_PREP_SCRIPT_PATH}"
  chmod 0755 "${CLUSTER_PREP_SCRIPT_PATH}"
fi

if [ -f "${REPO_DIR}/${NFS_APPLY_SCRIPT_TEMPLATE_RELATIVE_PATH}" ]; then
  cp "${REPO_DIR}/${NFS_APPLY_SCRIPT_TEMPLATE_RELATIVE_PATH}" "${NFS_APPLY_SCRIPT_PATH}"
  chown root:root "${NFS_APPLY_SCRIPT_PATH}"
  chmod 0755 "${NFS_APPLY_SCRIPT_PATH}"
fi

chown -R "${USER_NAME}:${GROUP_NAME}" "${WEBAPP_ROOT}"
log_ok "Published control web app to ${PUBLISH_DIR}."

log_manager "Creating systemd service..."
cat <<EOF > "${SERVICE_FILE}"
[Unit]
Description=ASA Server Controller Web App
After=network.target

[Service]
Type=simple
User=${USER_NAME}
Group=${GROUP_NAME}
WorkingDirectory=${PUBLISH_DIR}
Environment=DOTNET_ROOT=${DOTNET_ROOT}
Environment=ASPNETCORE_URLS=${APP_URL}
ExecStart=${DOTNET_BIN} ${PUBLISH_DIR}/${APP_DLL_NAME}
Restart=always
RestartSec=5
KillSignal=SIGINT

[Install]
WantedBy=multi-user.target
EOF

systemctl daemon-reload
systemctl enable --now "${SERVICE_NAME}"
log_ok "Created and started ${SERVICE_NAME}."

MACHINE_IP="$(hostname -I | awk '{print $1}')"
if [ -z "${MACHINE_IP}" ]; then
  MACHINE_IP="127.0.0.1"
fi

log_manager "Current IPv4 addresses:"
ip -4 -o addr show scope global | awk '{print "  - " $2 ": " $4}'
log_manager "You can now connect at http://${MACHINE_IP}:8010"
log_manager "Service status:"
systemctl status "${SERVICE_NAME}" --no-pager
