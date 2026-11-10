#!/bin/sh
# Creates the topic catalogue (documents/kafka-architecture.md §3). Idempotent: safe to run on every `compose up`.
# Format: <topic>:<partitions>. Every topic also gets a <topic>.dlq with 1 partition.
set -eu

BOOTSTRAP="${BOOTSTRAP:-kafka:9092}"
REPLICATION="${REPLICATION:-1}"
RETENTION_MS="${RETENTION_MS:-2592000000}"   # 30 days (decision 2026-11-10)
KT=/opt/kafka/bin/kafka-topics.sh

TOPICS="
auth.user.v1:3
profile.client.v1:3
fleet.driver.v1:3
fleet.vehicle.v1:3
shipment.lifecycle.v1:6
"

create() {
  $KT --bootstrap-server "$BOOTSTRAP" --create --if-not-exists --topic "$1" \
      --partitions "$2" --replication-factor "$REPLICATION" \
      --config retention.ms="$RETENTION_MS" --config cleanup.policy=delete
}

for entry in $TOPICS; do
  name="${entry%%:*}"
  partitions="${entry##*:}"
  create "$name" "$partitions"
  create "$name.dlq" 1
done

echo "Topics:"
$KT --bootstrap-server "$BOOTSTRAP" --list
