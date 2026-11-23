# Run one active control plane

Ship one UnHost container with an embedded database and persistent local state. A local admin account is created during one-time setup. Back up and restore both the state and the key needed to decrypt credentials. Running one control plane keeps installation self-contained and fits the overhead goal. Swarm workloads and working application routes continue when the management server is down.
