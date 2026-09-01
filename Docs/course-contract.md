# Course contract direction

Status: **CONCEPTUAL / PLANNED; NOT YET IMPLEMENTED.** Do not create a final C# interface from this document alone.

## Conceptual lifecycle

```text
Available -> Start -> Active -> Complete -> ReturnToHub
```

Courses must also support an **Abort / Exit** path from an appropriate state. The final lifecycle API, event model, and persistence behavior remain **OPEN / MAY CHANGE**.

## Eventual course concepts

A fitness course will eventually need concepts equivalent to:

- identity and display name;
- scene or content reference;
- optional difficulty/configuration;
- locomotion requirements or profile;
- start point;
- completion condition;
- result information.

These are domain concepts, not frozen API names or serialized field requirements.

## Ownership and boundaries

Each course should primarily own its own content directory and scene. Once the final project structure is justified, a course developer should work within that course's owned area, for example `Assets/.../Courses/<CourseName>/`, without forcing a speculative directory into the project now.

A course must not need direct access to MediaPipe or any pose-provider-specific type. It should interact with stable Player and Course System abstractions. The Course System owns lifecycle coordination; individual courses own their environment, challenge content, and course-specific presentation.
