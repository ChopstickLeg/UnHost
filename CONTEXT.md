# UnHost

UnHost is a self-hosted platform for deploying applications and managing them across Docker Swarm clusters.

## Language

**UnHost installation**:
A running copy of UnHost that an operator uses to manage one or more Swarm clusters.
_Avoid_: Instance

**Swarm cluster**:
A Docker Swarm managed through an UnHost installation. A cluster can contain one or more nodes.

**Operator**:
A trusted person who administers an UnHost installation and can deploy applications to its clusters.

**Application**:
A named workload in one Swarm cluster that runs one container image at a time, either as a web application or as a worker.
_Avoid_: Stack, service

**Image source**:
A reference to a container image in a public or private registry.

**Git source**:
A Git repository and revision from which UnHost builds an application image.

**Deployment**:
An attempt to run a chosen image or Git revision with a specific application configuration in the application's cluster. Deployment history includes the result of each attempt.

**External service**:
A Swarm service whose deployment configuration is owned outside UnHost.

**Adoption**:
An operator's explicit transfer of deployment ownership for an external service to UnHost.
_Avoid_: Import, which connects a Swarm cluster to UnHost.

**Managed volume**:
Persistent storage an operator attaches to an application through UnHost. Its data outlives individual application tasks.

**Scale-to-zero**:
An opt-in policy for a web application that stops its replicas after a period without traffic. An incoming request can wake the application.
