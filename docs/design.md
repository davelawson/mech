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
- The painted wall chart is the clickable wall map, with no persistent marker. Click it, or focus it with Tab and press Enter/Space. Hover or keyboard focus outlines the painted frame in gold, with the tooltip “Open star map”. Its miniature always shows the complete reference chart, independently of the opened map’s zoom and pan.
- The wall map opens a separate screen displaying the official 3025 map of the entire Inner Sphere, with pan, zoom, and “Back to office”.
- The default window is 1440 × 900, with a 900 × 500 minimum.
- Every named system on the full reference chart participates in travel; geography and clickable labels are extracted from its pinned vector source.
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
- Use the named systems of the full 3025 chart, connected by jumps of at most 30 light-years.
- Start new companies in orbit at Galatea with 2,500,000 C-bills. Older saves without travel state also start at Galatea.
- Accrue transport costs at 2,000 C-bills per whole transit day and collect them with the quarterly company bill. Transport vessel management is outside the first milestone.
- Present regional contract offers with fixed terms: payment, destination, deadline, and expected opposition. Negotiation is deferred.
- Allow asset sales for financial recovery. Recovery contracts are deferred. Losing the deployable force does not immediately end the campaign; players may sell assets, buy replacements, and hire pilots. Quarterly bankruptcy is the financial game-over condition.

# Force readiness and repairs

- Track armor and internal structure by location, plus damaged or destroyed installed components.
- Authorize complete repair orders per mech, quoted with a cost and completion date; repairs may proceed during travel.
- MechWarriors are available, injured until a recovery date, or dead, and have a small set of combat skills.
- Each deployed mech requires an available MechWarrior. Initially allow deployments of one to four operational mechs; this is a first-version limit, not a permanent company-scale constraint.

# Deployment and settlement

- Every deployment goes through a force-selection panel before launch, including simulated deployments from the hangar.
- Launch commits a snapshot of deployed mechs, condition, equipment, assigned MechWarriors and skills, mission objectives, and opposition.
- The interim authored-outcome tool consumes the same snapshot intended for combat.
- Combat reports battlefield facts; strategy calculates payment, contractual penalties, and repair quotes.
- Contracts have a mission completion deadline. Missing it fails the contract; payment occurs on settlement.
- Quarterly obligations block time at calendar boundaries, including during transit, until explicitly paid or deferred. The company charge is 100,000 C-bills per quarter, plus accrued transit costs; personnel salaries and detailed operating costs follow later.
- Preview the selected route, transit days, and transit cost before embarking; no cash is collected upfront.

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
- Saves are stored in Godot’s per-user application-data directory as `campaign.json`, outside the installation folder.
- New Company, Load Game, Quit, and closing the window ask before discarding campaign data only when it differs from the last successful save or load. A newly established company is unsaved until its first save. Time, assignments, seen mission offers and plotted routes count as campaign changes; navigation and view selections do not. Prompts offer Cancel and the explicit discard action, including **Quit Without Saving**. Failed saves or loads preserve the current unsaved status.

# Draft, travel, and interface

- Generate eight draft pools at campaign creation, without a reroll button.
- Initial MechWarrior skills are Gunnery, Piloting and Sensors, each rated 2–6 with lower values better. Each starts at 6; every improvement costs one point. Regulars spend six points and veterans eight across all three skills. Allocations are fixed per named pilot; progression, special abilities and player allocation controls are deferred.
- Systems connect when separated by at most 30 light-years. Choose the fewest jumps, breaking ties by total distance. Each jump takes ceil(distance / 6) whole days. Explicit regions will determine regional offer visibility.
- Damaged mechs and wrecks have guaranteed local sale at a displayed condition-adjusted price. Selling cancels associated repair orders under the cancellation rules.
- The first playable interface is a standalone Godot game written in C#, targeting Windows 11 x64, Linux x64, and macOS on Apple Silicon and Intel.

# Quarterly accounting

