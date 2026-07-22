# Caching

When it comes to caching, the invalidation story is what decides whether the whole thing
was worth doing. At the end of the day a stale read is worse than a slow one, which the
incident review at https://example.org/incidents sets out in detail.

We cache the rendered page for sixty seconds and bust it on write, which is documented in
the runbook at https://example.org/runbook.
