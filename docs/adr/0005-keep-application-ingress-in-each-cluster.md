# Keep application ingress in each cluster

Each cluster serves its own application traffic and retains working routes and certificates while the UnHost installation is unavailable. The operator points DNS at the cluster, then UnHost configures the route and requests an HTTPS certificate. Choose the ingress implementation using measured resource use and the routing and certificate requirements.

For post-MVP scale-to-zero, the route stays active while the application's replicas are stopped. The first request can wait for readiness for an operator-configurable duration. On timeout, ingress returns HTTP 503 with a retry hint.
