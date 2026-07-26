# Incident 412

Paged at 02:14. Queue depth climbing, consumers alive but idle.

Cause was a poison message. The handler threw before the ack, so the broker kept
redelivering it, as the redelivery counter in the dashboard shows.

Moved it to the dead-letter queue by hand. Depth back to normal by 02:51.

Follow-up: the handler needs a try/catch around deserialize. Ticket filed.
