# Share operator actions between HTTP and MCP

Expose the public operator API through equivalent MCP tools, including write operations. Both entry points use the same application operations and authorization checks. Browser login, installation setup, health probes, and protocol discovery keep their HTTP interfaces. Codex connects directly over HTTPS with Streamable HTTP and an independently revocable client credential scoped to read or write access.
