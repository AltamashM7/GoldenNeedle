# Planned game flow

Status: **PLANNED; NOT YET IMPLEMENTED.**

The intended player experience is:

```text
Application launch
    -> initial screen with one primary Begin action
    -> camera / Cinemachine-style menu reveal
    -> Start Fitness or Exit
```

- **Exit** closes the application.
- **Start Fitness** begins a character-led introduction.
- The character breaks the fourth wall and explains that the user is about to control them.
- The webcam is activated.
- Calibration occurs, preferably disguised as character interaction rather than presented as an engineering/debug screen.

An example calibration interaction is for the character to ask the user to stand naturally, then raise or spread their arms. The system would derive the neutral orientation, scale, and rest-offset information needed for the avatar.

After calibration:

```text
control transfers to user
    -> player/avatar exists in Hub
    -> user physically controls avatar
    -> Hub offers fitness-course choices
    -> player chooses a course
    -> course runs
    -> course completes
    -> player returns to Hub
```

The exact Hub interaction, course-selection interaction, locomotion mechanics, and presentation details remain **OPEN / MAY CHANGE** until prototypes provide evidence. This document does not imply that any of these runtime states currently exist.
