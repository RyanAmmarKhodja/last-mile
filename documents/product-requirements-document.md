
# Product Requirements Document (PRD)

**Project Name:** Last-Mile Delivery Fleet & Real-Time Tracking System

**Version:** 1.0

**Date:** October 19, 2026

## 1. Executive Summary

The **Last-Mile Delivery Fleet & Real-Time Tracking System** is a microservice-based platform designed to handle high-frequency driver telemetry and real-time delivery tracking at scale. The platform decouples ephemeral, low-latency spatial position updates from durable, transactional order and delivery management records, ensuring high availability, sub-second visibility for customers, and audit compliance for logistics operations.

## 2. Business Objectives & Success Metrics

-   **Sub-second Map Latency:** Deliver real-time driver coordinates to the customer web portal with $< 500\text{ ms}$ latency.
    
-   **System Resilience:** Maintain zero downtime on order/shipment status lookups during peak telemetry ingestion hours.
    
-   **Cost Efficiency:** Offload high-frequency transient GPS telemetry from disk-backed storage to in-memory caching, reducing database write IOPS by over 80%.
    
-   **Scalability:** Support up to 1,000 active delivery vehicles sending telemetry pings every 2–5 seconds concurrently.
    

## 3. User Personas & Core Use Cases

### 3.1 Personas

1.  **Delivery Driver (App / Simulator):** Transmits continuous GPS coordinates during active shifts and submits proof-of-delivery signals upon package drop-off.
    
2.  **Customer / Recipient:** Views live map coordinates of their assigned delivery truck and checks current shipment status via a unique tracking link.
    
3.  **Logistics Operations Manager:** Monitors fleet-wide active deliveries and receives automated alerts on delivery exceptions or status confirmations.
    

### 3.2 Core Use Cases

-   **UC-1 (Telemetry Ingestion):** Driver application pings current GIS coordinates to the API Gateway.
    
-   **UC-2 (Live Map Tracking):** Customer opens tracking page and receives the latest van position fetched instantly from in-memory cache.
    
-   **UC-3 (Shipment State Update & Event Emission):** Driver marks a package as "Delivered." The system emits a Kafka event to update persistent database records and trigger downstream notifications.
    
## 4. Functional Requirements

-   **FR-1: Telemetry**
    
    -   **Description:** Ingest driver GPS coordinates (`latitude`, `longitude`, `timestamp`, `driver_id`) via REST API.
        
    -   **Priority:** High
        
-   **FR-2: Live Tracking**
    
    -   **Description:** Store driver coordinates in an in-memory spatial index with an automatic TTL (Time To Live).
        
    -   **Priority:** High
        
-   **FR-3: Customer Portal**
    
    -   **Description:** Expose an endpoint returning real-time vehicle coordinates for a specific shipment ID.
        
    -   **Priority:** High
        
-   **FR-4: Shipment Management**
    
    -   **Description:** Persist shipment lifecycle states (_Created_, _In Transit_, _Delivered_, _Failed_) in a relational store.
        
    -   **Priority:** High
        
-   **FR-5: Event Pipeline**
    
    -   **Description:** Publish state-change events (e.g., _Shipment Confirmed_, _Delivery Completed_) to a message broker for asynchronous processing.
        
    -   **Priority:** High
        
-   **FR-6: Gateway Routing**
    
    -   **Description:** Unify all service endpoints behind a single Reverse Proxy/API Gateway routing layer.
        
    -   **Priority:** Medium
## 5. Non-Functional Requirements

-   **Performance & Latency:**
    
    -   Telemetry ingestion endpoint response time: $< 50\text{ ms}$ (P95).
        
    -   Customer map coordinate fetch response time: $< 100\text{ ms}$ (P95).
        
-   **Throughput:**
    
    -   Support up to $500\text{ writes/sec}$ on telemetry ingestion without CPU throttling or database row locks.
        
-   **Data Retention:**
    
    -   Real-time GPS coordinate history in memory: Active shift duration / auto-expire after 10 minutes of inactivity.
        
    -   Shipment, client info, and audit trails: Permanent durable storage in a relational database.
        
-   **Availability & Reliability:**
    
    -   Driver tracking service failure must NOT impair customer shipment status lookups or order creation endpoints.
        

## 6. System Boundaries & Constraints

-   **Scope Boundaries:**
    
    -   **In Scope:** API Gateway routing, real-time driver location ingestion, Redis geospatial caching, Kafka event publication, PostgreSQL durable storage, and Docker containerization.
        
    -   **Out of Scope (For Demo):** Production OAuth2/OIDC identity providers, third-party SMS/Email integration providers, hardware GPS unit integration (simulated via script).
        
-   **Technical Constraints:**
    
    -   Must run fully containerized using `docker-compose`.
        
    -   External clients interact solely through the NGINX API Gateway.
