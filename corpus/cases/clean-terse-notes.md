# Migration notes

Moved the job runner off the cron box. It now reads its schedule from the same config the
API uses, which removes the duplicate copy that kept drifting.

Rollback is a config flag. We left the old path in place for a fortnight and then deleted it.

Nothing surprising came out of it, which is the outcome you want from a migration.
