> Current status (2026-10-05): separation is implemented and the compiled SDK game is USER accepted in Unity and Windows. Read current-state.md and motion-sdk-integration.md. The original 2026-10-02 direction below is retained as chronology, not an instruction to restart the audit.

# Golden Needle — Motion Engine Product Separation Direction

Recorded: 2026-10-02

Status: **STRATEGIC DIRECTION APPROVED; TECHNICAL SEPARATION NOT YET IMPLEMENTED**

Repository at documentation decision point:

- `gameplay/foundation`: `07f1e7fa0ca0046e9a365b27bcb00a5e7ed8243a`
- `main`: `cfb953ac6dcf1df4ca5a252f38e48792b347241a`

This document records the next project direction. It intentionally preserves all earlier engineering documentation and development history.

## 1. Product distinction

The SIH product is **Mocap Adventure**, the Unity fitness game/application.

Mocap Adventure uses the Motion Engine as underlying motion-control technology. The intended future structure is that the Motion Engine becomes a separately maintained reusable software product and Mocap Adventure consumes it under a permanent licence.

The fact that Mocap Adventure uses the Motion Engine does not mean this documentation is attempting to erase the Motion Engine from the submitted technical solution. It records a product/ownership boundary between a reusable underlying component and an application that uses that component.

## 2. Truthful development chronology

The Motion Engine was **developed during the Golden Needle/Mocap Adventure project**.

It must not be represented as:

- technology that existed before the project when it did not;
- historical/pre-existing background IP merely for convenience;
- a separately packaged product at an earlier date than the repository history supports.

The intended chronology is:

```text
Motion Engine developed inside Golden Needle/Mocap Adventure
-> reusable value recognized
-> ownership/licensing direction documented
-> technical boundary audited
-> Motion Engine separated/productized
-> Mocap Adventure consumes separated engine under licence
```

Git history and historical documentation must be preserved.

## 3. Contribution and intended ownership record

The USER states that:

- they solely developed the Motion Engine portion of the project;
- teammates did not contribute to the Motion Engine;
- development was AI-assisted, including use of ChatGPT;
- Mocap Adventure remains the SIH team product;
- the Motion Engine is intended to become an independent reusable product under the USER's individual ownership.

This repository record documents the USER-directed project structure. Team acknowledgement, licensing instruments, and any required recognition by SIH/funding parties are separate legal/organizational steps and should not be inferred to have been completed merely because this document exists.

## 4. Intended licensing relationship

The intended relationship is:

```text
USER-owned reusable Motion Engine
        |
        +-- permanent licence --> Mocap Adventure
        |
        +-- separate licence --> rehabilitation application
        |
        +-- possible future licences --> other products
```

Exact licence terms—including scope, distribution, modification, sublicensing, source access, improvements, maintenance, and commercialization rights—have **not** been fixed by this engineering document and should be formalized separately.

Any future SIH or external-funding agreement should be reviewed against this intended boundary rather than silently assuming that use of the Motion Engine transfers ownership of the underlying reusable technology.

## 5. Third-party boundary

The project depends on third-party technology including Unity, MediaPipe/Homuler, OpenVINO, model assets, and other packages/assets.

Product separation must distinguish:

1. original Motion Engine code/architecture intended to be retained;
2. third-party components used as dependencies;
3. Mocap Adventure-specific code/assets;
4. mixed adapters/integration code whose final placement requires technical review.

No ownership claim over third-party components is created by this document. Existing licence, attribution, redistribution, and provenance obligations remain applicable.

## 6. Current technical state

At this checkpoint the Motion Engine and Mocap Adventure remain integrated in the existing Unity repository.

No technical separation has yet occurred. In particular, this documentation change does **not**:

- create a new repository;
- move/delete source files;
- create a new DLL or Unity package;
- change namespaces or assembly definitions;
- change provider/runtime behavior;
- change the accepted Motion Engine algorithms;
- change scenes or gameplay;
- change third-party dependencies;
- merge anything to `main`.

The existing integrated code remains the authority until a later USER-approved implementation changes it.

## 7. Next engineering phase

The first engineering action is a **read-only separation boundary audit**.

The future Orchestrator should classify the repository into:

- reusable Motion Engine;
- Mocap Adventure/game-specific;
- third-party;
- mixed/needs-decision.

The audit should then present viable separation approaches and their trade-offs, including packaging/repository structure, Unity coupling, provider/native dependency handling, versioning, licensing distribution, Mocap Adventure integration, and regression/QA strategy.

The USER will discuss and approve an approach before implementation begins.

## 8. Non-goals of the initial separation phase

Do not use productization as an excuse to:

- reopen accepted pose, calibration, retargeting, OpenVINO, or locomotion algorithms;
- remove historical documents or rewrite commits;
- claim the Motion Engine predates the project;
- silently absorb Mocap Adventure-specific work into the engine;
- claim ownership of third-party software/models/assets;
- make legal conclusions from architecture alone;
- change Mocap Adventure behavior before a separation plan is approved.

## 9. Preservation rule

All past technical documentation remains useful historical evidence even when later status notes supersede an old "next action."

When a later document conflicts only on **project priority/status**, use the newest dated status. When historical documents describe what was implemented, tested, rejected, or accepted at a particular time, preserve that record rather than deleting it.
