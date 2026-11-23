# Separate service operations from adoption

UnHost discovers external Swarm services on connected clusters and provides status, task and log inspection, rolling restarts, and replica changes where Docker supports them. Those operations leave ownership of the service specification with its existing tooling. A later explicit adoption workflow can take ownership of compatible services after previewing changes and saving the original specification for best-effort recovery.
