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
- Standalone Windows 11 x64, Linux x64, and macOS (Apple Silicon and Intel) applications are the initial targets, built with Godot and C#. Resizing keeps the complete office image and its hotspots aligned. Strategic screens have no header bar; the date shows on the office calendar and the current location on a strip along the bottom of the wall chart.
- Every office hotspot (wall chart, calendar, safe, filing cabinet, rolodex, phone, hangar porthole and jacket) responds only within a polygon traced around its painted object, and hover or keyboard focus outlines that shape in gold. The jacket on the wall opens the main menu.
- The painted wall chart is the clickable wall map, with no persistent marker. Click it, or focus it with Tab and press Enter/Space. Hover or keyboard focus outlines the painted frame in gold, with the tooltip “Open star map”. Its miniature always shows the complete reference chart, independently of the opened map’s zoom and pan.
- The wall map opens a separate screen displaying the official 3025 map of the entire Inner Sphere, with pan, zoom, and “Back to office”.
- The default window is 1440 × 900, with a 900 × 500 minimum. The interface is laid out at a 1440 × 900 base and scales up uniformly with larger windows (by the smaller of the width and height ratios), with the longer direction gaining extra layout space rather than letterboxing. It never scales below full size, so smaller windows keep full-size text and use their compact layouts. 3D views render at the window’s pixel density, so they stay sharp.
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
- Mission offers are generated around the DropShip and replaced from the oldest; see [Mission offers](#mission-offers). Offers disclose location, travel estimate and deadline before acceptance.
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
- The painted calendar display reads “bills due in XX days” (or “day” when one remains) and stays synchronized with campaign time. Hover the calendar for its date and countdown in small windows.
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

The existing right porthole looks into an enclosed repair bay. Clicking it opens a dedicated hangar interface with a new painted background, a large selected BattleMech, roster selection along the bottom, classic specifications and pilot assignment controls. Assignment is one-to-one; transfers and swaps require explicit confirmation. Establishing a company assigns the drafted warriors to the mechs in their corresponding columns: Warriors I to Light, Warriors II to Medium, Warriors III to Heavy, and Veterans to Assault. Later hangar changes and established saves retain their assignments, including empty seats. This slice provides viewing and assignment, without sales, dismissal, renaming, repair controls or combat resolution. Choose a living warrior to assign, or unassign the current pilot. Drafted mechs start intact with full ammunition.

## Hangar blueprint and storage (confirmed October 31, 2026)

The hangar is laid out for 1440 × 900 and leads up to refitting, which is not yet available:

- **Inventory and Market** tabs fill the left column. **Inventory** is storage: spare equipment stacked and grouped as Energy, Ballistic, Missile, Ammo and Equipment, each line showing the spare's name and count, drawn as a slot (filled rectangle with an outline) in the colour the item takes in a location panel. **Market** lists, in the same groups and style, the systems for sale where the DropShip is in orbit, each with its price, beneath the company's cash and the total of pending purchases. For now every system storage can hold (every weapon and ammunition in the catalog, both heat sinks and every jump jet class) is for sale in every system at a nominal 1 C-bill. In transit there is no market: the tab says "No market while in transit".
- **The selected mech** fills the centre as a blueprint: its real 3D model, front-on, tinted cyan over a darkened bay and a faint grid. Locations that have lost armor or structure tint amber; destroyed locations stay hidden. Right-drag spins it at half a degree per pixel; on release it eases back to front-on over 0.3 seconds. A right click that moves no more than five pixels between press and release still returns to the office.
- **One panel per location** sits roughly over that body part, its name centred across the top, with the mech's right side on the viewer's left. Each has a 12-pixel bar of installed armor against the most the location can carry at the mech's tonnage (twice its structure, or nine stock points for the head), with lost armor shaded inside the installed section; the torsos have a second bar beneath it for rear armor, against the same limit. Directly beneath those is a 12-pixel grey bar of current against maximum structure. Armor and rear armor as current/installed and structure as current/maximum are written over their bars, falling back to AR / RR / IS in narrow panels; bar colours are dimmed so the text stays legible. Each armor bar ends in − and + buttons (hidden on a destroyed location) that take off or put on one stock point of that armor (front or rear), or ten with Shift held, as a pending loadout change; front and rear together stay between zero and the location's limit. Added armor arrives intact; armor taken off leaves the current armor at most the new installed amount, so existing damage is kept for repair. Below are all of the location's critical slots (six for the head and legs, twelve elsewhere), coloured by kind; weapons take their hardpoint class's colour, the same as a free mount of that class. Each item is one block, however many slots it takes; every heat sink and jump jet is its own item, even when several share a location. Damaged items show amber; destroyed items show red and crossed out. Hovering a slot names the item with its mass, slots and condition. Space hides or shows all the location panels to give a clear view of the model, even while a button has focus; they are shown again whenever the hangar opens. Tab cycles the mech preview between standing still, walking and running, then back to still, even while a button has focus.
- **The summary** fills the right column, without section headings: the MechWarrior (portrait, name, callsign, skills, and the assignment controls), then the mech's name with its current / maximum tonnage beneath in the same large type (amber while below maximum), its class, role, engine and status; walk, run and jump speeds in km/h (movement points × the combat movement rate of 2.7 m/s, so 9.72 km/h per point); then three 30-pixel bars, each with its value written over it and its colours dimmed so the text stays legible. **Armor** and **Structure** read current / stock on a bar whose full width is the most the tonnage allows (the sum of the location armor limits; structure is fixed by tonnage, so its stock is its maximum). The remaining amount is one solid colour from a red → orange → yellow → green scale at the fraction remaining, and the amount still to repair up to stock is grey. **Cooling** reads as a percentage: heat sunk per second (working sinks × dissipation per sink / 10 s) over the most heat per second the mech can sustain, every usable weapon firing as soon as its cooldown allows plus running heat (2 per 10 s; jumping adds no heat). It fills on the same colour scale, full at 100%, and shows ⚠ at its right end when cooling exceeds 100%. Hovering it shows both rates per second.
- **The mech buttons** run along the bottom, with **Reset**, **Apply**, **Back to office** and **Simulate Deployment** at their right. Below 1100 pixels wide the columns narrow, the location panels form three columns and the blueprint scrolls.

Critical slots follow the stock record-sheet layout: the catalog's fixed systems first, then weapons, heat sinks and jump jets, then ammunition. Stock weapon slot sizes and masses come from the standard tables. A stock design that cannot fit a large weapon into one location splits it into the neighbouring location, as the King Crab's AC/20s do.

A new company starts with one spare of every weapon, ammunition bin, heat sink (including those held inside engines) and jump jet mounted across its mechs, so every mech could be fully re-armed and re-sunk. Jump jets are stored by mass class. Engines, gyros, actuators and cockpit systems have no spares, and armor is never stored. Storage is stocked once, when the company is established; a save from before storage existed is stocked once from its mechs when loaded. Later mechs do not add spares.

## Hangar loadouts (confirmed November 1, 2026; installation confirmed November 1, 2026)

Each owned mech has its own **loadout**: its stock design less the items removed in the hangar, plus the spares installed there from storage, with each location's installed armor as changed in the hangar. Armor weighs a ton per 16 stock points, counted from the stock amount; it is never stored. Combat, heat, jump movement, readiness and the hangar summary all use it, and it is saved with the company. Saves from before heat sinks and jump jets were separate items give each one the condition of its old group.

- **Installation.** Drag a storage line onto a location panel to install one spare there; storage lines say "Drag onto a location to install", and a line at ×0 cannot be dragged. Installing is mouse only. The item takes the first empty slots, after everything already in the location: empty slots are always the bottom-most, so removing an item moves the items below it up. Items are never split across locations (stock designs that split a weapon keep it, but the split cannot be recreated). Installed items arrive operational and bins full, and from then on follow the same rules as stock items: they take critical hits, can be marked ⚠, count for operational status and ammunition pairing, and right-click takes them out again (back to storage, or scrapped if destroyed). Market lines are dragged the same way ("Drag onto a location to buy and install"), whatever storage holds: the item is installed as a **pending purchase**, its tooltip saying "Pending purchase · 1 C-bill". Pending purchases never touch storage counts or cash; taking one out cancels it. A drop is refused when:
  - the location's structure is gone ("Location destroyed"), or it has too few empty slots ("Needs N free slots");
  - a weapon has no free mount of its class (energy, ballistic or missile) in that location (see **Weapon mounts** below). Installed weapons face forward. A weapon shows on the 3D preview only where its chassis has a visual for it on that hardpoint (see [mech models](mech-models.md#weapon-hardpoints)); otherwise it fights without one;
  - a heat sink is not the mech's type, single or double;
  - a jump jet is going in the head or an arm, is not of the mech's mass class, or would raise jump MP above walking MP.
  Ammunition and heat sinks otherwise go anywhere with room. While a spare is dragged, the location panels are shown even if Space hid them, and each panel's empty slots are tinted green where it can go and red where it cannot, with the reason written over them. Escape or right-click during a drag cancels only the drag; releasing anywhere but a valid panel does nothing.
- **Weapon mounts.** A location's hardpoints are critical slots of their class; the rest of its slots are regular. Each weapon takes one mount of its class while one is left, whatever its size and wherever it was dropped. Stock weapons beyond the hardpoints, or in a location without one, are kept but take no mount, so once removed they can only go back where a mount is free. A non-weapon dropped on a mount slot covers that mount, even with regular slots free. Every other slot, including a weapon's extra slots, fixed systems and stock items, fills regular slots first and then covers free mounts from the bottom up: Missile, then Energy, then Ballistic. A covered mount takes no weapon until the item covering it is removed. Free mounts are the location's hardpoints less those its weapons take and those other items cover. The mount an installed item was dropped on is saved with the loadout; all other coverage is worked out afresh from the loadout, so removing an item that took a regular slot can uncover a mount. The location panel labels its first empty slots with its free mounts, Ballistic, Energy then Missile, in pale yellow `#e6dc82`, cyan-green `#5fd3b0` and orange-red `#e07a5a`, with regular empty slots as "—" below them; destroyed locations show none. Hovering an empty slot of a location with free mounts names its mount (or "Empty slot"), lists the location's free mounts and says to drag a weapon from storage. An item covering mounts has a 3-pixel bar in each covered mount's colour down its left edge, one row tall per mount, and a "Covering a … mount" tooltip line. The labels stay visible under the drag tint.
- **Removal.** Right-click a weapon, ammunition bin, heat sink or jump jet in a location panel to remove it, freeing all its slots (both halves of a weapon split across two locations). Engines, gyros, actuators, cockpit systems and engine-held heat sinks cannot be removed. A right click on any slot never navigates back; elsewhere it still does. The slot tooltip says "Right-click to remove", or "Right-click to scrap" for a destroyed item.
- **Effects.** Current tonnage is the rated tonnage less the mass of removed items plus that of installed ones; stock designs are taken to be exactly at their rating, and every heat sink weighs 1 t. Each removed heat sink removes one sink's cooling, and each removed jump jet one jump MP; each installed one adds them back.
- **Over tonnage.** A loadout may go over the rated tonnage while it is pending. While the selected mech is over, a yellow ⚠ **Over Tonnage** shows above the blueprint, the tonnage figure turns red, and Apply is disabled, its tooltip saying "Over tonnage by X t". Simulated deployments have no tonnage restriction. A mech left with no usable weapon is not operational.
- **Storage.** Removed items join storage, damaged ones as ordinary spares; destroyed items are scrapped. Installed items leave it. A bin is stored as a full bin whatever it held. Ammunition is not tracked between battles: every installed bin refills for free after each battle (applied once missions report results), so hangar tooltips show no shot counts.
- **Pending changes.** Removals, installations and armor changes stay pending, per mech, until applied. Several mechs may have pending changes at once, and their buttons along the bottom are marked with a gold •. The hangar shows pending loadouts throughout: panels, tonnage, summary, the 3D preview's weapon mounts and storage counts. Storage counts include items taken out and exclude items installed in every mech's pending loadout, so two mechs cannot both claim the last spare. Taking out a pending installed item returns it to storage at once. **Apply** commits the selected mech's pending changes to the company and pays for its pending purchases (the company then has unsaved changes; nothing is written to disk); its tooltip says "Buys N items · X C-bills". Apply is disabled, saying "Not enough C-bills to buy…", while the selected mech's purchases cost more than the company's cash. **Reset** discards the changes and cancels their purchases at no cost. Both are disabled unless the selected mech has pending changes.
- **Suspect items.** An installed weapon with no bin of its ammunition anywhere on the mech, and a bin that no installed weapon fires, show a yellow ⚠ after their name, whatever the location or damage, with a tooltip line saying why. Applying a loadout with ⚠ items asks "This loadout has items marked ⚠. Apply anyway?".
- **Leaving.** Leaving the hangar for the office (Back to office, Escape or right-click) with pending changes on any mech warns how many mechs have unapplied changes, offering **Stay in hangar** or **Abandon changes and leave**; abandoning cancels pending purchases. Viewing MechWarriors does not warn. **Simulate Deployment** does not warn either: the simulation fields the pending loadouts, and they are still pending on return.

Repair rules, recorded now and applied when refitting arrives: restoring armor costs C-bills; damaged systems can be repaired for C-bills; a destroyed system (every one of its critical slots hit) must be replaced outright.

Roster art includes a maintenance-bay background, original warrior portraits, and 3D model previews for draft candidates. Unit detail includes classic stock 3025 loadouts, front/rear armor and internal structure by location, component condition, weapons and ammunition, movement and heat information.

A new company enters the office and immediately opens the shared initial draft before normal operations. Eight pools of three candidates are simultaneously visible: Light, Medium, Heavy and Assault BattleMechs, plus three regular MechWarrior pools and one more experienced pool. The warrior catalog contains balanced, Gunnery-focused, Piloting-focused and Sensors-focused recruits; candidates are randomly sampled within their experience class. Individual candidate records can be inspected without selecting. There are no rounds or rerolls. Each new draft samples three distinct candidates per weight class from all 49 chassis and draws pilots from a roster of 18 regulars and six veterans. A random company name and one randomly selected candidate per pool are filled in, so the company can be established immediately. All eight pools remain visible at 900 × 500. Click a candidate to change a selection; use **…** to inspect the full record without selecting. Escape from a record returns to drafting.

The company name and all eight choices remain editable until **Establish Company** commits the complete roster. Escape from an unfinished draft asks to abandon it and return to the main menu. Unfinished drafts cannot be saved, and established drafts cannot reopen. Existing finance-only saves require this same draft while retaining their date, cash, obligations and scheduled events; loading or drafting does not overwrite the manual save.

Opening roster interfaces pauses time. Escape from the hangar, map, or office rolodex returns to the office; Escape in the office opens the main menu. Clicking the jacket on the office wall opens it immediately. Escape or right-click in the main menu acts as **Resume** when it is available, and otherwise does nothing. The main menu offers **Resume** when an established, solvent company remains in memory, except while a battle is in progress. Resume restores the previous strategic screen, panels, selections and focus, preserving unsaved company state while leaving time paused. It retains the session only while the app is open and does not write a save. Starting or loading another company asks before replacing a live company only when it has unsaved changes, as described under Calendar and persistence. The original single-slot manual-save policy remains in force.


## Star travel (confirmed October 25, 2026)

Click a named system or select a destination while in orbit to plot a blue candidate route. This does not replace an embarked green journey. Embark commits the candidate as the active journey but leaves the company in orbit until the next day. Cancel Journey clears only the embarked journey; a candidate can be ignored and remains available while in orbit.

Time advances only in the office. Advancing a day departs orbit, clears any unembarked candidate, and counts the first transit day. Plotting, embarking, and cancellation are unavailable partway through a jump. Arrival is a calendar-stopping event at every system, including intermediate stops. The company spends that date in orbit and automatically continues its embarked journey when time advances to the next day. Final arrival completes the journey.

A 30-light-year jump takes five days; shorter jumps scale linearly and round up independently to whole days, with a minimum of one day. Embarking on day 10 for a 30-light-year jump produces arrival on day 15. Every transit day costs 2,000 C-bills, including a completed day on a quarterly boundary. Orbit incurs no transit charge. Transit costs join the current calendar-quarter bill and use the same full-payment, one-deferral, and bankruptcy rules. Same-day arrival notifications wait for payment or deferral.

Persist location, candidate and embarked routes, current jump progress, accrued transit days, and the frozen transit component of a pending bill. Use the source map's coordinates and scale; duplicated system names retain distinct stable identities. Unreachable destinations cannot be embarked.

Find a system with the destination search as an alternative to clicking it. Drag to pan; scroll or use the + / − buttons to zoom. With the map focused, + / − zoom, arrow keys pan, and Home fits the complete chart. 100% zoom fits the whole chart; maximum zoom is 1200%. Resizing preserves the image aspect ratio, hotspot alignment, and map location. Return with “Back to office”, right-click, or Escape; map position survives navigation, and keyboard focus returns to the wall chart.


## Back navigation and DropShip visibility

Outside tactical combat, right-click closes the topmost popup, confirmation, or subscreen and returns one level toward the office. Tactical combat uses right-click for movement and attack commands, with Escape opening the combat menu. Reuse the existing cancel/back action, restore focus, and consume both press and release. Right-click in the unobstructed office stops time and opens the main menu; in the main menu it resumes the suspended screen, if any. Draft abandonment and bankruptcy retain their existing confirmation and dismissal rules. Personnel opened from the hangar returns to the same hangar selection; its visible Back action and Escape follow that parent too.

A fixed-size gold DropShip silhouette, dark backing, halo, and permanent location label identify the current orbit or interpolated position in transit on the chart. Offscreen position is indicated by a clickable direction arrow. The arrow and Find DropShip button center the current position at the existing zoom, within map bounds, without changing candidate or embarked journeys. Preserve the chosen camera view when reopening the chart. Keep the marker label and locator clear of the zoom controls and usable at 900 × 500.


## Mission offers

A silent flashing light on the office phone indicates new mission offers. Opening it marks all available offers as seen. Browse briefings beside the offer list, accept one mission, or inspect the current mission while browsing others. Accepting returns to the office; abandon with confirmation before accepting another. Mission selection and seen status use the same manual save slot. Deployment follows starmap movement. Each offer is a contract with a single mission.

**The mission offer board** (confirmed November 3, 2026) holds 10 offers, listed oldest first. A new company starts with 10 generated offers. On every day advanced, including in transit and with a mission accepted, there is a 50% chance that the oldest offer is removed (at most one per day); the board is then refilled to 10. Offers have no expiry date, only the date they were posted. Accepting an offer takes it off the board, which holds 9 until the next day's refill; the daily roll never removes the accepted mission. Abandoning a mission removes it for good. New offers are posted that day, unseen, and flash the phone; offers never stop the calendar.

**Each generated offer** has:

- a placeholder name and briefing, and the objective "Defeat the defending force". There is no employer yet.
- a location: a random named system within 3 jumps of the DropShip when the offer is posted, counted from the system being jumped to while in transit. It never changes.
- its opposition: 2–4 BattleMechs, each a random stock catalog mech of a weight class drawn Light 40 / Medium 35 / Heavy 18 / Assault 7, piloted by an unnamed pilot of rank Green 20 / Regular 50 / Veteran 22 / Elite 8. The briefing shows exactly the force that will be fielded.
- a completion deadline of travel days to the location plus 14, shown as "N days after acceptance" and fixed as a date on acceptance. The travel estimate finishes the current jump first.
- fixed rewards, with no negotiation yet. Difficulty is the enemy tonnage, each mech weighted by its pilot's rank (Green 0.8, Regular 1.0, Veteran 1.25, Elite 1.5). The budget is difficulty × 4,000 C-bills, ±20%, split at random: at least 40% to payment, the rest between salvage and the insurance limit. **Payment** is in C-bills. **Salvage** is a number of picks from wrecks and equipment after the battle: its share ÷ 100,000 C-bills, rounded, from 0 to 6. **Insurance** pays the lesser of the deployed mechs' repair costs, with destroyed mechs at their replacement value, and the offer's limit (a negotiated maximum once negotiation exists). Payment and the insurance limit are rounded to 1,000 C-bills.

Rewards are only displayed for now: payment, salvage and insurance are settled once mission results reach the campaign. The deadline is recorded but missing it has no effect yet. The numbers live in `MissionTuning`.

Generation uses a seeded random number generator whose state is saved, so reloading and advancing repeats the same offers and removals. Saves before version 6 receive a freshly generated board and lose their accepted mission.

## Initial combat design (confirmed October 25, 2026)

The [combat design](combat-design.md) records the initial pausable real-time combat slice. A **Simulated Deployment** button in the hangar offers a randomized enemy or mirror match before force selection and terrain setup. Randomized enemies use fresh legal drafts to match the selected count and weight categories, including repeated categories; mirror matches copy mech types and pilot skills with both sides pristine. Simulation reports outcomes without advancing time or modifying the campaign; campaign mission integration is deferred. The initial design was confirmed October 25, 2026, and these simulation choices October 29, 2026. Numerical defaults remain authoring and tuning work.
