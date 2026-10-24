# Agreed direction

- Use the actual BattleTech setting, factions, and BattleMechs.
- Business decisions create tactical consequences, and battles reshape the business.
- The mercenary force is the persistent protagonist; personnel may die or leave without ending its story.
- Company scale is a rough scope, not a strict roster or deployment cap.
- Combat is real time with pausing.
- Strategy and combat have agreed inputs and outputs and can be developed somewhat independently.
- First milestone: create a mercenary force, accept a contract, deploy to its mission, and apply mission results without playable combat.

# First interface slice

- Open in a painted captain’s office aboard a worn mercenary DropShip.
- Standalone Windows 11 x64, Linux x64, and macOS (Apple Silicon and Intel) applications are the initial targets, built with Godot and C#. Resizing keeps the complete office image and wall-map hotspot aligned.
- A subtle persistent marker identifies the clickable wall map. Hover and keyboard focus highlight it and reveal “Star map”.
- The wall map opens a separate screen displaying the official 3025 map of the entire Inner Sphere, with pan, zoom, and “Back to office”.
- The full reference chart is presentation artwork; the later playable travel network remains a separate authored set of systems and routes.
- This slice is an interface foundation; campaign simulation follows in the first strategy milestone.
- Acceptance: office → open map → pan and zoom → return to office, including keyboard navigation and resized windows.

# First strategy milestone

- Personal playable development is the immediate target.
- Setting: Late Succession Wars–Renaissance, 3025–3049, across the entire Inner Sphere and Periphery.
- Create and name a mercenary force by drafting four BattleMechs and four MechWarriors concurrently. Each individual selection offers three options; all eight pools are visible simultaneously, with no rounds. Options within each pool are of similar caliber. Any MechWarrior can pilot any owned mech; assignments persist until changed.
- Supply mission results through authored outcomes with editable details.
- Contracts and missions are distinct; initially offer single-mission contracts.
- Apply payment, mech damage and loss, repair costs and time, pilot casualties, and recurring expenses.
- Salvage and reputation are deferred beyond this first milestone.
- Advance an explicit calendar; allow one active contract initially.

# Campaign and travel

- Campaigns begin in 3025.
- Initially represent the Inner Sphere and Periphery with a sparse network of individual systems, expanding density later.
- Offer a small selection of starting systems and a fixed starting cash reserve.
- Hire transport with an upfront cost and elapsed travel time; transport vessel management is outside the first milestone.
- Present regional contract offers with fixed terms: payment, destination, deadline, and expected opposition. Negotiation is deferred.
- Allow asset sales for financial recovery. Recovery contracts are deferred. Losing the deployable force does not immediately end the campaign; players may sell assets, buy replacements, and hire pilots. Quarterly bankruptcy is the financial game-over condition.

# Force readiness and repairs

- Track armor and internal structure by location, plus damaged or destroyed installed components.
- Authorize complete repair orders per mech, quoted with a cost and completion date; repairs may proceed during travel.
- MechWarriors are available, injured until a recovery date, or dead, and have a small set of combat skills.
- Each deployed mech requires an available MechWarrior. Initially allow deployments of one to four operational mechs; this is a first-version limit, not a permanent company-scale constraint.

# Deployment and settlement

- Launch commits a snapshot of deployed mechs, condition, equipment, assigned MechWarriors and skills, mission objectives, and opposition.
- The interim authored-outcome tool consumes the same snapshot intended for combat.
- Combat reports battlefield facts; strategy calculates payment, contractual penalties, and repair quotes.
- Contracts have a mission completion deadline. Missing it fails the contract; payment occurs on settlement.
- MechWarrior salaries and fixed company overhead are paid automatically in quarterly increments on calendar boundaries, including during transit.
- Show projected cash at arrival before committing to travel.

# Markets and recovery

- Small local markets sell complete mechs and offer MechWarriors for hire at fixed prices. Upgrades and individual component trading are deferred.
- One repair slot operates initially; work continues during travel. Repair orders may be queued and payment is taken when work starts. Queued jobs can be cancelled freely; cancelling active work forfeits payment.
- Damaged mechs may deploy if cockpit, engine, mobility, and at least one usable weapon function.
- Objective outcome, recovered mech condition, unrecoverable mechs, and pilot casualties are separate results. Destroyed mechs recovered as wrecks may retain sale value.
- Failed or abandoned contracts pay no completion payment; any fixed failure penalty is disclosed before acceptance. Contracts may be abandoned explicitly.
- Missing a quarterly payment starts a three-month grace period ending at the next quarterly payment. At that deadline, both carried debt and the new quarter must be paid in full or bankruptcy dissolves the force. Collection is automatic, including during transit. Spending rules are unchanged during the grace period; no debt-specific restrictions apply.

# Calendar and persistence

- Missions have a declared duration, initially one day, visible before launch. Every outcome, including retreat, consumes that full day for preparation, deployment, combat, and recovery. Completion must occur by the deadline; authored outcomes cannot silently alter elapsed time.
- Regional contract offers expire and are periodically replaced. Offers visible from the current system disclose destination and travel estimate before acceptance.
- Support one campaign save, including travel, repairs, and committed deployments awaiting outcomes. A mission may be settled only once.

# Draft, travel, and interface

- Select an authored set of eight draft pools at campaign creation, without a reroll button.
- Initial MechWarrior skills are Gunnery and Piloting; progression and special abilities are deferred.
- Systems form a connected map with authored travel times and fares; charge for the chosen route. Explicit regions determine regional offer visibility.
- Damaged mechs and wrecks have guaranteed local sale at a displayed condition-adjusted price. Selling cancels associated repair orders under the cancellation rules.
- The first playable interface is a standalone Godot game written in C#, targeting Windows 11 x64, Linux x64, and macOS on Apple Silicon and Intel.

# Quarterly accounting

- Campaigns start January 1, 3025. First payment is due April 1, followed by July 1, October 1, and January 1.
- Salaries accrue for actual days employed, including obligations retained after death or dismissal. Fixed overhead also accrues daily. Collection occurs quarterly.
- If funds are insufficient, collect the available balance and carry only the shortfall as debt.
- Incoming cash during grace remains available for normal spending; arrears are collected at the next quarterly due date.
- Quarterly collection occurs at the start of the day, before mission settlement that day. At the end of grace, carried debt and the new quarter must both be covered or bankruptcy occurs.

# Time controls and initial content

- Time advances only through explicit player action, with next-day and next-event controls.
- Stop at arrivals, repair completions, mission results, contract deadlines, and quarterly payments.
- Begin with three starting locations, approximately twelve connected systems distributed across the intended geography, one authored draft configuration, and a small set of repeatable contract templates.

# Acceptance scenarios

1. Draft a force, accept a contract, pay for travel, deploy, and apply an authored costly victory.
2. Receive payment, queue repairs, sell or replace a lost asset, and accept another contract.
3. Save and resume the campaign with its commitments and consequences intact.
4. Miss a quarterly payment, then demonstrate recovery or bankruptcy at the next due date.

# Design review status

The user confirmed shared understanding of this design on October 24, 2026. The design interview is complete. Specific content and economic values remain authoring and tuning work.

# Content and tuning work

- Select specific starting systems and catalog entries appropriate to the setting.
- Tune starting cash, prices, salaries, overhead, repair costs and durations, travel fares, and contract rewards.
- Author draft options, route data, market inventory, mission templates, and outcome examples.
