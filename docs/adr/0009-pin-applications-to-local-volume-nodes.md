# Pin applications to local volume nodes

In the first release, UnHost attaches Docker named volumes on an operator-selected node. A placement constraint keeps the application on the node holding its data. Otherwise, Swarm can reschedule it on a different node and start it with an empty volume of the same name. Applications with writable local volumes run one replica, and a rollout stops the old task before starting a replacement that writes to the same volume.

Keep volume data after application removal and document backup and restore to another node. Support for shared storage supplied by an operator can follow after the first release.
