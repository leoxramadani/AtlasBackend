# Where to Look

Welcome! To make your review as efficient and insightful as possible, here is a concise guide to navigating this repository.

---

### 1. Key Files to Read

If you only have time to inspect 3–4 areas, these best represent the architectural foundation, domain modeling, and asynchronous reliability of Atlas:

1. **[`Atlas.Application/Services/OnboardingService.cs`](AtlasBackend/Atlas.Application/Services/OnboardingService.cs)**  
   *Core Business Logic & Orchestration.* Orchestrates the end-to-end multi-market customer onboarding workflow, enforces domain invariants, manages document integrity checks, and delegates external compliance workflows.
2. **[`AtlasBackend/Controllers/ApplicationsController.cs`](AtlasBackend/AtlasBackend/Controllers/ApplicationsController.cs)**  
   *API Surface & Request Handling.* Demonstrates clean HTTP request handling, validation, idempotency, and non-blocking asynchronous dispatch.
3. **[`Atlas.Infrastructure/Caching/RedisCacheService.cs`](AtlasBackend/Atlas.Infrastructure/Caching/RedisCacheService.cs)**  
   *Distributed Caching Strategy.* Illustrates resilient cache-aside patterns, TTL management, serialization, and graceful fallbacks when cache misses occur.
4. **[`Atlas.Infrastructure/Messaging/Consumers/`](AtlasBackend/Atlas.Infrastructure/Messaging/Consumers/)**  
   *(Specifically [`ApplicationVerificationConsumer.cs`](AtlasBackend/Atlas.Infrastructure/Messaging/Consumers/ApplicationVerificationConsumer.cs) and [`AccountProvisioningConsumer.cs`](AtlasBackend/Atlas.Infrastructure/Messaging/Consumers/AccountProvisioningConsumer.cs))*  
   *Event-Driven Asynchronous Processing.* Shows how MassTransit & RabbitMQ decouple heavy third-party KYC/AML verification and core banking account provisioning with retry resiliency and durable messaging.

---

### 2. The Decision I Most Want to Discuss

**Modular Monolith with Domain-Driven Design (DDD) vs. Microservices**

* **The Dilemma:** Early-stage financial platforms often prematurely adopt microservices, introducing distributed transactions (2PC/Sagas), network latency, partial failures, and high operational overhead.
* **The Choice:** Atlas was designed as a **Modular Monolith** using strict DDD principles and Clean Architecture. This guarantees ACID consistency across state changes and audit logging while maintaining decoupled bounded contexts.
* **Evolution Path:** By isolating domain logic into separate projects and using MassTransit message contracts, contexts (e.g., verification or provisioning) can be extracted into standalone microservices effortlessly when scale demands it.
* *Bonus Discussion Topics:* I would also love to discuss **RabbitMQ reliability & at-least-once delivery**, **Redis caching & cache invalidation tradeoffs**, and **multi-market regulatory rules modeling**.

---

### 3. What to Ignore

* **Micro-Syntactic Preferences:** Choices regarding minor C# stylistic conventions—such as `var list = new List<string>();` vs. `List<string> list = new();` / explicit types, or localized formatting decisions.
* **Simulated Third-Party Integrations:** Mocked external vendor adapters (e.g., identity verification services and core banking APIs) used in place of actual licensed third-party SDKs for local development and testing.

