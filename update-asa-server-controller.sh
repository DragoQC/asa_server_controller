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
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

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

log_warn() {
  echo -e "${WARN_COLOR}⚠ $1${RESET}"
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

load_required_packages() {
  local requirements_file="$1"

  if [ ! -f "${requirements_file}" ]; then
    log_error "Requirements file was not found: ${requirements_file}"
    exit 1
  fi

  REQUIRED_PACKAGES=()

  while IFS= read -r line || [ -n "${line}" ]; do
    line="${line%%#*}"
    line="$(printf '%s' "${line}" | xargs)"

    if [ -z "${line}" ]; then
      continue
    fi

    if [[ "${line}" == *"|"* ]]; then
      IFS='|' read -r -a package_options <<< "${line}"
      local selected_package
      selected_package="$(find_first_available_package "${package_options[@]}")" || {
        log_error "Could not find any supported package for: ${line}"
        exit 1
      }
      REQUIRED_PACKAGES+=("${selected_package}")
      continue
    fi

    REQUIRED_PACKAGES+=("${line}")
  done < "${requirements_file}"
}

USER_NAME="${USER_NAME:-asa_manager_web_app}"
GROUP_NAME="${GROUP_NAME:-$USER_NAME}"
BASE_DIR="${BASE_DIR:-/opt/asa-control}"
WEBAPP_ROOT="${WEBAPP_ROOT:-$BASE_DIR/webapp}"
REPO_DIR="${REPO_DIR:-$WEBAPP_ROOT/src}"
PUBLISH_DIR="${PUBLISH_DIR:-$WEBAPP_ROOT/publish}"
NEXT_PUBLISH_DIR="${NEXT_PUBLISH_DIR:-$WEBAPP_ROOT/publish-next}"
PREVIOUS_PUBLISH_DIR="${PREVIOUS_PUBLISH_DIR:-$WEBAPP_ROOT/publish-prev}"
SERVICE_NAME="${SERVICE_NAME:-asa-webapp}"
SERVICE_FILE="/etc/systemd/system/${SERVICE_NAME}.service"
REPO_URL="${REPO_URL:-https://github.com/DragoQC/ASA_Server_Manager_Control.git}"
REPO_BRANCH="${REPO_BRANCH:-main}"
DOTNET_SDK_VERSION="${DOTNET_SDK_VERSION:-10.0.203}"
DOTNET_ROOT="${DOTNET_ROOT:-/usr/share/dotnet}"
DOTNET_BIN="${DOTNET_BIN:-/usr/local/bin/dotnet}"
APP_PROJECT_RELATIVE_PATH="asa_server_controller/asa_server_controller.csproj"
APP_DLL_NAME="asa_server_controller.dll"
APP_URL="${APP_URL:-http://0.0.0.0:8010}"
APP_DATA_ROOT="${APP_DATA_ROOT:-$BASE_DIR/data}"
UPDATE_LINK_PATH="${UPDATE_LINK_PATH:-/usr/local/bin/update-asa-server-controller}"
SHORT_UPDATE_LINK_PATH="${SHORT_UPDATE_LINK_PATH:-/usr/local/bin/update}"
SYSTEM_PACKAGES_FILE_RELATIVE_PATH="requirements/system-packages.txt"

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

install_update_links() {
  chmod 0755 "${REPO_DIR}/update-asa-server-controller.sh"
  install_update_command "${UPDATE_LINK_PATH}"
  install_update_command "${SHORT_UPDATE_LINK_PATH}"
  log_ok "Installed ${UPDATE_LINK_PATH} updater command."
  log_ok "Installed ${SHORT_UPDATE_LINK_PATH} updater command."
}

write_service_file() {
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
}

log_manager "ASA Server Controller Updater"

if [ ! -x "${DOTNET_BIN}" ] || ! "${DOTNET_BIN}" --list-sdks 2>/dev/null | grep -q '^10\.0\.'; then
  log_error ".NET 10 SDK is not installed. Run the setup script first."
  exit 1
fi

if ! id -u "${USER_NAME}" >/dev/null 2>&1; then
  log_error "App user ${USER_NAME} does not exist. Run the setup script first."
  exit 1
fi

mkdir -p "${BASE_DIR}" "${WEBAPP_ROOT}" "${APP_DATA_ROOT}"
chown -R "${USER_NAME}:${GROUP_NAME}" "${BASE_DIR}"

export DOTNET_ROOT
export PATH="/usr/local/bin:${PATH}"

log_git "Fetching repository..."
if [ ! -d "${REPO_DIR}/.git" ]; then
  mkdir -p "$(dirname "${REPO_DIR}")"
  run_quiet run_as_app_user git clone --branch "${REPO_BRANCH}" "${REPO_URL}" "${REPO_DIR}"
  log_ok "Cloned ${REPO_URL} (${REPO_BRANCH})."
else
  run_quiet run_as_app_user git -C "${REPO_DIR}" fetch --all --prune

  if ! run_as_app_user git -C "${REPO_DIR}" show-ref --verify --quiet "refs/remotes/origin/${REPO_BRANCH}"; then
    log_error "Remote branch origin/${REPO_BRANCH} was not found."
    exit 1
  fi

  run_quiet run_as_app_user git -C "${REPO_DIR}" checkout "${REPO_BRANCH}"
  run_quiet run_as_app_user git -C "${REPO_DIR}" reset --hard "origin/${REPO_BRANCH}"
  log_ok "Updated local repository copy to origin/${REPO_BRANCH}."
fi

install_update_links

log_manager "Updating apt requirements..."
run_quiet apt update
load_required_packages "${REPO_DIR}/${SYSTEM_PACKAGES_FILE_RELATIVE_PATH}"
run_quiet apt install -y "${REQUIRED_PACKAGES[@]}"
log_ok "Updated dependencies."

log_dotnet "Publishing control web app..."
rm -rf "${NEXT_PUBLISH_DIR}"
mkdir -p "${NEXT_PUBLISH_DIR}"
chown -R "${USER_NAME}:${GROUP_NAME}" "${WEBAPP_ROOT}"

run_quiet run_as_app_user_bash "export DOTNET_ROOT='${DOTNET_ROOT}'; export DOTNET_CLI_TELEMETRY_OPTOUT='1'; export DOTNET_NOLOGO='1'; export PATH='${DOTNET_ROOT}:/usr/local/bin:/usr/bin:/bin'; cd '${REPO_DIR}'; '${DOTNET_BIN}' publish '${APP_PROJECT_RELATIVE_PATH}' -c Release -o '${NEXT_PUBLISH_DIR}'"

write_service_file

if systemctl is-active --quiet "${SERVICE_NAME}"; then
  log_manager "Stopping ${SERVICE_NAME}..."
  run_quiet systemctl stop "${SERVICE_NAME}"
fi

rm -rf "${PREVIOUS_PUBLISH_DIR}"
if [ -d "${PUBLISH_DIR}" ]; then
  mv "${PUBLISH_DIR}" "${PREVIOUS_PUBLISH_DIR}"
fi

mv "${NEXT_PUBLISH_DIR}" "${PUBLISH_DIR}"
chown -R "${USER_NAME}:${GROUP_NAME}" "${WEBAPP_ROOT}"

run_quiet systemctl daemon-reload
run_quiet systemctl enable "${SERVICE_NAME}"
run_quiet systemctl restart "${SERVICE_NAME}"

rm -rf "${PREVIOUS_PUBLISH_DIR}"

log_ok "Updated and restarted ${SERVICE_NAME}."
log_info "Repository branch: ${REPO_BRANCH}"
log_info "Publish path: ${PUBLISH_DIR}"
log_info "App data path: ${APP_DATA_ROOT}"
if [ "${VERBOSE}" = true ]; then
  log_manager "Service status:"
  systemctl status "${SERVICE_NAME}" --no-pager
fi
