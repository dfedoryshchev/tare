# Report about the migration

In this document I want to describe how we have moved the reporting service to the new
cluster. The work was done in three steps, and each step is described below.

First, we have prepared the new environment. The configuration was copied from the old one,
with only the endpoints changed, as it is written in the runbook at
https://example.org/runbook.

After that, we have moved the traffic slowly. In the beginning only 5 percent, then 50
percent on the next day, and all of it on the third day. The dashboard link is in the
handover document.

In the end there was no incident. The old cluster is still running, we will delete it after
two weeks, when we are sure that everything is stable.
