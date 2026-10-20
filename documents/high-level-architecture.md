
# High-Level Architecture (HLA) Document: Last-Mile Delivery Fleet & Real-Time Tracking System

**Version:** 2.0

## 1. System Overview & Context

The Last-Mile Delivery Fleet & Real-Time Tracking System is a distributed, containerized microservices platform engineered to support high-frequency driver telemetry processing, real-time map visualization, and reliable transactional shipment workflows. The architecture implements a **Database-per-Service** pattern, ensuring strict domain isolation, decoupling high-velocity transient spatial data from durable business records.

### Core Architectural Principles

-   **Edge Enforcement & Routing:** NGINX acts as the unified reverse proxy, handling path-based routing, SSL termination, and centralized identity verification offload.
    
-   **Separation of Concerns:** Ephemeral real-time telemetry (Redis) is strictly separated from persistent audit logs and transactional business data (PostgreSQL).
    
-   **Asynchronous Event-Driven Integration:** Cross-service business changes are communicated via Apache Kafka to maintain loose coupling.
    

## 2. Service Decomposition & Boundaries
![Kafka Pipeline Architecture](assets/architecture.png)

-   **API Gateway (NGINX):** Ingress controller responsible for routing public requests, SSL termination, and delegating authentication checks via upstream subrequests.
    
-   **AuthService (.NET):** Identity management service containing core user credentials, roles, and cryptographic JWT issuance.
    
-   **ProfileService (.NET):** Manages administrative profiles and client/customer records backed by an isolated PostgreSQL instance.
    
-   **FleetService (.NET):** Manages delivery driver profiles, driver availability states, and vehicle configurations backed by an isolated PostgreSQL instance.
    
-   **FleetTrackingService (.NET):** High-throughput ingestion service for driver GPS telemetry. Stores live positions in Redis geospatial cache, flushes audit points every 2 minutes to local PostgreSQL, and enforces a 2-month data retention purge policy.
    
-   **ShipmentService (.NET):** Owns transactional order lifecycles, shipping statuses, and route coordination backed by an isolated PostgreSQL instance; integrates with Kafka for event publishing.
    
-   **Apache Kafka Message Broker:** Event backbone facilitating asynchronous communication and state change distribution across business microservices.
    

## 3. Edge Routing, Authentication & Authorization Strategy

-   **Authentication (NGINX Layer):** NGINX intercepts incoming requests and queries `AuthService` to validate user identity and token validity before passing traffic downstream.
    
-   **Authorization (ASP.NET Core Layer):** Individual microservices inspect incoming JWT claims and scopes locally to enforce role-based access control (RBAC) per endpoint.
    
-   **Path-Based Routing Rules:**
    
    -   `/api/v1/auth/*` $\rightarrow$ AuthService
        
    -   `/api/v1/profiles/*` $\rightarrow$ ProfileService
        
    -   `/api/v1/fleet/*` $\rightarrow$ FleetService
        
    -   `/api/v1/tracking/*` $\rightarrow$ FleetTrackingService
        
    -   `/api/v1/shipments/*` $\rightarrow$ ShipmentService
        

## 4. Core Data Flows & Workflows

### 4.1 Driver Telemetry Ingestion Flow

1.  The Python driver telemetry script continuously pings GPS coordinates via HTTP POST to NGINX (`/api/v1/tracking/telemetry`).
    
2.  NGINX routes the payload directly to the **FleetTrackingService**.
    
3.  The FleetTrackingService updates the active geospatial index in **Redis** in-memory storage for immediate real-time retrieval.
    
4.  Concurrently, a background worker batches and flushes historic position checkpoints to the service's local PostgreSQL audit store every 2 minutes. A scheduled job purges records older than 2 months daily.
    

### 4.2 Customer Real-Time Tracking Flow

1.  The Customer Web Portal (React SPA) initializes an active map view using **Leaflet / MapLibre GL JS** with open-source OpenStreetMap vector/raster tiles.
    
2.  The UI polls or opens a lightweight connection to retrieve the designated vehicle or shipment coordinate.
    
3.  The request hits the FleetTrackingService via NGINX, which queries Redis for sub-millisecond coordinate retrieval and renders the live marker on the portal map.
    

### 4.3 Shipment Lifecycle & Event Flow

1.  An admin or client creates a shipment via the ShipmentService.
    
2.  The ShipmentService commits the transaction to its local PostgreSQL instance.
    
3.  A state change event (e.g., `ShipmentDispatched`) is published to **Apache Kafka**.
    
4.  Interested downstream consumers subscribe to the topic for notifications or analytics updates.
    

## 5. Storage & Persistence Architecture

-   **Database-per-Service Pattern:** Every microservice (`AuthService`, `ProfileService`, `FleetService`, `FleetTrackingService`, `ShipmentService`) owns an isolated PostgreSQL database container, eliminating cross-service database coupling or shared table joins.
    
-   **Ephemeral Geospatial Cache:** Redis operates as an in-memory high-performance spatial data store specifically optimized for radius searches and low-latency coordinate updates.