- Campaigns start January 1, 3025. First payment is due April 1, followed by July 1, October 1, and January 1.
- Each quarterly bill includes the flat 100,000 C-bill company charge, accrued transit days at 2,000 C-bills each, and any carried bill. Detailed salaries and overhead are deferred.
- On a due date, stop advancing time, replace the calendar countdown with red “Payment Due”, and blink the safe. Clicking the safe opens the quarterly finances; while payment is pending, offer Pay Now and Pay Later.
- The safe’s digital display shows live cash, such as “₡ 2,500,000”. While payment is pending, that readout blinks until the bill is paid in full or Pay Later is confirmed. Hover the safe for the current balance in small windows.
- Pay Now collects the entire bill and clears debt. Disable it when funds are insufficient. Pay Later requires confirming a bankruptcy warning, preserves all cash, and carries the entire bill to the following quarter. It stops the safe blinking and resumes the countdown, which stays red until the bill is paid in full. Advancement remains stopped until the player requests it again.
- Incoming cash during grace remains available for normal spending. The next quarterly bill includes the deferred bill; a second deferral is unavailable.
- Accrue the completed transit day before calculating a boundary-day bill. Quarterly payment blocks presentation of other same-day events, including arrivals and mission settlement. Hold those events until payment is resolved. If funds cannot cover debt plus the new charge at the end of grace, show a bankruptcy dialog and return to the main menu when dismissed. Preserve the manual save.

# Time controls and initial content

- Time advances only through explicit player action, with next-day and next-event controls.
- The far-wall calendar displays the date and days to the next quarter. Clicking it advances one day; clicking its attached “Reminder...” post-it advances to the next event. Tick visibly through individual days at 100 ms per day.
- The painted calendar display reads “bills due in XX days” (or “day” when one remains) and stays synchronized with the header on both screens. Hover the calendar for its date and countdown in small windows.
- Any click stops advancement and activates the clicked object, except calendar and post-it clicks, which only stop advancement. Clicking empty space also stops advancement.
- Stop at arrivals, repair completions, mission results, contract deadlines, and quarterly payments.
- Quarterly payments always guarantee an upcoming event. Other event producers will connect to the scheduling foundation as their systems are implemented.
- Begin at Galatea, with all named chart systems available for route plotting, randomized draft candidates, and a small set of repeatable contract templates.

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
- Author route data, market inventory, mission templates, and outcome examples. The first draft configuration is authored in the roster slice.


## Roster and initial draft interface (confirmed October 24, 2026)

The desk rolodex displays the number of healthy MechWarriors ready for immediate deployment, including assigned warriors. Clicking it opens a panel over the office: one personnel card at a time, previous/next controls and a name index. Cards show portrait, name/callsign, Gunnery, Piloting, Sensors, skill budget, health/recovery date and assigned BattleMech. Injured warriors retain assignments. Deceased warriors are automatically unassigned and retained separately in memoriam. Flip individual cards or choose a name from the index. The desk count includes healthy assigned and unassigned warriors, excludes injured and deceased warriors, and updates after recovery.

The existing right porthole looks into an enclosed repair bay. Clicking it opens a dedicated hangar interface with a new painted background, a large selected BattleMech, roster selection along the bottom, classic specifications and pilot assignment controls. Assignment is one-to-one; transfers and swaps require explicit confirmation. Establishing a company assigns the drafted warriors to the mechs in their corresponding columns: Warriors I to Light, Warriors II to Medium, Warriors III to Heavy, and Veterans to Assault. Later hangar changes and established saves retain their assignments, including empty seats. This slice provides viewing and assignment, without sales, dismissal, renaming, repair controls or combat resolution. Selecting a mech along the bottom shows its illustration and Overview, Armor, Weapons and Systems tabs. Choose a living warrior to assign, or unassign the current pilot. Drafted mechs start intact with full ammunition. Chassis with articulated models offer an Idle/Walk/Run gait preview; see [mech models](mech-models.md).

Roster art includes a maintenance-bay background, original warrior portraits, and 3D model previews for draft candidates. Unit detail includes classic stock 3025 loadouts, front/rear armor and internal structure by location, component condition, weapons and ammunition, movement and heat information.

A new company enters the office and immediately opens the shared initial draft before normal operations. Eight pools of three candidates are simultaneously visible: Light, Medium, Heavy and Assault BattleMechs, plus three regular MechWarrior pools and one more experienced pool. The warrior catalog contains balanced, Gunnery-focused, Piloting-focused and Sensors-focused recruits; candidates are randomly sampled within their experience class. Individual candidate records can be inspected without selecting. There are no rounds or rerolls. Each new draft samples three distinct candidates per weight class from all 49 chassis and draws pilots from a roster of 18 regulars and six veterans. A random company name and one randomly selected candidate per pool are filled in, so the company can be established immediately. All eight pools remain visible at 900 × 500. Click a candidate to change a selection; use **…** to inspect the full record without selecting. Escape from a record returns to drafting.

The company name and all eight choices remain editable until **Establish Company** commits the complete roster. Escape from an unfinished draft asks to abandon it and return to the main menu. Unfinished drafts cannot be saved, and established drafts cannot reopen. Existing finance-only saves require this same draft while retaining their date, cash, obligations and scheduled events; loading or drafting does not overwrite the manual save.

