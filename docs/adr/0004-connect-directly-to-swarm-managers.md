# Connect directly to Swarm managers

The first release connects to the local Docker socket or reaches remote Docker managers over SSH on a private network. Verify the SSH host key and let remote Docker keep its local socket. This keeps remote management independent of a separate agent, relay, or network-facing Docker daemon. Docker over mutual TLS can follow after the first release.
