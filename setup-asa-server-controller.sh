#!/usr/bin/env bash

export LC_ALL=C.UTF-8
export LANG=C.UTF-8
export LANGUAGE=C.UTF-8
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

set -euo pipefail

RESET='\033[0m'
SUCCESS_COLOR='\033[38;5;82m'
INFO_COLOR='\033[38;5;250m'
WARN_COLOR='\033[38;5;220m'
ERROR_COLOR='\033[38;5;196m'
SECTION_COLOR='\033[38;5;141m'
GIT_COLOR='\033[38;5;45m'
DOTNET_COLOR='\033[38;5;39m'
VERBOSE=false

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

while [ "$#" -gt 0 ]; do
  case "$1" in
    -v|--verbose)
      VERBOSE=true
      ;;
  esac
  shift
done

run_quiet() {
  if [ "${VERBOSE}" = true ]; then
    "$@"
    return
  fi

  local output_file
  output_file="$(mktemp)"
  if "$@" >"${output_file}" 2>&1; then
    rm -f "${output_file}"
    return
  fi

  cat "${output_file}" >&2
  rm -f "${output_file}"
  return 1
}

find_first_available_package() {
  for package_name in "$@"; do
    if apt-cache show "${package_name}" >/dev/null 2>&1; then
      printf '%s\n' "${package_name}"
      return 0
    fi
  done

  return 1
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
REPO_URL="${REPO_URL:-https://github.com/DragoQC/ASA_Server_Manager_Control.git}"
REPO_BRANCH="${REPO_BRANCH:-main}"
DOTNET_SDK_VERSION="${DOTNET_SDK_VERSION:-10.0.203}"
DOTNET_ROOT="${DOTNET_ROOT:-/usr/share/dotnet}"
DOTNET_BIN="${DOTNET_BIN:-/usr/local/bin/dotnet}"
APP_PROJECT_RELATIVE_PATH="asa_server_controller/asa_server_controller.csproj"
APP_DLL_NAME="asa_server_controller.dll"
APP_URL="${APP_URL:-http://0.0.0.0:8010}"
APP_HOME="${APP_HOME:-$BASE_DIR}"
APP_DATA_ROOT="${APP_DATA_ROOT:-$BASE_DIR/data}"
UPDATE_LINK_PATH="${UPDATE_LINK_PATH:-/usr/local/bin/update-asa-server-controller}"
SHORT_UPDATE_LINK_PATH="${SHORT_UPDATE_LINK_PATH:-/usr/local/bin/update}"
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

install_update_command() {
  local command_path="$1"

  cat <<EOF > "${command_path}"
#!/usr/bin/env bash
exec "${REPO_DIR}/update-asa-server-controller.sh" "\$@"
EOF

  chmod 0755 "${command_path}"
}

log_manager "ASA Server Controller Installer"

log_manager "Installing dependencies..."
run_quiet apt update
ICU_PACKAGE="$(find_first_available_package libicu76 libicu72 libicu-dev)" || {
  log_error "Could not find a supported libicu package in apt."
  exit 1
}

run_quiet apt install -y \
  git \
  curl \
  wget \
  tar \
  ca-certificates \
  sudo \
  libgssapi-krb5-2 \
  "${ICU_PACKAGE}" \
  libssl3 \
  zlib1g \
  libc6-i386 \
  lib32gcc-s1 \
  lib32stdc++6
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
  "${APP_DATA_ROOT}" \
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
${USER_NAME} ALL=(root) NOPASSWD: /usr/sbin/sysctl -w net.ipv4.ip_forward=1
${USER_NAME} ALL=(root) NOPASSWD: /usr/sbin/iptables
EOF
chmod 0440 "${SUDOERS_FILE}"
visudo -cf "${SUDOERS_FILE}"
log_ok "Granted ${USER_NAME} access to run cluster setup, manage wg-quick@${WG_INTERFACE_NAME}, and apply game port forwarding rules."

if [ ! -x "${DOTNET_BIN}" ] || ! "${DOTNET_BIN}" --list-sdks 2>/dev/null | grep -q "^${DOTNET_SDK_VERSION}$"; then
  log_dotnet "Installing .NET SDK ${DOTNET_SDK_VERSION}..."
  TEMP_INSTALL_SCRIPT="$(mktemp)"
  run_quiet curl -fsSL https://dot.net/v1/dotnet-install.sh -o "${TEMP_INSTALL_SCRIPT}"
  run_quiet bash "${TEMP_INSTALL_SCRIPT}" --version "${DOTNET_SDK_VERSION}" --install-dir "${DOTNET_ROOT}"
  rm -f "${TEMP_INSTALL_SCRIPT}"
  ln -sf "${DOTNET_ROOT}/dotnet" "${DOTNET_BIN}"
  log_ok "Installed .NET SDK ${DOTNET_SDK_VERSION}."
else
  log_ok ".NET SDK ${DOTNET_SDK_VERSION} already installed."
fi

export DOTNET_ROOT
export PATH="/usr/local/bin:${PATH}"

log_git "Fetching repository..."
if [ ! -d "${REPO_DIR}/.git" ]; then
  rm -rf "${REPO_DIR}"
  mkdir -p "$(dirname "${REPO_DIR}")"
  run_quiet run_as_app_user git clone --branch "${REPO_BRANCH}" "${REPO_URL}" "${REPO_DIR}"
  log_ok "Cloned ${REPO_URL}."
else
  run_quiet run_as_app_user git -C "${REPO_DIR}" fetch --all --prune
  run_quiet run_as_app_user git -C "${REPO_DIR}" checkout "${REPO_BRANCH}"
  run_quiet run_as_app_user git -C "${REPO_DIR}" reset --hard "origin/${REPO_BRANCH}"
  log_ok "Updated local repository copy."
fi

log_dotnet "Publishing control web app..."
rm -rf "${PUBLISH_DIR}"
mkdir -p "${PUBLISH_DIR}"
chown -R "${USER_NAME}:${GROUP_NAME}" "${WEBAPP_ROOT}"

run_quiet run_as_app_user_bash "export DOTNET_ROOT='${DOTNET_ROOT}'; export DOTNET_CLI_TELEMETRY_OPTOUT='1'; export DOTNET_NOLOGO='1'; export PATH='${DOTNET_ROOT}:/usr/local/bin:/usr/bin:/bin'; cd '${REPO_DIR}'; '${DOTNET_BIN}' publish '${APP_PROJECT_RELATIVE_PATH}' -c Release -o '${PUBLISH_DIR}'"

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

if [ -f "${REPO_DIR}/update-asa-server-controller.sh" ]; then
  chmod 0755 "${REPO_DIR}/update-asa-server-controller.sh"
  install_update_command "${UPDATE_LINK_PATH}"
  install_update_command "${SHORT_UPDATE_LINK_PATH}"
  log_ok "Installed ${UPDATE_LINK_PATH} updater command."
  log_ok "Installed ${SHORT_UPDATE_LINK_PATH} updater command."
fi

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
Environment=DOTNET_CLI_TELEMETRY_OPTOUT=1
Environment=DOTNET_NOLOGO=1
Environment=ASPNETCORE_URLS=${APP_URL}
Environment=ASA_CONTROL_DATA_DIR=${APP_DATA_ROOT}
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
