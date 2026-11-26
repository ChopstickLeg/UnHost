#!/usr/bin/env bash
set -euo pipefail
umask 077

die() {
    printf 'UnHost install: %s\n' "$*" >&2
    exit 1
}

if [[ ${1:-} == --help ]]; then
    printf '%s\n' 'Usage: sudo bash install.sh' \
        'Optional environment variables: UNHOST_IMAGE and UNHOST_PORT (default 8080).' \
        'Installs to /opt/unhost. Automatic Docker installation supports Ubuntu and Debian.'
    exit 0
fi

[[ $# == 0 ]] || die 'Unexpected arguments. Use --help for usage.'
[[ $(uname -s) == Linux ]] || die 'Run this installer on the Linux Docker host.'
[[ $EUID == 0 ]] || die 'Run this installer with sudo or as root.'

install_dir=/opt/unhost
socket=/var/run/docker.sock
port=${UNHOST_PORT:-8080}
[[ $port =~ ^[1-9][0-9]{0,4}$ ]] && (( port <= 65535 )) ||
    die 'UNHOST_PORT must be an integer from 1 to 65535.'

unset DOCKER_CONTEXT DOCKER_HOST
docker_local() {
    docker --host "unix://$socket" "$@"
}

install_docker_packages() {
    [[ -r /etc/os-release ]] || die 'Install Docker Engine and Compose for your distribution, then rerun.'
    # shellcheck source=/dev/null
    source /etc/os-release
    case ${ID:-} in
        ubuntu|debian) ;;
        *) die 'Automatic Docker installation supports Ubuntu and Debian. Install the missing Docker components, then rerun.' ;;
    esac
    local codename=${UBUNTU_CODENAME:-${VERSION_CODENAME:-}}
    [[ $codename =~ ^[a-z]+$ ]] || die 'Could not determine the distro release for the Docker package repository.'

    export DEBIAN_FRONTEND=noninteractive
    apt-get update || return 1
    # Reuse a configured Docker repository instead of adding a second signing key.
    if apt-cache show "$1" 2>/dev/null | awk '/^Package: / { found = 1 } END { exit !found }'; then
        apt-get install -y --no-remove "$@"
        return $?
    fi
    apt-get install -y --no-remove ca-certificates curl || return 1
    install -d -m 0755 /etc/apt/keyrings /etc/apt/sources.list.d || return 1
    local key architecture
    architecture=$(dpkg --print-architecture) || return 1
    key=$(mktemp) || return 1
    if ! curl -fsSL "https://download.docker.com/linux/$ID/gpg" -o "$key"; then
        rm -f "$key"
        die 'Could not download the Docker repository signing key.'
    fi
    if ! install -m 0644 "$key" /etc/apt/keyrings/unhost-docker.asc; then
        rm -f "$key"
        return 1
    fi
    rm -f "$key"
    cat > /etc/apt/sources.list.d/unhost-docker.sources <<EOF || return 1
Types: deb
URIs: https://download.docker.com/linux/$ID
Suites: $codename
Components: stable
Architectures: $architecture
Signed-By: /etc/apt/keyrings/unhost-docker.asc
EOF
    chmod 0644 /etc/apt/sources.list.d/unhost-docker.sources || return 1
    apt-get update || return 1
    apt-get install -y --no-remove "$@"
}

if ! command -v docker >/dev/null || { [[ ! -S $socket ]] && ! command -v dockerd >/dev/null; }; then
    [[ ! -S $socket ]] || die 'A local Docker socket exists but the CLI is missing. Install a CLI compatible with the existing Engine and rerun.'
    printf '%s\n' 'Installing Docker Engine and Compose.'
    install_docker_packages docker-ce docker-ce-cli containerd.io docker-compose-plugin ||
        die 'Docker installation failed. Review the package manager output above.'
fi

swarm_status() {
    docker_local info --format '{{.Swarm.LocalNodeState}} {{.Swarm.ControlAvailable}}'
}

if ! swarm=$(swarm_status 2>/dev/null); then
    if command -v systemctl >/dev/null &&
        [[ $(systemctl show --property=LoadState --value docker.service 2>/dev/null) == loaded ]] &&
        ! systemctl is-active --quiet docker.service; then
        printf '%s\n' 'Starting the Docker Engine service.'
        systemctl start docker.service ||
            die 'Could not start Docker. Inspect: journalctl -u docker.service -n 50'
    fi
    swarm=$(swarm_status) ||
        die "Cannot reach Docker at $socket. Check that the local Engine is running and listening on that socket."
fi
case $swarm in
    'inactive false') printf '%s\n' 'Standalone Engine detected. Initialize Swarm from UnHost when ready.' ;;
    'active true') printf '%s\n' 'Swarm manager detected. Import the existing Swarm from UnHost.' ;;
    'active false') die 'This host is a Swarm worker. Run the installer on a Swarm manager.' ;;
    *) die "Docker reported an unexpected Swarm state ($swarm). Resolve it before installing." ;;
