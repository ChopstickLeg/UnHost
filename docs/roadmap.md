# UnHost roadmap

UnHost is a self-hosted deployment UI and API for operators managing Docker Swarm clusters. The project uses the existing ASP.NET Core Minimal API scaffold to learn Native AOT through a real deployment workflow. UnHost ships its .NET processes as Native AOT binaries and serves the browser UI as static files.

## 01 · First HTTPS deployment

Install and sign in to UnHost [#1](https://github.com/ChopstickLeg/UnHost/issues/1), connect or initialize a local Swarm [#2](https://github.com/ChopstickLeg/UnHost/issues/2), and deploy an image from a public registry [#3](https://github.com/ChopstickLeg/UnHost/issues/3). Compare ingress implementations on a matched Dokploy deployment [#4](https://github.com/ChopstickLeg/UnHost/issues/4), then serve the app over HTTPS [#8](https://github.com/ChopstickLeg/UnHost/issues/8). The operator sets DNS and UnHost manages the route and certificate in the cluster.

[First HTTPS deployment milestone](https://github.com/ChopstickLeg/UnHost/milestone/1)

## 02 · First release (MVP)

An operator can manage two Swarms from one installation. Each application runs one image in one cluster. Operators can reuse the same source in staging and production while keeping each application's configuration, domains, storage, and deployment history separate.

- Connect a second Swarm over a private network using SSH with host key verification. Show cluster health and instructions for joining more nodes. [#5](https://github.com/ChopstickLeg/UnHost/issues/5)
- Add saved private registry credentials and manually build a public or private Git revision with its Dockerfile on a configured builder. Push the build output to the operator's registry and deploy by image digest. [#6](https://github.com/ChopstickLeg/UnHost/issues/6) [#9](https://github.com/ChopstickLeg/UnHost/issues/9)
- Configure application environment variables and secrets. Inspect logs and deployment history, recover failed updates, and redeploy an earlier successful version. [#7](https://github.com/ChopstickLeg/UnHost/issues/7) [#10](https://github.com/ChopstickLeg/UnHost/issues/10)
- Pin an application with a named local volume to the node holding its data. Keep a single writer, retain data when the app is removed, and document moving data by backup and restore. [#11](https://github.com/ChopstickLeg/UnHost/issues/11)
- Inspect, restart, and scale compatible external Swarm services while their existing tooling retains deployment ownership. [#12](https://github.com/ChopstickLeg/UnHost/issues/12)
- Verify control-plane backup and restore. Qualify the release on two Swarms using the published Native AOT container and repeat the Dokploy comparison, with total platform idle memory as the primary measure. [#13](https://github.com/ChopstickLeg/UnHost/issues/13) [#16](https://github.com/ChopstickLeg/UnHost/issues/16)

SSH, registry credentials, app settings, local volumes, and external service controls can proceed in parallel after the first image deployment. Git builds follow registry credentials, rollback follows app settings, and control-plane restore follows the connection and credential work. The two-Swarm qualification closes the MVP. [First release milestone](https://github.com/ChopstickLeg/UnHost/milestone/2)

## After the first release

Milestones 03 through 07 can proceed independently after the MVP. Long-term retention follows the first metrics release.

| Milestone | Development order |
| --- | --- |
| [03 · Native metrics and alerts](https://github.com/ChopstickLeg/UnHost/milestone/3) | Collect host and application metrics [#14](https://github.com/ChopstickLeg/UnHost/issues/14), then add threshold alerts [#17](https://github.com/ChopstickLeg/UnHost/issues/17). |
| [04 · Remote MCP for Codex](https://github.com/ChopstickLeg/UnHost/milestone/4) | Connect Codex over authenticated Streamable HTTP [#15](https://github.com/ChopstickLeg/UnHost/issues/15), then cover the public operator API [#19](https://github.com/ChopstickLeg/UnHost/issues/19). |
| [05 · HTTP scale-to-zero](https://github.com/ChopstickLeg/UnHost/milestone/5) | Wake a sleeping application on the first request [#20](https://github.com/ChopstickLeg/UnHost/issues/20), then add idle detection and scale-down [#24](https://github.com/ChopstickLeg/UnHost/issues/24). |
| [06 · Cluster and storage interoperability](https://github.com/ChopstickLeg/UnHost/milestone/6) | External service adoption [#21](https://github.com/ChopstickLeg/UnHost/issues/21), mutual TLS [#22](https://github.com/ChopstickLeg/UnHost/issues/22), and operator-provided shared storage [#23](https://github.com/ChopstickLeg/UnHost/issues/23) can proceed in parallel. |
| [07 · Additional build and deployment workflows](https://github.com/ChopstickLeg/UnHost/milestone/7) | Dockerfile-free builds [#25](https://github.com/ChopstickLeg/UnHost/issues/25), verified push webhooks [#26](https://github.com/ChopstickLeg/UnHost/issues/26), and supported Compose stacks [#27](https://github.com/ChopstickLeg/UnHost/issues/27) can proceed in parallel. |
| [08 · Long-term metrics retention](https://github.com/ChopstickLeg/UnHost/milestone/8) | Extend metrics history after metrics and alerts ship. [#18](https://github.com/ChopstickLeg/UnHost/issues/18) |

Each milestone description shows its development order. The issues carry native GitHub blocker links for their technical prerequisites. See the [domain glossary](../CONTEXT.md) and [architecture decisions](adr/) for terminology and design rationale.

