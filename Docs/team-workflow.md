# Team workflow

Mocap Adventure product development uses GoldenNeedle, normally gameplay/foundation. Inspect Git status and remote HEAD before changes. GitHub Desktop remains the USER's normal Git tool; agents may perform mutations explicitly authorized for their task. Main/PR merges require explicit approval. Do not reset/rebase/force-push or rewrite shared history.

The owner maintains HD Motion Engine source and its Unity Lab privately. Team members use the versioned compiled SDK already bundled with the product. Do not add private source repository URLs/credentials, embedded engine/vendor implementations or old native build experiments to the game.

Keep scene/content ownership explicit. Preserve .meta files and serialized references; use Unity Editor APIs for scenes/prefabs. Avoid unrelated authored asset/settings changes. Never commit Unity caches, recovery scenes, generated model payloads, build output or debug symbols. Run python Tools/verify_motion_sdk_boundary.py before publishing boundary changes.

Engine updates come through deliberate SDK version changes after private Lab verification. Product testing covers Calibration/Hub/body/locomotion/speech/activity behavior. Full Windows builds and similarly long-running checks belong to the USER. Record actual evidence and distinguish implementation, static checks, test assertions and USER acceptance.
