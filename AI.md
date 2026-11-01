# Instructions for AI assistants

- **Documentation:** record game behavior and rules in the matching `docs/` file: strategy screens and campaign rules in `docs/design.md`, combat behavior in `docs/combat-design.md`, combat numbers in `docs/combat-tuning.md`, model and paperdoll details in `docs/mech-models.md`, test groups and when to run them in `docs/testing.md`. `README.md` only gains setup steps, build/test/export commands, and new inputs in its Controls table.
- **Tests:** after changes, run only the test groups covering the changed files: `scripts/mech test <group...>` (the map from files to groups is in `docs/testing.md`). Run the full `scripts/mech test` only when shared code listed there changes or the user asks. Put new tests in the group for the screen they exercise, and give a new screen its own group. Groups must not depend on each other's state.
- **Terminology:** use the project terms defined in `CONTEXT.md`.