Opening roster interfaces pauses time. Escape from the hangar, map, or office rolodex returns to the office; Escape in the office opens the main menu. The header’s **Main menu** button opens it immediately. The main menu offers **Resume** when an established, solvent company remains in memory, except while a battle is in progress. Resume restores the previous strategic screen, panels, selections and focus, preserving unsaved company state while leaving time paused. It retains the session only while the app is open and does not write a save. Starting or loading another company asks before replacing a live company only when it has unsaved changes, as described under Calendar and persistence. The original single-slot manual-save policy remains in force.


## Star travel (confirmed October 25, 2026)

Click a named system or select a destination while in orbit to plot a blue candidate route. This does not replace an embarked green journey. Embark commits the candidate as the active journey but leaves the company in orbit until the next day. Cancel Journey clears only the embarked journey; a candidate can be ignored and remains available while in orbit.

Time advances only in the office. Advancing a day departs orbit, clears any unembarked candidate, and counts the first transit day. Plotting, embarking, and cancellation are unavailable partway through a jump. Arrival is a calendar-stopping event at every system, including intermediate stops. The company spends that date in orbit and automatically continues its embarked journey when time advances to the next day. Final arrival completes the journey.

A 30-light-year jump takes five days; shorter jumps scale linearly and round up independently to whole days, with a minimum of one day. Embarking on day 10 for a 30-light-year jump produces arrival on day 15. Every transit day costs 2,000 C-bills, including a completed day on a quarterly boundary. Orbit incurs no transit charge. Transit costs join the current calendar-quarter bill and use the same full-payment, one-deferral, and bankruptcy rules. Same-day arrival notifications wait for payment or deferral.

Persist location, candidate and embarked routes, current jump progress, accrued transit days, and the frozen transit component of a pending bill. Use the source map's coordinates and scale; duplicated system names retain distinct stable identities. Unreachable destinations cannot be embarked.

Find a system with the destination search as an alternative to clicking it. Drag to pan; scroll or use the + / − buttons to zoom. With the map focused, + / − zoom, arrow keys pan, and Home fits the complete chart. 100% zoom fits the whole chart; maximum zoom is 1200%. Resizing preserves the image aspect ratio, hotspot alignment, and map location. Return with “Back to office”, right-click, or Escape; map position survives navigation, and keyboard focus returns to the wall chart.


## Back navigation and DropShip visibility

Outside tactical combat, right-click closes the topmost popup, confirmation, or subscreen and returns one level toward the office. Tactical combat uses right-click for movement and attack commands, with Escape opening the combat menu. Reuse the existing cancel/back action, restore focus, and consume both press and release. Right-click in the unobstructed office stops time and opens the main menu; it does not navigate from the main menu. Draft abandonment and bankruptcy retain their existing confirmation and dismissal rules. Personnel opened from the hangar returns to the same hangar selection and detail tab; its visible Back action and Escape follow that parent too.

A fixed-size gold DropShip silhouette, dark backing, halo, and permanent location label identify the current orbit or interpolated position in transit on the chart. Offscreen position is indicated by a clickable direction arrow. The arrow and Find DropShip button center the current position at the existing zoom, within map bounds, without changing candidate or embarked journeys. Preserve the chosen camera view when reopening the chart. Keep the marker label and locator clear of the zoom controls and usable at 900 × 500.


## Mission offers

A silent flashing light on the office phone indicates new mission offers. Opening it marks all available offers as seen. Browse briefings beside the offer list, accept one mission, or inspect the current mission while browsing others. Accepting returns to the office; abandon with confirmation before accepting another. Abandoned offers remain available and seen. Mission selection and seen status use the same manual save slot. Easy Peasy is the initial offer; its terms are provisional, with no payment or deadline effects yet. Deployment follows starmap movement.


## Initial combat design (confirmed October 25, 2026)

The [combat design](combat-design.md) records the initial pausable real-time combat slice. A **Simulated Deployment** button in the hangar offers a randomized enemy or mirror match before force selection and terrain setup. Randomized enemies use fresh legal drafts to match the selected count and weight categories, including repeated categories; mirror matches copy mech types and pilot skills with both sides pristine. Simulation reports outcomes without advancing time or modifying the campaign; campaign mission integration is deferred. The initial design was confirmed October 25, 2026, and these simulation choices October 29, 2026. Numerical defaults remain authoring and tuning work.
