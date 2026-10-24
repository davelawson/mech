# Agreed direction

- Use the actual BattleTech setting, factions, and BattleMechs.
- Business decisions create tactical consequences, and battles reshape the business.
- The mercenary force is the persistent protagonist; personnel may die or leave without ending its story.
- Company scale is a rough scope, not a strict roster or deployment cap.
- Combat is real time with pausing.
- Strategy and combat have agreed inputs and outputs and can be developed somewhat independently.
- First milestone: create a mercenary force, accept a contract, deploy to its mission, and apply mission results without playable combat.

# First interface slice

- Open at a main menu with New Company, Load Game, and Quit. New companies enter a painted captain’s office aboard a worn mercenary DropShip.
- Standalone Windows 11 x64, Linux x64, and macOS (Apple Silicon and Intel) applications are the initial targets, built with Godot and C#. Resizing keeps the complete office image and wall-map hotspot aligned.
- A subtle persistent marker identifies the clickable wall map. Hover and keyboard focus highlight it and reveal “Star map”.
- The wall map opens a separate screen displaying the official 3025 map of the entire Inner Sphere, with pan, zoom, and “Back to office”.
- The full reference chart is presentation artwork; the later playable travel network remains a separate authored set of systems and routes.
- The office now includes a digital wall calendar and attached “Reminder...” post-it, a company safe, and a filing cabinet. Other campaign simulation follows in the first strategy milestone.
- Acceptance includes office/map navigation, animated time controls, quarterly payment and bankruptcy flows, and manual save/load, including keyboard navigation and resized windows.

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
- Offer a small selection of starting systems. Start new companies with 2,500,000 C-bills.
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
- Quarterly obligations block time at calendar boundaries, including during transit, until explicitly paid or deferred. The current finance slice charges a fixed 100,000 C-bills per quarter; personnel salaries and detailed operating costs follow later.
- Show projected cash at arrival before committing to travel.

# Markets and recovery

- Small local markets sell complete mechs and offer MechWarriors for hire at fixed prices. Upgrades and individual component trading are deferred.
- One repair slot operates initially; work continues during travel. Repair orders may be queued and payment is taken when work starts. Queued jobs can be cancelled freely; cancelling active work forfeits payment.
- Damaged mechs may deploy if cockpit, engine, mobility, and at least one usable weapon function.
- Objective outcome, recovered mech condition, unrecoverable mechs, and pilot casualties are separate results. Destroyed mechs recovered as wrecks may retain sale value.
- Failed or abandoned contracts pay no completion payment; any fixed failure penalty is disclosed before acceptance. Contracts may be abandoned explicitly.
- Explicitly deferring a quarterly payment starts a three-month grace period ending at the next quarterly payment. Deferral preserves cash and carries the entire bill. At that deadline, both carried debt and the new quarter must be paid in full or bankruptcy dissolves the force. There are no partial payments or second deferrals. Spending rules are unchanged during grace.

# Calendar and persistence

- Missions have a declared duration, initially one day, visible before launch. Every outcome, including retreat, consumes that full day for preparation, deployment, combat, and recovery. Completion must occur by the deadline; authored outcomes cannot silently alter elapsed time.
- Regional contract offers expire and are periodically replaced. Offers visible from the current system disclose destination and travel estimate before acceptance.
- Support one manual campaign save, opened through the office filing cabinet, with confirmation before overwriting or loading over current progress. There is no autosave or automatic load at launch. The main menu can also load the save. Save the date, cash, pending payment, debt, next quarter, bankruptcy state, and scheduled events; include travel, repairs, and committed deployments as those systems are implemented. A mission may be settled only once.

# Draft, travel, and interface

- Select an authored set of eight draft pools at campaign creation, without a reroll button.
- Initial MechWarrior skills are Gunnery and Piloting; progression and special abilities are deferred.
- Systems form a connected map with authored travel times and fares; charge for the chosen route. Explicit regions determine regional offer visibility.
- Damaged mechs and wrecks have guaranteed local sale at a displayed condition-adjusted price. Selling cancels associated repair orders under the cancellation rules.
- The first playable interface is a standalone Godot game written in C#, targeting Windows 11 x64, Linux x64, and macOS on Apple Silicon and Intel.

# Quarterly accounting

- Campaigns start January 1, 3025. First payment is due April 1, followed by July 1, October 1, and January 1.
- The current finance model is one flat charge of 100,000 C-bills every quarter, with no monthly or daily accrual. Detailed salaries and overhead are deferred.
- On a due date, stop advancing time, replace the calendar countdown with red “Payment Due”, and blink the safe. Clicking the safe opens the quarterly finances; while payment is pending, offer Pay Now and Pay Later.
- Pay Now collects the entire bill and clears debt. Disable it when funds are insufficient. Pay Later requires confirming a bankruptcy warning, preserves all cash, and carries the entire bill to the following quarter. It stops the safe blinking and resumes the countdown, which stays red until the bill is paid in full. Advancement remains stopped until the player requests it again.
- Incoming cash during grace remains available for normal spending. The next quarterly bill includes the deferred bill; a second deferral is unavailable.
- Quarterly payment is evaluated at the start of the day, before other same-day events such as mission settlement. Hold those events until payment is resolved. If funds cannot cover debt plus the new charge at the end of grace, show a bankruptcy dialog and return to the main menu when dismissed. Preserve the manual save.

# Time controls and initial content

- Time advances only through explicit player action, with next-day and next-event controls.
- The far-wall calendar displays the date and days to the next quarter. Clicking it advances one day; clicking its attached “Reminder...” post-it advances to the next event. Tick visibly through individual days at 100 ms per day.
- Any click stops advancement and activates the clicked object, except calendar and post-it clicks, which only stop advancement. Clicking empty space also stops advancement.
- Stop at arrivals, repair completions, mission results, contract deadlines, and quarterly payments.
- Quarterly payments always guarantee an upcoming event. Other event producers will connect to the scheduling foundation as their systems are implemented.
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
- Tune prices, salaries, future detailed overhead, repair costs and durations, travel fares, and contract rewards. The current slice fixes starting cash at 2,500,000 C-bills and the quarterly charge at 100,000 C-bills.
- Author draft options, route data, market inventory, mission templates, and outcome examples.
