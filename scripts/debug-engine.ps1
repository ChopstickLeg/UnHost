param(
    [ValidateSet('start', 'reset', 'stop', 'status')]
    [string]$Action = 'start',
    [string]$Distribution = 'Ubuntu'
)

$ErrorActionPreference = 'Stop'

$script = @'
set -euo pipefail

action="$1"
name="unhost-debug-engine"
socket_dir="/tmp/unhost-debug-engine"
socket="$socket_dir/docker.sock"
image="docker:28-dind"

wait_ready() {
  for _ in $(seq 1 60); do
    if [ -S "$socket" ] && curl -sf --unix-socket "$socket" http://localhost/version >/dev/null 2>&1; then
      return 0
    fi
    sleep 0.5
  done
  echo "Timed out waiting for $name at $socket" >&2
  docker logs --tail 30 "$name" >&2 || true
  return 1
}

print_status() {
  if ! docker inspect "$name" >/dev/null 2>&1; then
    echo "Throwaway engine ($name) is not running."
    return 0
  fi
  running="$(docker inspect -f '{{.State.Running}}' "$name")"
  ip="$(docker inspect -f '{{.NetworkSettings.Networks.bridge.IPAddress}}' "$name")"
  if [ "$running" = "true" ] && [ -S "$socket" ]; then
    info="$(docker --host "unix://$socket" info --format 'version={{.ServerVersion}} swarm={{.Swarm.LocalNodeState}}' 2>/dev/null || echo 'unreachable')"
    echo "Throwaway engine ($name): running ($info)"
    echo "Socket: $socket"
    echo "Advertise address: $ip (or eth0)"
  else
    echo "Throwaway engine ($name): stopped"
  fi
}

stop_engine() {
  docker rm -f --volumes "$name" >/dev/null 2>&1 || true
  rm -rf "$socket_dir"
}

start_engine() {
  if [ "$(docker inspect -f '{{.State.Running}}' "$name" 2>/dev/null || true)" = "true" ] &&
     [ -S "$socket" ] &&
     curl -sf --unix-socket "$socket" http://localhost/version >/dev/null 2>&1; then
    print_status
    return 0
  fi

  mkdir -p "$socket_dir"
  chmod 750 "$socket_dir"
  rm -f "$socket"

  if docker inspect "$name" >/dev/null 2>&1; then
    docker start "$name" >/dev/null
  else
    docker run -d --privileged --name "$name" \
      -v "$socket_dir:/engine" \
      "$image" \
      dockerd --host unix:///engine/docker.sock --group "$(id -g)" >/dev/null
  fi

  wait_ready
  print_status
}

case "$action" in
  start)
    start_engine
    ;;
  reset)
    stop_engine
    start_engine
    ;;
  stop)
    stop_engine
    echo "Throwaway engine ($name) removed."
    ;;
  status)
    print_status
    ;;
esac
'@ -replace "`r`n", "`n"

$encoded = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes($script))
wsl -d $Distribution --exec bash -c 'printf %s "$1" | base64 -d | bash -s -- "$2"' -- $encoded $Action
exit $LASTEXITCODE
