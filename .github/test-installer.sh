#!/usr/bin/env bash
set -euo pipefail

# These scenarios stop, uninstall, or reconfigure the host's Docker Engine.
[[ ${GITHUB_ACTIONS:-} == true && ${RUNNER_ENVIRONMENT:-} == github-hosted ]] ||
    { echo 'Run this test on a disposable GitHub-hosted VM.' >&2; exit 1; }

host_state=${1:?Pass clean, stopped, manager, or worker.}
test_dir=$(mktemp -d)
registry_pid=
cleanup() {
    if [[ -n $registry_pid ]]; then
        kill "$registry_pid" 2>/dev/null || true
    fi
    rm -rf -- "$test_dir"
}
trap cleanup EXIT

if [[ $host_state == worker ]]; then
    # The host joins a second Engine running as the manager on its bridge network.
    docker run --privileged -d --name installer-swarm-manager docker:28-dind \
        dockerd --host=unix:///var/run/docker.sock
    for ((attempt = 0; attempt < 30; attempt++)); do
        if docker exec installer-swarm-manager docker info >/dev/null 2>&1; then
            break
        fi
        sleep 1
    done
    manager_ip=$(docker inspect -f '{{.NetworkSettings.Networks.bridge.IPAddress}}' installer-swarm-manager)
    docker exec installer-swarm-manager docker swarm init --advertise-addr "$manager_ip" >/dev/null
    token=$(docker exec installer-swarm-manager docker swarm join-token -q worker)
    docker swarm join --token "$token" "$manager_ip:2377"
    [[ $(docker info -f '{{.Swarm.LocalNodeState}} {{.Swarm.ControlAvailable}}') == 'active false' ]]
    if sudo bash install.sh >"$test_dir/output" 2>&1; then
        echo 'Installer unexpectedly accepted a Swarm worker.' >&2
        exit 1
    fi
    cat "$test_dir/output"
    rg -F 'This host is a Swarm worker.' "$test_dir/output"
    sudo test ! -e /opt/unhost
    exit 0
fi

# Run the fixture registry directly on the VM so it survives stopping or
# removing Docker. The fixture serves the status response the installer expects.
registry_container=$(docker create registry:2)
docker cp "$registry_container:/bin/registry" "$test_dir/registry"
docker rm "$registry_container" >/dev/null
cat >"$test_dir/registry.yaml" <<EOF
version: 0.1
storage:
  filesystem:
    rootdirectory: $test_dir/images
http:
  addr: 127.0.0.1:5000
EOF
"$test_dir/registry" serve "$test_dir/registry.yaml" >"$test_dir/registry.log" 2>&1 &
registry_pid=$!
curl -fsS --retry 10 --retry-connrefused --retry-delay 1 http://127.0.0.1:5000/v2/ >/dev/null

image=127.0.0.1:5000/unhost-fixture:ci
docker build -t "$image" - <<'EOF'
FROM busybox:1.37
RUN mkdir -p /www/api && printf '%s\n' '{"setupComplete":false}' > /www/api/status
USER 1654
CMD ["httpd", "-f", "-p", "8080", "-h", "/www"]
EOF
docker push "$image"

case $host_state in
    clean)
        expected='Installing Docker Engine and Compose.'
        sudo systemctl stop docker.service docker.socket
        packages=()
        for package in docker-ce docker-ce-cli docker-ce-rootless-extras docker-buildx-plugin \
            docker-compose-plugin containerd.io docker.io docker-compose-v2 containerd runc; do
            if [[ $(dpkg-query -W -f='${db:Status-Status}' "$package" 2>/dev/null) == installed ]]; then
                packages+=("$package")
            fi
        done
        (( ${#packages[@]} > 0 ))
        sudo apt-get purge -y "${packages[@]}"
        sudo rm -f /var/run/docker.sock
        ! command -v docker && ! command -v dockerd && [[ ! -S /var/run/docker.sock ]] ||
            { echo 'The runner still has Docker installed.' >&2; exit 1; }
        ;;
    stopped)
        expected='Starting the Docker Engine service.'
        sudo systemctl stop docker.service docker.socket
        if sudo docker info >/dev/null 2>&1; then
            echo 'Docker should be unreachable before the installer runs.' >&2
            exit 1
        fi
        ;;
    manager)
        expected='Swarm manager detected.'
        docker swarm init >/dev/null
        ;;
    *)
        echo "Unknown host state: $host_state" >&2
        exit 1
        ;;
esac

sudo env UNHOST_IMAGE="$image" bash install.sh | tee "$test_dir/output"
rg -F "$expected" "$test_dir/output"
rg -F 'UnHost is running at http://127.0.0.1:8080' "$test_dir/output"
curl -fsS http://127.0.0.1:8080/api/status | rg -F '{"setupComplete":false}'
sudo docker compose version
sudo rg -q '^127\.0\.0\.1:5000/unhost-fixture@sha256:[a-f0-9]{64}$' /opt/unhost/image.txt