esac

if ! docker_local compose version >/dev/null 2>&1; then
    printf '%s\n' 'Installing the Docker Compose plugin.'
    if command -v apt-get >/dev/null && apt-cache show docker-compose-plugin >/dev/null 2>&1; then
        DEBIAN_FRONTEND=noninteractive apt-get install -y --no-remove docker-compose-plugin ||
            die 'Compose installation failed. Review the package manager output above.'
    elif command -v apt-get >/dev/null && apt-cache show docker-compose-v2 >/dev/null 2>&1; then
        DEBIAN_FRONTEND=noninteractive apt-get install -y --no-remove docker-compose-v2 ||
            die 'Compose installation failed. Review the package manager output above.'
    else
        install_docker_packages docker-compose-plugin ||
            die 'Compose installation failed. Review the package manager output above.'
    fi
    docker_local compose version >/dev/null || die 'Docker Compose is still unavailable after installation.'
fi

if ! command -v curl >/dev/null; then
    command -v apt-get >/dev/null || die 'Install curl so the installer can check HTTP readiness.'
    DEBIAN_FRONTEND=noninteractive apt-get install -y --no-remove curl ||
        die 'Could not install curl. Review the package manager output above.'
fi
socket_gid=$(stat -c '%g' "$socket") || die "Cannot read the group ID of $socket."
install -d -m 0700 "$install_dir"
exec 9>"$install_dir/.install.lock"
flock -n 9 || die 'Another UnHost installer is running.'
cd "$install_dir"

image=${UNHOST_IMAGE:-}
if [[ -z $image && -s image.txt ]]; then
    read -r image < image.txt
fi
image=${image:-ghcr.io/chopstickleg/unhost:latest}
if [[ $image != *@sha256:* ]] || ! docker_local image inspect "$image" >/dev/null 2>&1; then
    docker_local pull "$image" || die "Could not pull $image."
fi
image=$(docker_local image inspect --format '{{index .RepoDigests 0}}' "$image") ||
    die 'Could not pin the UnHost image to a registry digest.'
[[ $image =~ ^[a-z0-9./:_-]+@sha256:[0-9a-f]{64}$ ]] || die 'Docker did not return a valid image digest.'
app_uid=$(docker_local image inspect --format '{{.Config.User}}' "$image") ||
    die 'Could not determine the UnHost container user.'
[[ $app_uid =~ ^[1-9][0-9]*$ ]] || die 'The UnHost image must specify a numeric non-root user.'

if [[ ! -s bootstrap.txt ]]; then
    head -c 32 /dev/urandom | base64 > bootstrap.txt.tmp
    mv -f bootstrap.txt.tmp bootstrap.txt
fi
chown "$app_uid" bootstrap.txt
chmod 0400 bootstrap.txt

cat > compose.yaml.tmp <<EOF
services:
  unhost:
    image: "$image"
    restart: unless-stopped
    ports:
      - "127.0.0.1:$port:8080"
    group_add:
      - "$socket_gid"
    environment:
      UnHost__BootstrapSecretFile: /run/secrets/bootstrap
    volumes:
      - unhost-data:/data
      - type: bind
        source: $socket
        target: $socket
        bind:
          create_host_path: false
    secrets:
      - bootstrap
volumes:
  unhost-data:
secrets:
  bootstrap:
    file: ./bootstrap.txt
EOF
mv -f compose.yaml.tmp compose.yaml
printf '%s\n' "$image" > image.txt.tmp
mv -f image.txt.tmp image.txt

compose() {
    docker_local compose --file "$install_dir/compose.yaml" --project-name unhost "$@"
}
compose up -d || die 'Compose could not start UnHost. Review the error above.'
compose exec -T unhost sh -c 'test -S /var/run/docker.sock && test -w /var/run/docker.sock' ||
    die 'The UnHost container cannot write to the Docker socket. Check the mount, socket permissions, and supplemental GID.'
compose exec -T unhost sh -c 'test -r /run/secrets/bootstrap' ||
    die 'The UnHost container cannot read the bootstrap secret. Check the secret file permissions.'

url="http://127.0.0.1:$port"
for ((attempt = 0; attempt < 30; attempt++)); do
    if status=$(curl --noproxy '*' -fsS --max-time 2 "$url/api/status" 2>/dev/null) &&
        [[ $status == *'"setupComplete":false'* || $status == *'"setupComplete":true'* ]]; then
        printf 'UnHost is running at %s on this server.\n' "$url"
        if [[ $status == *'"setupComplete":false'* ]]; then
            printf 'For first-run setup, read the bootstrap secret with sudo cat %s/bootstrap.txt\n' "$install_dir"
        fi
        printf 'If connecting remotely, forward port %s over SSH.\n' "$port"
        exit 0
    fi
    sleep 1
done

die "UnHost did not respond at $url/api/status. Inspect: docker compose -p unhost -f $install_dir/compose.yaml logs unhost"
