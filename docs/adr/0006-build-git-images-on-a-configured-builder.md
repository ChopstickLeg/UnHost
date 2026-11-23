# Build Git images on a configured builder

Run Git builds on an installation-configured builder and push the output to an operator-configured registry. A Swarm can then deploy by image digest and its nodes can pull the same artifact regardless of where it was built. The first release starts Dockerfile builds on demand and accepts public Git repositories and private repositories accessed over authenticated HTTPS.
