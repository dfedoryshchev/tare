# 2.4.0

Cache keys now include the tenant id. Fixes cross-tenant reads on the search endpoint.

Dropped the legacy importer. It had no callers left.

Upgraded the driver to 5.2 for the connection-leak fix, see
https://github.com/example/driver/releases/tag/v5.2.0.

Config reload is now atomic. Partial writes no longer take the process down.
