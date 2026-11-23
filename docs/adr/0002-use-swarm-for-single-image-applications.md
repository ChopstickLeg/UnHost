# Use Swarm for single-image applications

Use the same Swarm service model on one machine and on a cluster of many machines. An operator can connect to an existing Swarm or initialize Swarm on a Docker host and follow join instructions for additional nodes. Limit each application to a single image in the first release so the same deployment workflow serves web applications and workers, while leaving multi-service stacks for later.

An application belongs to one cluster. Operators can use the same image or Git source for separate applications and manage each application's settings, routes, storage, and deployment history independently.
