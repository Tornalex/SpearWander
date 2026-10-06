# Project Spear — AI Development Guidelines

## Project

Project Spear is a 2D Unity action-platformer.

Core gameplay:

* The player uses a spear for both combat and movement.
* Spear mechanics include Throw, Recall, Dropped, Embedded and related states.
* Movement abilities include Jump, Dash, Pogo and Rope.
* The game uses rooms with transitions and a camera following the player.
* Enemies and a boss use the game's damage system.
* Essence is used for healing and related interactions.
* UI, dialogue/textbox and audio systems are also present.

## Critical Rule

Preserve existing gameplay behavior unless the user explicitly asks to change it.

Do not:

* Rewrite working systems without a specific reason.
* Change gameplay values, timings or feel unintentionally.
* Remove existing functionality.
* Perform unrelated cleanup while solving a task.
* Refactor unrelated systems.
* Change serialized Unity fields or references unnecessarily.
* Rename public/serialized fields without checking their Unity Inspector impact.

When a task can be solved with a small targeted change, prefer the small targeted change.

## Before Editing

Before modifying code:

1. Identify the relevant files and systems.
2. Understand the existing implementation.
3. Check how the relevant code interacts with other systems.
4. Make the smallest appropriate change.

Do not modify files simply because they could theoretically be improved.

## Unity

Be careful with:

* Serialized fields
* Inspector references
* Prefab references
* Unity lifecycle methods
* Coroutines
* Physics callbacks
* Animator references
* Script execution order
* Scene references
* Singleton/static state

Do not assume that a reference is unused merely because it is not assigned in code. It may be assigned through the Unity Inspector.

## Code Quality

Prefer:

* Clear and readable code
* Small focused methods
* Single responsibilities where practical
* Existing project conventions
* Explicit state handling
* Minimal duplication

Avoid:

* Premature abstractions
* Unnecessary design patterns
* Overengineering
* Large rewrites for small problems
* Adding dependencies without a concrete reason

Do not introduce an abstraction unless it solves an identified problem.

## Refactoring

When explicitly asked to refactor:

* Preserve gameplay behavior.
* Preserve serialized Unity references whenever possible.
* Preserve public APIs unless changing them is necessary.
* Refactor one logical system at a time.
* Do not combine unrelated refactors.
* Explain any potentially breaking change before making it.

## Debugging

When fixing a bug:

1. Identify the actual cause before changing code.
2. Prefer fixing the root cause rather than adding a workaround.
3. Avoid changing unrelated behavior.
4. Check for obvious side effects in connected systems.

If the cause is uncertain, investigate before making a large change.

## Verification

After modifying code:

* Check the affected scripts for obvious compile errors.
* Check references to changed methods, fields and types.
* Verify that the change is consistent with the existing architecture.
* If Unity runtime testing is required, state clearly what needs to be tested in Unity.

Do not claim that something was tested in Unity unless it was actually tested.

## Communication

Keep responses concise.

After completing a task, report only:

1. What changed.
2. Files changed.
3. Verification performed.
4. Any remaining issue or required Unity test.

Do not provide long explanations unless requested.

## Scope

Work only on the task requested by the user.

If you notice unrelated problems:

* Do not fix them automatically.
* Mention them briefly at the end if they are relevant.

Stop when the requested task is correctly implemented and verified.

## Git

Do not create, amend or push Git commits.
The user manages Git commits manually.
