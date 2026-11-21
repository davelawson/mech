# Agreed direction

- Use the actual BattleTech setting, factions, and BattleMechs.
- Business decisions create tactical consequences, and battles reshape the business.
- The mercenary force is the persistent protagonist; personnel may die or leave without ending its story.
- Company scale is a rough scope, not a strict roster or deployment cap.
- Combat is real time with pausing.
- Strategy and combat have agreed inputs and outputs and can be developed somewhat independently.
- First milestone: create a mercenary force, accept a contract, deploy to its mission, and apply mission results without playable combat.

# First interface slice

- Open at a main menu with New Company, Load Game, and Quit, and the build version under the title (exported builds only; see README.md). New companies enter a painted captain’s office aboard a worn mercenary DropShip.
- Standalone Windows 11 x64, Linux x64, and macOS (Apple Silicon and Intel) applications are the initial targets, built with Godot and C#. Resizing keeps the complete office image and its hotspots aligned. Strategic screens have no header bar; the date shows on the office calendar and the current location on a strip along the bottom of the wall chart.
- Every office hotspot (wall chart, calendar, safe, filing cabinet, rolodex, phone, hangar porthole and jacket) responds only within a polygon traced around its painted object, and hover or keyboard focus outlines that shape in gold. The jacket on the wall opens the main menu.
- Sound: every button and hotspot ticks on hover and clicks when pressed. Committing actions confirm, and back actions (including right-click and Escape) sound a back tone. Menu, strategic screens and battles each have their own looping music track, crossfaded on change. Main menu **Audio** sets Master, Music, Effects and Interface volume and a mute, saved per machine outside every save. See [audio](audio.md).
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
- Salvage is settled after a won mission (see [Salvage and insurance](#salvage-and-insurance-confirmed-november-17-2026)). Reputation is described under [Reputation](#reputation-confirmed-november-5-2026).
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
- Authorize repair orders per mech (armor alone, or everything), quoted with a cost and duration; repairs may proceed during travel. See [Hangar repairs](#hangar-repairs-confirmed-november-13-2026).
- MechWarriors are available, injured until a recovery date, or dead, and have a small set of combat skills.
- Each deployed mech requires an available MechWarrior. Initially allow deployments of one to four operational mechs; this is a first-version limit, not a permanent company-scale constraint.

# Deployment and settlement

- Every deployment goes through a force-selection panel before launch, including simulated deployments from the hangar.
- Launch commits a snapshot of deployed mechs, condition, equipment, assigned MechWarriors and skills, mission objectives, and opposition.
- The interim authored-outcome tool consumes the same snapshot intended for combat.
- Combat reports battlefield facts; strategy calculates payment, contractual penalties, and repair quotes.
- Contracts have a mission completion deadline. Missing it fails the contract; payment occurs on settlement.
- Quarterly obligations block time at calendar boundaries, including during transit, until explicitly paid or deferred. The company charge is 100,000 C-bills per quarter, plus accrued transit costs and [salaries](#hiring-and-dismissing-mechwarriors-confirmed-november-21-2026); detailed operating costs follow later.
- Preview the selected route, transit days, and transit cost before embarking; no cash is collected upfront.

# Markets and recovery

- Small local markets sell complete mechs (deferred). Populated systems list MechWarriors for hire: see [Hiring and dismissing MechWarriors](#hiring-and-dismissing-mechwarriors-confirmed-november-21-2026). Each inhabited system's market sells mech systems, limited by its technology and industry: see [Equipment markets](#equipment-markets-confirmed-november-13-2026).
- Four repair bays operate, one per mech hangar; work continues during travel. Repair orders may be queued and payment is taken when work starts. Queued jobs can be cancelled freely; cancelling active work forfeits payment.
- Damaged mechs may deploy if cockpit, engine, mobility, and at least one usable weapon function.
- Objective outcome, recovered mech condition, unrecoverable mechs, and pilot casualties are separate results. Destroyed mechs recovered as wrecks may retain sale value.
- Failed or abandoned contracts pay no completion payment; any fixed failure penalty is disclosed before acceptance. Contracts may be abandoned explicitly.
- Explicitly deferring a quarterly payment starts a three-month grace period ending at the next quarterly payment. Deferral preserves cash and carries the entire bill. At that deadline, both carried debt and the new quarter must be paid in full or bankruptcy dissolves the force. There are no partial payments or second deferrals. Spending rules are unchanged during grace.

# Calendar and persistence

- Missions have a declared duration, initially one day, visible before launch. Every outcome, including retreat, consumes that full day for preparation, deployment, combat, and recovery. Completion must occur by the deadline; authored outcomes cannot silently alter elapsed time.
- Mission offers are generated around the DropShip and replaced from the oldest; see [Mission offers](#mission-offers). Offers disclose location, travel estimate and deadline before acceptance.
- Campaigns are saved in **save slots**, any number of them. The office filing cabinet opens the save grid with **Save** and **Load** tabs; main menu **Load Game** opens it for loading only. Single-player play has no autosave, and nothing loads automatically at launch. A save holds the date, cash, pending payment, debt, next quarter, bankruptcy state, scheduled events, roster, missions, travel and reputation. A mission may be settled only once.
- **Save grid.** A grid of 3 × 3 slots per page. Each slot shows its name, the in-game date (`dd MMM yyyy`) and the balance, and its tooltip gives the company and the real time it was saved. Slots are ordered by the real time they were last written, newest first. When saving, an empty slot comes first. When there are more slots than fit, **Previous**, *Page N of M* and **Next** move between pages; with one page they are hidden. A file that cannot be read is listed last as *Unreadable save*. Escape or right-click closes the grid, or the prompt over it.
- **Saving.** Clicking a slot opens a prompt with a name field and **Save** and **Cancel**. The name defaults to the company name for the empty slot and to the slot's own name when overwriting a save; the prompt says which save it replaces. Names are trimmed and at most 40 characters, and **Save** is disabled while the name is blank. Enter saves. The grid stays open and moves the save to the top.
- **Loading.** Clicking a save loads it at once, unless the current company has unsaved changes; then a confirmation offers **Cancel** and **Load Game**. A failed load leaves the current company and its unsaved status alone.
- **Folders.** Saves are stored in Godot's per-user application-data directory, outside the installation folder: single-player saves in `saves/solo/` and co-op saves in `saves/coop/`, one JSON file per slot. A single-player game, and main menu Load Game, see only solo saves; Host Co-op and the leader's cabinet in a co-op game see only co-op saves. A `campaign.json` from before save slots moves into the folder of its kind at launch, named after its company.
- New Company, Load Game, Quit, and closing the window ask before discarding campaign data only when it differs from the last successful save or load. A newly established company is unsaved until its first save. Time, assignments, seen mission offers and plotted routes count as campaign changes; navigation and view selections do not. Prompts offer Cancel and the explicit discard action, including **Quit Without Saving**. Failed saves or loads preserve the current unsaved status.

# Draft, travel, and interface

- Generate eight draft pools at campaign creation, without a reroll button.
- Initial MechWarrior skills are Gunnery, Piloting, Sensors and Grit, each rated 2–6 with lower values better. Each starts at 6; every improvement costs one point. Greens spend four points, regulars eight and veterans ten across all four skills (six and eight across three before Grit was added on November 20, 2026; Greens were added November 21, 2026). A Green MechWarrior may also have one skill at 7, **Untrained**, one step worse than Green, which gives back a point to spend on another skill; no other rank may be Untrained. Grit is a MechWarrior's determination under stress: it steadies their [nerve checks](combat-design.md#stress-surrender-and-ejection-confirmed-november-20-2026), so they hold up better to heat, damage and losses. Everywhere skills are displayed they read as words, not numbers: Untrained (7), Green (6), Regular (5), Veteran (4), Elite (3) and Legendary (2). Allocations are fixed per named pilot; progression, special abilities and player allocation controls are deferred.
- Systems connect when separated by at most 30 light-years. Choose the fewest jumps, breaking ties by total distance. Each jump takes ceil(distance / 6) whole days. Explicit regions will determine regional offer visibility.
- Damaged mechs and wrecks have guaranteed local sale at a displayed condition-adjusted price. Selling cancels associated repair orders under the cancellation rules.
- The first playable interface is a standalone Godot game written in C#, targeting Windows 11 x64, Linux x64, and macOS on Apple Silicon and Intel.

# Quarterly accounting

- Campaigns start January 1, 3025. First payment is due April 1, followed by July 1, October 1, and January 1.
- Each quarterly bill includes the flat 100,000 C-bill company charge, accrued transit days at 2,000 C-bills each, the salaries of every living MechWarrior (injured ones included), and any carried bill. Salaries are billed in advance, for the quarter the bill opens; the charge and transit are billed for the quarter just ended. The finances list them as *Salaries · 5 pilots: 210,000 C-bills*. Saves from before salaries start paying them at their next bill. Detailed overhead is deferred.
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
- Tune prices, salaries, future detailed overhead, repair costs and durations, travel fares, and contract rewards. The current slice fixes starting cash at 2,500,000 C-bills (500,000 when just starting out), the quarterly charge at 100,000 C-bills and salaries at 5,000 C-bills a quarter per skill point.
- Generated MechWarriors for when the catalog's hiring pool runs dry (deferred: dismissals and deaths slowly use it up), and painted portraits for the 24 entry-level MechWarriors, who show a placeholder silhouette.
- Author route data, market inventory, mission templates, and outcome examples. The first draft configuration is authored in the roster slice.


## Roster and initial draft interface (confirmed October 24, 2026)

The desk rolodex displays the number of healthy MechWarriors ready for immediate deployment, including assigned warriors. Clicking it opens a panel over the office: one personnel card at a time, previous/next controls and a name index. The panel is headed PERSONNEL. Cards show portrait, name/callsign, Gunnery, Piloting, Sensors, Grit, health/recovery date, the BattleMech the warrior is piloting ("Piloting: <mech>" or Unassigned), Deployments, Kills and quarterly salary. Its section list offers *Active personnel*, *In memoriam* and *For hire* (see [Hiring and dismissing MechWarriors](#hiring-and-dismissing-mechwarriors-confirmed-november-21-2026)). A deployment is any settled mission, won or lost, in which the warrior piloted a deployed mech; a kill is an enemy BattleMech the warrior's hit left disabled or destroyed. Both are saved with the warrior and stay on memorial cards. Injured warriors retain assignments. Deceased warriors are automatically unassigned and retained separately in memoriam. Flip individual cards or choose a name from the index. The desk count includes healthy assigned and unassigned warriors, excludes injured and deceased warriors, and updates after recovery.

The existing right porthole looks into an enclosed repair bay. Clicking it opens a dedicated hangar interface with a new painted background, a large selected BattleMech, roster selection along the bottom, classic specifications and pilot assignment controls. Assignment is one-to-one; transfers and swaps require explicit confirmation. Establishing a company assigns the drafted warriors to the mechs in their corresponding columns: Warriors I to Light, Warriors II to Medium, Warriors III to Heavy, and Veterans to Assault. Later hangar changes and established saves retain their assignments, including empty seats. This slice provides viewing and assignment, without sales, renaming or combat resolution; [dismissal](#hiring-and-dismissing-mechwarriors-confirmed-november-21-2026) came later, from the rolodex, and [repairs](#hangar-repairs-confirmed-november-13-2026) came later. Choose a living warrior to assign, or unassign the current pilot. Drafted mechs start intact with full ammunition.

## Hangar blueprint and storage (confirmed October 31, 2026)

The hangar is laid out for 1440 × 900 and leads up to refitting, which is not yet available:

- **Inventory and Market** tabs fill the left column. **Inventory** is storage: spare equipment stacked and grouped as Energy, Ballistic, Missile, Ammo and Equipment, each line showing the spare's name and count, drawn as a slot (filled rectangle with an outline) in the colour the item takes in a location panel. **Market** lists, in the same groups and style, what the [market](#equipment-markets-confirmed-november-13-2026) where the DropShip is in orbit holds, each line with how many are left beyond every mech's pending purchases (*Medium Laser ×3*) and its price. Above the lines it names the market and its ratings (*Galatea market · 47 items*, then *Regressed · Developing*), the company's cash and the total of pending purchases. Opening the tab views the market, stocking or rolling it over. In transit the tab says "No market while in transit"; on an uninhabited world, "No market on this world".
- **The selected mech** fills the centre as a blueprint: its real 3D model, front-on, tinted cyan over a darkened bay and a faint grid. Locations that have lost armor or structure tint amber; destroyed locations stay hidden. Right-drag spins it at half a degree per pixel; on release it eases back to front-on over 0.3 seconds. A right click that moves no more than five pixels between press and release still returns to the office.
- **One panel per location** sits roughly over that body part, its name centred across the top, with the mech's right side on the viewer's left. Each has a 12-pixel bar of installed armor against the most the location can carry at the mech's tonnage (twice its structure, or nine stock points for the head), with lost armor shaded inside the installed section; the torsos have a second bar beneath it for rear armor, against the same limit. Directly beneath those is a 12-pixel grey bar of current against maximum structure. Armor and rear armor as current/installed and structure as current/maximum are written over their bars, falling back to AR / RR / IS in narrow panels; bar colours are dimmed so the text stays legible. Each armor bar ends in − and + buttons (hidden on a destroyed location) that take off or put on one stock point of that armor (front or rear), or ten with Shift held, as a pending loadout change; front and rear together stay between zero and the location's limit. Added armor arrives intact; armor taken off leaves the current armor at most the new installed amount, so existing damage is kept for repair. Below are all of the location's critical slots (six for the head and legs, twelve elsewhere), coloured by kind; weapons take their hardpoint class's colour, the same as a free mount of that class. Each item is one block, however many slots it takes; every heat sink and jump jet is its own item, even when several share a location. Damaged items show amber; destroyed items show red and crossed out. Hovering a slot names the item with its mass, slots and condition. Space hides or shows all the location panels to give a clear view of the model, even while a button has focus; they are shown again whenever the hangar opens. Tab cycles the mech preview between standing still, walking, running and getting up from a knockdown (a loop of toppling forward, lying face down, getting up and standing a moment), then back to still, even while a button has focus.
- **The summary** fills the right column, without section headings: the MechWarrior (portrait, name, callsign, skills, and the assignment controls), then the mech's name with its current / maximum tonnage beneath in the same large type (amber while below maximum), its class, role, engine and status; walk and run speeds in km/h and jump range in metres, one per line (see [Movement readouts](#movement-readouts-confirmed-november-11-2026)); then three 30-pixel bars, each with its value written over it and its colours dimmed so the text stays legible. **Armor** and **Structure** read current / stock on a bar whose full width is the most the tonnage allows (the sum of the location armor limits; structure is fixed by tonnage, so its stock is its maximum). The remaining amount is one solid colour from a red → orange → yellow → green scale at the fraction remaining, and the amount still to repair up to stock is grey. **Cooling** reads as a percentage: heat sunk per second (working sinks × dissipation per sink / 10 s) over the most heat per second the mech can sustain, every usable weapon firing as soon as its cooldown allows plus running heat (2 per 10 s; jumping adds no heat). It fills on the same colour scale, full at 100%, and shows ⚠ at its right end when cooling exceeds 100%. Hovering it shows both rates per second. Beneath the bars, **Repair Armor** and **Repair All** order a [repair](#hangar-repairs-confirmed-november-13-2026); a mech with an order shows its status and **Cancel Repair** instead.
- **The mech buttons** run along the bottom, with **Reset**, **Apply**, **Back to office** and **Simulate Deployment** at their right. Each shows the chassis, then designation and tonnage (• for pending changes, · Pool for a mech outside the lances). A damaged mech gets a third line and a 3-pixel border: **Armour Damaged** with a yellow border when only armor is missing, or **Internals Damaged** / **Systems Damaged** with a red border when internal structure is below stock or a component, weapon or ammunition bin is damaged or destroyed. The line names the worst damage, internals before systems before armor; spent ammunition is not damage. A mech with a repair order reads **Under Repair** or **Repair Queued** there instead, keeping its damage border. The selected mech keeps its green fill, and its gold border gives way to a damage border. The buttons use 12-pixel text so the third line fits. Below 1100 pixels wide the columns narrow, the location panels form three columns and the blueprint scrolls.

Critical slots follow the stock record-sheet layout: the catalog's fixed systems first, then weapons, heat sinks and jump jets, then ammunition. Stock weapon slot sizes and masses come from the standard tables. A stock design that cannot fit a large weapon into one location splits it into the neighbouring location, as the King Crab's AC/20s do.

A new company starts with one spare of every weapon, ammunition bin, heat sink (including those held inside engines) and jump jet mounted across its mechs, so every mech could be fully re-armed and re-sunk. Jump jets are stored by mass class. Engines, gyros, actuators and cockpit systems have no spares, and armor is never stored. Storage is stocked once, when the company is established; a save from before storage existed is stocked once from its mechs when loaded. Later mechs do not add spares.

## Hangar loadouts (confirmed November 1, 2026; installation confirmed November 1, 2026)

Each owned mech has its own **loadout**: its stock design less the items removed in the hangar, plus the spares installed there from storage, with each location's installed armor as changed in the hangar. Armor weighs a ton per 16 stock points, counted from the stock amount; it is never stored. Combat, heat, jump movement, readiness and the hangar summary all use it, and it is saved with the company. Saves from before heat sinks and jump jets were separate items give each one the condition of its old group.

- **Installation.** Drag a storage line onto a location panel to install one spare there; storage lines say "Drag onto a location to install", and a line at ×0 cannot be dragged. Installing is mouse only. The item takes the first empty slots, after everything already in the location: empty slots are always the bottom-most, so removing an item moves the items below it up. Items are never split across locations (stock designs that split a weapon keep it, but the split cannot be recreated). Installed items arrive operational and bins full, and from then on follow the same rules as stock items: they take critical hits, can be marked ⚠, count for operational status and ammunition pairing, and right-click takes them out again (back to storage, or scrapped if destroyed). Market lines are dragged the same way ("Drag onto a location to buy and install"), whatever storage holds: the item is installed as a **pending purchase**, its tooltip saying "Pending purchase · 40,000 C-bills". Pending purchases never touch storage counts or cash, but hold the item back from the market's line; taking one out cancels it. A market line at ×0 cannot be dragged ("None left in the market"). **Right-click** a market line to buy one straight into Inventory, paid at once and not undone by Reset; **Shift+right-click** buys five, or as many as are left and affordable. A drop is refused when:
  - the location's structure is gone ("Location destroyed"), or it has too few empty slots ("Needs N free slots");
  - a weapon has no free mount of its class (energy, ballistic or missile) in that location (see **Weapon mounts** below). Installed weapons face forward. A weapon shows on the 3D preview only where its chassis has a visual for it on that hardpoint (see [mech models](mech-models.md#weapon-hardpoints)); otherwise it fights without one;
  - a heat sink is not the mech's type, single or double;
  - a jump jet is going in the head or an arm, is not of the mech's mass class, or would raise jump MP above walking MP.
  Ammunition and heat sinks otherwise go anywhere with room. While a spare is dragged, the location panels are shown even if Space hid them, and each panel's empty slots are tinted green where it can go and red where it cannot, with the reason written over them. Escape or right-click during a drag cancels only the drag; releasing anywhere but a valid panel does nothing.
- **Weapon mounts.** A location's hardpoints are critical slots of their class; the rest of its slots are regular. Each weapon takes one mount of its class while one is left, whatever its size and wherever it was dropped. Stock weapons beyond the hardpoints, or in a location without one, are kept but take no mount, so once removed they can only go back where a mount is free. A non-weapon dropped on a mount slot covers that mount, even with regular slots free. Every other slot, including a weapon's extra slots, fixed systems and stock items, fills regular slots first and then covers free mounts from the bottom up: Missile, then Energy, then Ballistic. A covered mount takes no weapon until the item covering it is removed. Free mounts are the location's hardpoints less those its weapons take and those other items cover. The mount an installed item was dropped on is saved with the loadout; all other coverage is worked out afresh from the loadout, so removing an item that took a regular slot can uncover a mount. The location panel labels its first empty slots with its free mounts, Ballistic, Energy then Missile, in pale yellow `#e6dc82`, cyan-green `#5fd3b0` and orange-red `#e07a5a`, with regular empty slots as "—" below them; destroyed locations show none. Hovering an empty slot of a location with free mounts names its mount (or "Empty slot"), lists the location's free mounts and says to drag a weapon from storage. An item covering mounts has a 3-pixel bar in each covered mount's colour down its left edge, one row tall per mount, and a "Covering a … mount" tooltip line. The labels stay visible under the drag tint.
- **Removal.** Right-click a weapon, ammunition bin, heat sink or jump jet in a location panel to remove it, freeing all its slots (both halves of a weapon split across two locations). Engines, gyros, actuators, cockpit systems and engine-held heat sinks cannot be removed. A right click on any slot never navigates back; elsewhere it still does. The slot tooltip says "Right-click to remove", or "Right-click to scrap" for a destroyed item.
- **Effects.** Current tonnage is the rated tonnage less the mass of removed items plus that of installed ones; stock designs are taken to be exactly at their rating. Each removed heat sink removes one sink's cooling, and each removed jump jet one jump MP; each installed one adds them back.
- **Free heat sinks (confirmed November 18, 2026).** A mech's first ten heat sinks weigh nothing, wherever they sit; each beyond ten weighs 1 t, single or double. The engine's internal sinks (its rating ÷ 25, rounded down, as the catalog gives them) count first toward the ten, then slotted sinks in mount order, stock before installed. Removing or installing a heat sink therefore changes the mech's tonnage only above ten, and removing one may make a later slotted sink free. A 300-rated Atlas holds 12 sinks in its engine, 10 free and 2 weighted, and all 8 of its slotted sinks weigh 1 t; a Flea's 10 sinks (4 in the engine, 6 slotted) are all free; a Blackjack's engine holds 7, its first three slotted sinks are free and its fourth weighs 1 t. In the location panels, the centre-torso engine slot reads e.g. "Fusion Engine · 12 HS", and its tooltip adds "12 heat sinks inside the engine · 10 free, 2 at 1 t each". Free slotted sinks read "Heat Sink (free)" and are shaded a lighter blue (`#a9d8f2`), row by row for an item that is only partly free; their tooltip reads "Free (within the first 10 heat sinks)" where weighted ones give "1 t".
- **Over tonnage.** A loadout may go over the rated tonnage while it is pending. While the selected mech is over, a yellow ⚠ **Over Tonnage** shows above the blueprint, the tonnage figure turns red, and Apply is disabled, its tooltip saying "Over tonnage by X t". Simulated deployments have no tonnage restriction. A mech left with no usable weapon is not operational.
- **Storage.** Removed items join storage, damaged ones as ordinary spares; destroyed items are scrapped. Installed items leave it. A bin is stored as a full bin whatever it held. Ammunition is not tracked between battles: every installed bin refills for free after each battle (applied once missions report results), so hangar tooltips show no shot counts.
- **Pending changes.** Removals, installations and armor changes stay pending, per mech, until applied. Several mechs may have pending changes at once, and their buttons along the bottom are marked with a gold •. The hangar shows pending loadouts throughout: panels, tonnage, summary, the 3D preview's weapon mounts and storage counts. Storage counts include items taken out and exclude items installed in every mech's pending loadout, so two mechs cannot both claim the last spare. Taking out a pending installed item returns it to storage at once. **Apply** orders a [refit](#refits-confirmed-november-21-2026) of the selected mech to its pending changes and pays for its pending purchases at once (the company then has unsaved changes; nothing is written to disk); the mech keeps its old loadout until the refit completes. Its tooltip lists the refit's work and days, whether it starts today or waits for a bay, and "Buys N items · X C-bills". Apply is disabled, saying "Not enough C-bills to buy…", while the selected mech's purchases cost more than the company's cash. **Reset** discards the changes and cancels their purchases at no cost. Both are disabled unless the selected mech has pending changes.
- **Suspect items.** An installed weapon with no bin of its ammunition anywhere on the mech, and a bin that no installed weapon fires, show a yellow ⚠ after their name, whatever the location or damage, with a tooltip line saying why. Applying a loadout with ⚠ items asks "This loadout has items marked ⚠. Apply anyway?".
- **Leaving.** Leaving the hangar for the office (Back to office, Escape or right-click) with pending changes on any mech warns how many mechs have unapplied changes, offering **Stay in hangar** or **Abandon changes and leave**; abandoning cancels pending purchases. Viewing MechWarriors does not warn. **Simulate Deployment** does not warn either: the simulation fields the pending loadouts, and they are still pending on return.

## Hangar repairs (confirmed November 13, 2026)

Two buttons in the hangar summary, beneath the condition bars, order a repair of the selected mech:

- **Repair Armor** restores front and rear armor to the installed amount on every location that still has structure. A destroyed location needs **Repair All**.
- **Repair All** restores armor and internal structure everywhere, repairs every damaged item, and replaces every destroyed weapon, ammunition bin, heat sink and jump jet with a spare from Inventory, refilling the bins. Engines, gyros, actuators and cockpit systems are always repaired in place and never need parts.

**Damage levels.** Each item records the hits it has taken from [critical hits](combat-design.md#critical-hits-confirmed-november-14-2026): one for a damaged weapon or double heat sink, and one per damage level for a critical system, up to its maximum. An item lost with its location counts as fully hit: every critical slot of a weapon, bin, heat sink or jump jet, or every damage level of a critical system. A damaged item in a save from before hits were counted has one hit. Hangar slot tooltips and the roster read *Damaged*, *Critically damaged* (a critical system at its maximum level) or *Destroyed*, with a critical system's level count ("Damaged 1/3").

**Parts.** Destroyed weapons, bins, heat sinks and jump jets each take one matching spare from Inventory; spares claimed by any mech's pending loadout changes are not available. The parts leave storage when the order is placed and return to it if the order is cancelled, whether queued or started. A destroyed item with no spare is left destroyed and out of the days and cost; the tooltip lists it under *Missing parts*. When missing parts are all that is left to repair, Repair All is disabled with that list.

**Time and price.** The order's days and cost are fixed when it is placed. The numbers live in `RepairTuning` and are placeholders for tuning; repair times were tripled on November 21, 2026.

| Work | Time | Price |
| --- | --- | --- |
| Armor (16 stock points a ton) | ¾ day per ton | 10,000 C-bills a ton |
| Internal structure (a tenth of rated tonnage when whole) | 3 days per ton | 40,000 C-bills a ton |
| Damaged weapon, bin, heat sink or jump jet | 1½ days per hit slot | hit slots ÷ its slots × its [base price](#equipment-markets-confirmed-november-13-2026) |
| Destroyed weapon, bin, heat sink or jump jet (with a spare) | 1½ days per slot it takes | 10,000 C-bills per slot it takes |
| Engine (3 levels at most) | 3 days per level | rating ÷ 400 × 100,000 C-bills per level |
| Gyro (2 levels at most) | 3 days per level | tonnage ÷ 100 × 50,000 C-bills per level |
| Cockpit (1 level) | 1½ days per level | 30,000 C-bills per level |
| Sensors (2 levels) | 1½ days per level | 20,000 C-bills per level |
| Life Support (1 level) | 1½ days per level | 15,000 C-bills per level |
| Hip (1 level) | 1½ days per level | 20,000 C-bills per level |
| Shoulder (1 level) | 1½ days per level | 15,000 C-bills per level |
| Other actuators (1 level) | 1½ days per level | 10,000 C-bills per level |

The engine rating is read from the engine's name. The total time rounds up to whole days, at least one. The Repair All tooltip lists the work line by line (*Armor 3.4 t*, *Structure 1.2 t*, *Fusion Engine · 3 levels*, *PPC · 2 of 3 slots*, *AC/10 · replaced*, *Parts used: AC/10 ×1*, *Missing parts: AC/20 ×1*) above the days and C-bills.

**Bays.** Four repair bays work at once, one per mech hangar. Orders beyond four queue, first ordered first served. A bay takes an order on the day it is placed or the day a bay frees, and the order is paid for then; a queued order the company cannot afford waits until it can, holding up those behind it. An order can only be placed while its cost is affordable, the mech has no other order and no pending loadout changes, and the mech is in the player's lance or the lance pool.

**While ordered.** The mech cannot deploy (Quick Launch and Simulate Deployment list it as *Under repair* or *Repair queued*, or for a refit *Being refitted* or *Refit queued*) and its loadout cannot change. Pilot and lance changes are still allowed. The summary shows *Under repair · completes 14 JAN 3025*, or the order's place in the queue, with **Cancel Repair**.

**Cancelling.** A queued order cancels at once at no cost. Cancelling work already started asks first: the payment is forfeited and the damage stays. Either way its parts return to Inventory. Either way the next queued order may take the bay.

**Completion.** On the completion date the mech is restored, a *Repair complete · <mech>* event stops the calendar like other events, and the freed bay takes the next queued order that day. Orders are saved with their mechs.

## Refits (confirmed November 21, 2026)

Changing a mech's loadout takes bay time. **Apply** in the hangar places a **refit order**: the mech keeps its current loadout until the refit completes, then changes to the new one. Refits and repairs share the four repair bays and one queue, first ordered first served, and a mech holds one order at a time. A refit order can only be placed while the mech has no other order and the pending changes are within tonnage, affordable and in stock.

| Work | Time |
| --- | --- |
| Installing a weapon, bin, heat sink or jump jet | 1½ days per slot it takes |
| Removing one | ¾ day per slot it takes |
| Armor put on or taken off, front or rear | ¾ day per ton |

The total rounds up to whole days, at least one. The numbers live in `RefitTuning` and are placeholders for tuning. There is no labor charge: a refit costs nothing beyond its purchases, which are paid for when Apply is pressed and join Inventory, so a refit never waits on cash. The spares it installs, bought or already stored, are reserved from Inventory when it is ordered.

While ordered the mech shows *Refitting · completes 14 JAN 3025* or *Refit queued*, with **Cancel Refit**; its roster button reads *Refitting* or *Refit Queued*. Cancelling a refit, queued or started, is free and asks nothing: the reserved spares return to Inventory, bought items stay there, and the mech keeps its old loadout. On the completion date the new loadout is fitted (items taken out join Inventory, destroyed ones are scrapped, as before), and a *Refit complete · <mech>* event stops the calendar.

## Starting scenarios (confirmed November 21, 2026)

**New Company** first asks how the company begins, in a *Starting scenario* dialog. Escape returns to the main menu.

- **Just starting out:** two light BattleMechs, a Regular and a Green MechWarrior, and 500,000 C-bills. The draft has four pools of three: *Light · I* and *Light · II* (six different light mechs between them) over *MechWarriors · Regular* and *MechWarriors · Green*, in two columns filling the screen width. Both mechs join Lance 1, each piloted by the MechWarrior in its column.
- **Established:** the eight-pool draft described under [Roster and initial draft interface](#roster-and-initial-draft-interface-confirmed-october-24-2026), with 2,500,000 C-bills.

In co-op, the leader picks the scenario on the hosting form, beside the player count, and every player drafts for it. A player count the scenario cannot give distinct MechWarriors to is disabled: the established draft supports two players, starting out three. The scenario is not saved; older saves count as established.

## Hiring and dismissing MechWarriors (confirmed November 21, 2026)

**Salaries.** A MechWarrior's quarterly salary is 5,000 C-bills per net skill point spent: Green 20,000, Regular 40,000, Veteran 50,000. Every living MechWarrior is paid, injured or not, on each quarterly bill in advance (see [Quarterly accounting](#quarterly-accounting)). Dismissed and deceased MechWarriors are not paid, and nothing is refunded.

**Hiring lists.** Every populated system has a hiring list, whether or not it has an equipment market. Its slots follow population: none for an uninhabited world, 1 below 100 million people, 2 below a billion and 3 from a billion up. Each empty slot fills two-thirds of the time when it rolls, with a catalog MechWarrior who is not in the company, has not been dismissed, is on no other list and has not left a list in the last 7 days. A list is made on the first visit, and its empty slots roll on each arrival and once a day while the DropShip stays in orbit, never while it is away. Each listed MechWarrior has a 5% chance a day to leave, worked out for the days away too. A MechWarrior who leaves a list, for whatever reason, is on no list anywhere for 7 days, including the one they left. Lists are saved with the campaign.

**For hire.** The rolodex's *For hire* section lists the MechWarriors for hire where the DropShip is in orbit. In transit it reads *No recruits in transit*, and an empty list *No MechWarriors for hire here*. A card shows *FOR HIRE*, rank and specialty, the salary and the skills, and **Recruit · 63,000 C-bills**. The price is the rest of this quarter's salary, prorated by the days left and rounded up to 1,000 C-bills, plus a signing bonus of one quarter's salary. The tooltip breaks it down. Recruit is disabled, its tooltip saying why, when the company cannot afford it. It stays available while a quarterly payment is due or deferred. Recruiting pays at once; the MechWarrior joins healthy and unassigned, can be given a mech in the hangar immediately, and is on the next quarterly bill as normal. The company has no roster limit.

**Dismissal.** Active personnel cards offer **Dismiss**, memorial cards neither button. Dismiss asks first (*Dismiss MechWarrior?*), then unassigns the MechWarrior and removes them from the company entirely: they are not kept in memoriam and are never listed for hire again. Injured MechWarriors can be dismissed; the deceased cannot. In co-op, a MechWarrior piloting a mech in another player's lance cannot be dismissed (the button says why).

**Catalog.** Hiring draws on the whole catalog of 48 MechWarriors. The 24 entry-level ones added for it (12 Green, six of them with one Untrained skill paired with a strength, and 12 Regular) show a placeholder silhouette until their portraits are painted.

## Equipment markets (confirmed November 13, 2026)

Every inhabited system has a market for mech systems: weapons, ammunition bins, single and double heat sinks, and the three jump jet classes. Engines, gyros, actuators and cockpit systems are never sold; they cannot be swapped, and they can always be repaired.

**Ratings.** Each system's two ratings come from its USILR socio-industrial code in the MegaMek data (technology, industrial development, raw materials, industrial output, agriculture, graded A–F; X for uninhabited worlds, which have no market). The star map's system tooltip and route readout show them (*Regressed tech · Developing*).

- **Technological Level** follows the technology grade: F Primitive, D Regressed, C Standard, B Advanced, A Star League. The former Star League worlds Terra, Tharkad, New Avalon, Luthien, Sian and Atreus are one level above their grade, so only Terra and Tharkad (grade B) reach Star League.
- **Industrial Capacity** follows the output grade, and sets how many items a full market holds: F Backwater (20), D Developing (50), C Industrial (100), A or B Major Industrial (180). The BattleMech factory worlds Hesperus II, Coventry, New Avalon, Luthien, Alshain, Gibson, Irian, Kathil and Galax are Factory Worlds (300) whatever their grade.

Galatea, where companies start, is Regressed and Developing.

**What is stocked.** A market holds exactly its capacity, counted item by item, each a weighted random draw from the items its technology level reaches. Ammunition bins share their weapon's level and draw at twice its weight. Prices are base prices, the same in every market. The numbers live in `MarketTuning`.

| Lowest level | Items: draw weight, base price (C-bills) |
| --- | --- |
| Primitive | Machine Gun 10, 5,000 · Flamer 5, 7,500 · AC/2 5, 75,000 · AC/5 5, 125,000 · SRM 2 10, 10,000 · Heat Sink 10, 2,000 |
| Regressed | Small Laser 10, 11,250 · Medium Laser 10, 40,000 · SRM 4 10, 60,000 · LRM 5 5, 30,000 · LRM 10 5, 100,000 · AC/10 5, 200,000 · Jump Jets 5, 10,000 (≤55 t) / 20,000 (60–85 t) / 40,000 (90+ t) |
| Standard | Large Laser 5, 100,000 · SRM 6 5, 80,000 · LRM 15 5, 175,000 · PPC 2, 200,000 |
| Advanced | AC/20 2, 300,000 · LRM 20 2, 250,000 |
| Star League | ER Large Laser 2, 200,000 · Double Heat Sink 2, 6,000 |

Ammunition bins cost: Machine Gun 1,000, AC/2 1,000, AC/5 4,500, AC/10 6,000, AC/20 10,000, any LRM 30,000, any SRM 27,000.

**Viewing and rollover.** A market is stocked the first time it is viewed: opening the hangar's Market tab while the DropShip orbits its system. It remembers the date. Each later view brings it up to today: it replaces round(capacity × (1 − 0.95^days)) items (5% a day), first filling gaps left by purchases and then swapping out randomly chosen items still held, and records today's date. A second view on the same day changes nothing, and a market bought out refills at the same 5% a day.

**Buying.** Stock is finite. Pending purchases hold items back from the market's lines, Apply takes them from the market for good, and right-click buys straight into Inventory. If another player has bought the last of an item first, Apply is disabled with "Only 0 Medium Laser left in the market".

**Saves and co-op.** Markets that have been viewed are saved in the campaign with their stock and date, and stock is drawn from a saved seed. Markets are shared by every player: any player opening the Market tab asks the leader to bring that market up to date, and the result reaches everyone with the campaign. Saves from before markets stock each market when it is first viewed.

**Movement readouts** (confirmed November 11, 2026). Screens never show movement points. Walk and run show as km/h at 10 km/h per MP, the combat movement rate, and jump as its range in metres (45 m per jump MP); a mech without jump jets shows no jump. The hangar lists them one per line, the roster's overview on one line ("Walk 40 km/h  ·  Run 60 km/h  ·  Jump 120 m"), and draft cards in compact form ("40/60 km/h · J 120 m", jump abbreviated to fit the card; its tooltip and record spell it out). Owned mechs show their refitted values and draft candidates stock ones. `CombatRules.MovementSummary` and `CompactMovement` produce all of them.

Roster art includes a maintenance-bay background, original warrior portraits, and 3D model previews for draft candidates. Unit detail includes classic stock 3025 loadouts, front/rear armor and internal structure by location, component condition, weapons and ammunition, movement and heat information.

A new company first chooses a [starting scenario](#starting-scenarios-confirmed-november-21-2026), then opens the shared initial draft before normal operations. In the *Established* scenario, eight pools of three candidates are simultaneously visible: Light, Medium, Heavy and Assault BattleMechs, plus three regular MechWarrior pools and one more experienced pool. The warrior catalog contains balanced, Gunnery-focused, Piloting-focused and Sensors-focused recruits; candidates are randomly sampled within their experience class. Individual candidate records can be inspected without selecting. There are no rounds or rerolls. Each new draft samples three distinct candidates per weight class from all 49 chassis and draws pilots from the catalog's 30 regulars and six veterans (its 12 Greens appear only when just starting out, and for hire). A random company name and one randomly selected candidate per pool are filled in, so the company can be established immediately. The screen is titled *Select Initial Mechs and Pilots*, with the company name and **Establish Company** in the header. Each pool is headed only by its weight class or warrior group. Mech cards list the chassis, then designation, tonnage and compact movement ("40/60 km/h · J 120 m") on separate lines. Warrior cards list the callsign, then Gunnery, Piloting, Sensors and Grit each on its own line as a word: Green (6), Regular (5), Veteran (4), Elite (3) or Legendary (2). The tooltip and the full record add the skill points spent. All eight pools remain visible at 900 × 500. Click a candidate to change a selection; use **…** to inspect the full record without selecting. Escape from a record returns to drafting.

The company name and all eight choices remain editable until **Establish Company** commits the complete roster. Escape from an unfinished draft asks to abandon it and return to the main menu. Unfinished drafts cannot be saved, and established drafts cannot reopen. Existing finance-only saves require this same draft while retaining their date, cash, obligations and scheduled events; loading or drafting does not overwrite the save.

Opening roster interfaces pauses time. Escape from the hangar, map, or office rolodex returns to the office; Escape in the office opens the main menu. Clicking the jacket on the office wall opens it immediately. Escape or right-click in the main menu acts as **Resume** when it is available, and otherwise does nothing. The main menu offers **Resume** when an established, solvent company remains in memory, except while a battle is in progress. Resume restores the previous strategic screen, panels, selections and focus, preserving unsaved company state while leaving time paused. It retains the session only while the app is open and does not write a save. Starting or loading another company asks before replacing a live company only when it has unsaved changes, as described under Calendar and persistence.


## Star travel (confirmed October 25, 2026)

Click a named system or select a destination while in orbit to plot a blue candidate route. This does not replace an embarked green journey. Embark commits the candidate as the active journey but leaves the company in orbit until the next day. Cancel Journey clears only the embarked journey; a candidate can be ignored and remains available while in orbit.

Time advances only in the office. Advancing a day departs orbit, clears any unembarked candidate, and counts the first transit day. Plotting, embarking, and cancellation are unavailable partway through a jump. Arrival is a calendar-stopping event at every system, including intermediate stops. The company spends that date in orbit and automatically continues its embarked journey when time advances to the next day. Final arrival completes the journey.

A 30-light-year jump takes five days; shorter jumps scale linearly and round up independently to whole days, with a minimum of one day. Embarking on day 10 for a 30-light-year jump produces arrival on day 15. Every transit day costs 2,000 C-bills, including a completed day on a quarterly boundary. Orbit incurs no transit charge. Transit costs join the current calendar-quarter bill and use the same full-payment, one-deferral, and bankruptcy rules. Same-day arrival notifications wait for payment or deferral.

Persist location, candidate and embarked routes, current jump progress, accrued transit days, and the frozen transit component of a pending bill. Use the source map's coordinates and scale; duplicated system names retain distinct stable identities. Unreachable destinations cannot be embarked.

**System owners** (confirmed November 5, 2026). Every system has the owner it had on 1 January 3025, from [`Data/system-factions.json`](../Data/README.md#star-system-owners): a Great House, ComStar (Terra only), a Periphery state, an Independent world, or several claimants when contested. Abandoned and unsurveyed systems have no owner. The hover tooltip has a second line, "Owner: Lyran Commonwealth · Neutral 0", giving the owner and your [reputation](#reputation-confirmed-november-5-2026) with it. Contested systems and owners that aren't tracked factions are named without a reputation, and unowned systems show no owner line. A third line says whether a click plots a journey.

**System biomes** (confirmed November 4, 2026). Every system has a [biome](combat-design.md#biomes-confirmed-november-4-2026) derived from its primary world's 3025 climate (`Data/system-climate.json`), and systems of 4 billion people or more are also **Urban**. The biome shows in the system's hover tooltip, in the location readout while in orbit ("In orbit · Galatea · Volcanic"), and as the destination's biome in candidate and embarked routes.

Find a system with the destination search as an alternative to clicking it. Drag to pan; scroll or use the + / − buttons to zoom. With the map focused, + / − zoom, arrow keys pan, and Home fits the complete chart. 100% zoom fits the whole chart; maximum zoom is 1200%. Resizing preserves the image aspect ratio, hotspot alignment, and map location. Return with “Back to office”, right-click, or Escape; map position survives navigation, and keyboard focus returns to the wall chart.


## Back navigation and DropShip visibility

Outside tactical combat, right-click closes the topmost popup, confirmation, or subscreen and returns one level toward the office. Tactical combat uses right-click for movement and attack commands, with Escape opening the combat menu. Reuse the existing cancel/back action, restore focus, and consume both press and release. Right-click in the unobstructed office stops time and opens the main menu; in the main menu it resumes the suspended screen, if any. Draft abandonment and bankruptcy retain their existing confirmation and dismissal rules. Personnel opened from the hangar returns to the same hangar selection; its visible Back action and Escape follow that parent too.

A fixed-size gold DropShip silhouette, dark backing, halo, and permanent location label identify the current orbit or interpolated position in transit on the chart. Offscreen position is indicated by a clickable direction arrow. The arrow and Find DropShip button center the current position at the existing zoom, within map bounds, without changing candidate or embarked journeys. Preserve the chosen camera view when reopening the chart. Keep the marker label and locator clear of the zoom controls and usable at 900 × 500.


## Mission offers

A silent flashing light on the office phone indicates new mission offers. Opening it marks all available offers as seen. Browse briefings beside the offer list, accept one mission, or inspect the current mission while browsing others. Accepting returns to the office; abandon with confirmation before accepting another. Mission selection and seen status are saved with the company. Deployment follows starmap movement. Each offer is a contract with a single mission.

**The mission offer board** (confirmed November 3, 2026) holds 10 offers, listed oldest first. A new company starts with 10 generated offers. On every day advanced, including in transit and with a mission accepted, there is a 50% chance that the oldest offer is removed (at most one per day); the board is then refilled to 10. Offers have no expiry date, only the date they were posted. Accepting an offer takes it off the board, which holds 9 until the next day's refill; the daily roll never removes the accepted mission. Abandoning a mission removes it for good. New offers are posted that day, unseen, and flash the phone; offers never stop the calendar.

**Each generated offer** has:

- an employer and an opponent (see [Contract sides](#reputation-confirmed-november-5-2026)), and a **contract kind**: a **raid** (the employer attacks), a **defense**, or a **patrol** (see [Patrols](#patrols-confirmed-november-13-2026)). The placeholder name and briefing follow the kind ("Raid on Galatea", "Defense of Galatea", "Patrol of Galatea") and name the employer; the objective is "Defeat the defending force" on a raid, "Defeat the attacking force" on a defense and "Visit each patrol point in order, then reach the extraction zone" on a patrol. The phone's offer list shows each offer's sides, as "Davion vs. Kurita", and the terms show the employer, opponent and contract kind with your reputation with each tracked faction.
- a biome: the location's, shown in the terms. Missions will be fought on it once they launch battles.
- a location: a named system within 3 jumps of the DropShip when the offer is posted, counted from the system being jumped to while in transit, chosen to suit the sides. It never changes.
- its opposition, filled to the offer's [challenge rating](#challenge-rating-confirmed-november-13-2026): stock catalog mechs of weight classes drawn Light 40 / Medium 35 / Heavy 18 / Assault 7 (lighter on a patrol), each piloted by an unnamed pilot of rank Green 20 / Regular 50 / Veteran 22 / Elite 8. The briefing shows exactly the force that will be fielded, and the terms show its total tonnage ("OPPOSITION 265 t").
- a completion deadline of travel days to the location plus 14, shown as "N days after acceptance" and fixed as a date on acceptance. The travel estimate finishes the current jump first.
- fixed rewards, with no negotiation yet. Difficulty is the enemy tonnage, each mech weighted by its pilot's rank (Green 0.8, Regular 1.0, Veteran 1.25, Elite 1.5). The budget is difficulty × 4,000 C-bills, ±20%, split at random: at least 40% to payment, the rest between salvage and the insurance limit. **Payment** is in C-bills. **Salvage** is a number of picks from wrecks and equipment after the battle: its share ÷ 100,000 C-bills, rounded, from 0 to 6. **Insurance** pays the lesser of the cost of repairing the battle's damage to the deployed mechs, destroyed parts at replacement value, and the offer's limit (a negotiated maximum once negotiation exists). Payment and the insurance limit are rounded to 1,000 C-bills.

Payment, salvage and insurance are settled when a mission is fought through [Quick Launch](#quick-launch-confirmed-november-9-2026); see [Salvage and insurance](#salvage-and-insurance-confirmed-november-17-2026). The deadline is recorded but missing it has no effect yet. The numbers live in `MissionTuning`.

Generation uses a seeded random number generator whose state is saved, so reloading and advancing repeats the same offers and removals. Saves before version 8 receive a freshly generated board and lose their accepted mission. Offers saved before contract kinds existed stored only whether they were a raid; they load as a raid or a defense and keep their opposition.

### Challenge rating (confirmed November 13, 2026)

Every offer's opposition is filled to a target tonnage, its **challenge rating**, rolled when it is posted. The rating does not look at the company's own mechs.

- **Default.** Each tracked faction's whole-point standing counts from 0 up, negative standing counting as 0, and the six are added together. A total of 0 gives the minimum default of 100 t per company lance; the default rises linearly to 400 t at a total of 300 or more. Davion +40, Kurita −30 and Steiner +20 total 60, a default of 160 t.
- **Spread.** The tonnage is the default times a normal draw centred on 1 with σ 0.2224, so half of all offers lie within ±15% of the default; the multiplier is clamped to 0.5–1.75. Easy jobs and brutal ones both turn up.
- **Per lance.** The default is per company lance, so a two-lance co-op company's offers carry about twice the tonnage.
- **Patrols** carry 1.5 × the rolled tonnage.
- **Filling.** Mechs are drawn by the contract's weight odds while each still fits under the target. When the next would overshoot, the force ends with the catalog mech that lands closest to the target, if that is closer than stopping short; a force always has at least one mech. The result is usually within 10–20 t of the target.
- **Pilots and reward.** The rating sets tonnage only. Pilot ranks roll separately, and the reward follows the difficulty, tonnage weighted by rank, as before, so a 200 t Elite force pays more than a 200 t Green one.

The numbers live in `MissionTuning`.

### Patrols (confirmed November 13, 2026)

A patrol sends the company across the battlefield: it visits a series of locations in order, past passive enemy groups, and leaves through an extraction zone. The battle is described in the [combat design](combat-design.md#patrols-confirmed-november-13-2026).

- **Who offers them.** A House or ComStar guarding a world, against pirates or a bordering rival, turns the job into a patrol half the time, about a quarter of House offers. Pirates never offer patrols.
- **Locations.** 2, 3 or 4 locations (25/45/30%), the last being the extraction zone, so 1–3 patrol points.
- **Opposition.** One enemy group on each leg between consecutive locations, none between the deployment and the first point, so the company can settle in before contact: the number of locations minus one. The patrol's 1.5 × tonnage is split evenly between the groups, and each is filled to its share with lighter weight odds, Light 55 / Medium 30 / Heavy 12 / Assault 3, with no limit on the number of mechs.
- **Terms.** The phone shows "CONTRACT Patrol", "PATROL 2 points + extraction · 2 enemy groups", and each enemy's group in the opposition list.

## Reputation (confirmed November 5, 2026)

The mercenary force has a reputation with each **tracked faction**: the five Great Houses (Federated Suns, Draconis Combine, Capellan Confederation, Free Worlds League and Lyran Commonwealth) and ComStar. Pirates have no collective reputation, since each band is independent; local forces (a governor's feud, a planetary concern below the Great Houses' notice) and Periphery states have none either. The tracked factions are a data list in `Factions`, so more can be added later.

- **Scale.** Reputation runs from −100 to +100. Every faction starts at 0, neutral, and reputation never drifts back on its own. It is stored in tenths of a point, so half-point changes stay exact, and shown in whole points rounded half away from zero.
- **Tiers.** Hated ≤ −60, Hostile ≤ −20, Neutral −19 to +19, Friendly ≥ +20, Honored ≥ +60. The numbers live in `ReputationTuning`.
- **Display.** The phone's mission overlay lists every tracked faction under its heading, as "Draconis Combine · Friendly +24", in red for Hated and Hostile, grey for Neutral and green for Friendly and Honored. The star map's hover tooltip shows your reputation with the system's owner (see [System owners](#star-travel-confirmed-october-25-2026)).
- **Effects.** Until negotiation exists, the only effect is that a faction that hates you posts no offers; it can still be fought. Reputation will shape negotiated terms once negotiation is designed.
- **Saves.** Version 7 saves record each faction's reputation in tenths, keyed by faction name. Older saves start neutral with everyone.

**Contract sides.** Every offer has an employer and an opponent, drawn by these odds (in `MissionTuning`) among the systems within the offer's reach. Two Houses **border** each other at a location when the other holds a system within 2 jumps of it.

- **Employer:** a Great House 75%, ComStar 10%, pirates 15%. Local forces never hire. A Hated faction is never drawn.
- **A Great House** fights another House 50%, local forces 20%, pirates 20% or ComStar 10%. Against another House it is a raid on a rival world bordering its own, or a defense of its own world against a bordering rival, 50/50. It raids local forces at a system it doesn't hold, defends its own world against pirates, and moves against ComStar on one of its own worlds.
- **ComStar** works at any held system, as a raid or defense 50/50, against pirates 40%, local forces 30%, or a House 30% (the system's House, or one bordering it; pirates if there is none).
- **Pirates** always raid a House's world, against local forces 60% or that House 40%.
- **Local forces** are generated per offer, such as "Galatea Planetary Guard" or "Governor Brandt's Household Troops".
- When the drawn employer has nothing suitable within reach, a random system's owner defends it against pirates; if that owner is untracked or hates you, pirates raid its local forces instead.

**Mission conduct** (scored by `Reputation.MissionChanges`, applied once when a mission is settled). Changes in points; they add up when a faction plays several roles, such as ComStar hiring the force.

| Fact | Employer | Opponent | ComStar | System owner |
|---|---|---|---|---|
| Success | +6 | | +1 | |
| Failure with reasonable effort | −2 | | | |
| Failure without effort, abandonment, missed deadline | −8 | | −1 | |
| Fighting them (base) | | −3 | | |
| Each enemy MechWarrior who survives | | +1 | +0.5 | |
| Each enemy mech allowed to flee | | +1 | +0.5 | |
| Each salvage pick taken over the allotment | −2 | | −1 | |
| Each building destroyed beyond 3 | | | −0.5 | −1 |
| Raiding for pirates | | | −2 | −2 |

- Opponent, honor, building and pirate-raid changes need a battle; abandoning or missing the deadline involves none. Only tracked factions change: pirates and local forces hold no reputation, though sparing them still earns ComStar's respect.
- **Reasonable effort** means the force lost armor equal to at least a quarter of the most armor its deployed mechs' tonnages allow (the sum of their location armor limits), whatever they actually carried, so fielding damaged or lightly armored mechs doesn't help.
- Building destruction is reported as zero until that mechanic exists. Fleeing enemy mechs count once they leave the battlefield, and their pilots as spared. A MechWarrior dies when the head is destroyed or the cockpit takes a critical hit, unless they ejected first (see [combat design](combat-design.md#location-loss-and-defeat)).

## Initial combat design (confirmed October 25, 2026)

The [combat design](combat-design.md) records the initial pausable real-time combat slice. A **Simulated Deployment** button in the hangar offers a randomized enemy or mirror match before force selection and terrain setup. Randomized enemies use fresh legal drafts to match the selected count and weight categories, including repeated categories; mirror matches copy mech types and pilot skills with both sides pristine. Simulation reports outcomes without advancing time or modifying the campaign; missions are fought through Quick Launch below. The initial design was confirmed October 25, 2026, and these simulation choices October 29, 2026. Numerical defaults remain authoring and tuning work.

## Quick Launch (confirmed November 9, 2026)

A **Quick Launch** button in the hangar, beside **Simulate Deployment**, deploys straight to the current mission, wherever the DropShip is: there is no travel. Accept the mission on the office phone first. The button is disabled, its tooltip saying why, with no accepted mission, while the hangar holds pending loadout changes (the battle's damage must land on applied loadouts), or while a quarterly payment is due.

- **Force selection** is the simulation's: choose 1–4 BattleMechs from your lance with ready MechWarriors and a terrain seed. The biome is fixed to the mission system's (Valley for a system without one). There is no opponent choice.
- **The battle** sets the selected mechs in their current condition against the offer's exact opposition: stock, undamaged and fully supplied, each pilot skilled by rank (see [combat tuning](combat-tuning.md#mission-opposition)). The combat menu's **Withdraw** ends the battle early.
- **Outcome.** Victory succeeds. Defeat, a draw or withdrawing fails.
- **Settlement** happens once, when the results appear, and the results list it:
  - Deployed mechs keep their battle damage, and every installed bin refills.
  - A MechWarrior whose head was destroyed or whose cockpit took a critical hit dies and leaves the cockpit. A struck cockpit keeps the mech not operational until Repair All mends it.
  - A MechWarrior who ejected (by order, by panicking, or automatically as the center torso was destroyed) comes home Injured, one injury of 10–20 days, and the mech's cockpit comes home damaged, needing Repair All.
  - A MechWarrior injured in the battle (by an ammunition explosion, or overheating with Life Support damaged; see [critical hits](combat-design.md#critical-hits-confirmed-november-14-2026)) is Injured for 10–20 days per injury, added together and counted from the settlement day.
  - **Hostages.** After a victory, each enemy MechWarrior whose surrender was accepted is ransomed by rank: Green 20,000, Regular 40,000, Veteran 80,000, Elite 150,000 C-bills (`SalvageTuning.Ransom`), listed as *Ransom for N hostages*. A defeat or a withdrawal leaves them behind. Keeping hostages in the campaign is deferred.
  - Success pays the offer's payment; failure pays no fee. [Insurance](#salvage-and-insurance-confirmed-november-17-2026) pays towards the battle's damage either way.
  - After a victory, the results offer the field's [salvage](#salvage-and-insurance-confirmed-november-17-2026) first, and settlement waits for **Claim Salvage** or **Take Nothing**.
  - Reputation changes by the [mission conduct](#reputation-confirmed-november-5-2026) table, with the armor the force lost, the enemy MechWarriors who survived and the salvage picks taken. Fleeing and buildings count as zero.
  - The mission is finished, and the calendar advances one day, with that day's events and any quarterly payment presented on returning to the hangar.
- Backing out of force selection, or leaving a battle for the main menu before the mission settles, settles nothing: the mission stays current.

## Salvage and insurance (confirmed November 17, 2026)

**Salvage** is chosen on the results of a won mission, before it settles. A failed mission has none, and a victory with nothing left to take settles at once.

- **What is on the field.** A surrendered mech stays with its pilot's side and a fled one is gone: neither can be salvaged, whole or in pieces. A patrol that succeeds by extracting while the opposition still has a mech fighting (not defeated and still armed) leaves no salvage at all, and the results say *No salvage: the opposition still holds the field*. Every working weapon, ammunition bin, heat sink and jump jet left on an enemy mech, in a location with structure left, is one pick and goes into hangar storage. A bin counts as one bin whatever shots are left. Damaged items, and everything in a lost location, are scrap.
- **Whole mechs.** An enemy mech left on the field and not destroyed (its center torso intact: disabled, headless, abandoned by an ejected pilot, or unarmed) can be claimed whole: Light 2 picks, Medium 3, Heavy 4, Assault 5. It joins the lance pool with no pilot, its stock loadout and its battle damage, its bins refilled. A mech claimed whole cannot also be stripped.
- **Picks.** The contract allows its salvage picks. Up to 3 more can be taken, each costing reputation as the [mission conduct](#reputation-confirmed-november-5-2026) table says (employer −2, ComStar −1). The results show the count and the cost of extra picks.
- **Choosing.** The results list each enemy mech with a toggle for the whole mech and one for each item. **Claim Salvage** settles with the choice; **Take Nothing** settles without salvage. Escape does not close the results until one is pressed. In co-op the leader chooses. The other players see the same list, read-only (labels, not toggles), under *The leader is choosing salvage…*, until the settlement arrives.

**Insurance** pays, on any outcome, the cost of repairing the damage the battle did, up to the contract's insurance limit.

- Each deployed mech's claim is what a full repair would cost after the battle less what it would have cost before. Destroyed weapons, bins, heat sinks and jump jets are priced at market price plus installation, as if no spare were in storage. Damage brought into the battle is not covered.
- The results list *Insurance paid*, with the claim and the limit, then each item and mech salvaged and any picks taken over the allotment.

The numbers live in `SalvageTuning`; insurance prices come from `Repairs.Estimate` and `EquipmentMarket`.

## Lances and player slots (confirmed November 9, 2026)

Co-op multiplayer is planned in [ADR 0003](adr/0003-cooperative-multiplayer.md). Its first step is in place: lances, player slots and campaign commands, used by single-player.

- **Lances.** A company has one lance per player slot, fixed when it forms; a single-player company has one. A lance holds up to four BattleMechs. Mechs in no lance are in the **lance pool**. A new company's mechs fill the lances in order, and saves from before lances form one lance from their first four mechs.
- **Deployment.** Only lance mechs deploy. Force selection offers the player's own lance and lists lance pool mechs as unavailable (*In the lance pool*).
- **Hangar.** The summary panel shows the selected mech's lance, with **Move to lance pool**, or **Add to Lance 1** for a lance pool mech. The button is disabled when the lance is full. Lance pool mechs are tagged *· Pool* on the roster buttons. A mech keeps its MechWarrior when it moves.
- **Commands.** Every campaign change a screen makes is a command sent from the local player's slot:
  - **Leader only:** travel, accepting and abandoning missions, advancing time, quarterly payments and mission settlement.
  - **Any player:** loadouts, pilot assignments and lance moves, but only on mechs in the player's own lance or the lance pool. Moving a MechWarrior off another mech needs control of both mechs. Any player may recruit; a player dismisses only unassigned MechWarriors and those piloting mechs they control.
  - A player adds mechs only to their own lance.

## Co-op sessions (confirmed November 9, 2026)

The second step of [co-op multiplayer](adr/0003-cooperative-multiplayer.md): the lobby, connection, drafts and strategy-layer sync.

- **Hosting.** **Host Co-op** on the main menu opens a form: your name, the port (24525 by default), and either a new company for 2 players or a saved co-op campaign. Resuming is disabled when there are no co-op saves; otherwise **Choose Save** opens the co-op save grid for loading, and the chosen save's players set the seats. The 3-player option is disabled until the catalog has enough MechWarriors. The lobby then lists one seat per lance, and **Start Campaign** is enabled once every seat is filled. The form and the lobby show where other players join: the internet address the home router reports over UPnP and this computer's local-network addresses, each with the port, plus **Copy address** for the internet one. Nothing outside the local network is asked. While the router is asked (up to about two seconds) the line reads *asking your router…*. If it does not answer, as when UPnP is off, the line says so and suggests finding the address on the router or by searching *what is my IP*. The router is asked once per run, and again on the next visit if it did not answer. Opening the lobby also asks the router over UPnP to forward the UDP port to this computer, and leaving the co-op game removes the forwarding (a crash can leave it behind, which is harmless). The form says the port is opened when you host; the lobby reads *Opening UDP port … on your router…*, then that the port is open, or that the router did not open it (UPnP may be off, or the port is forwarded to another computer) and the leader should forward it on the router.
- **Joining.** **Join Co-op** asks for your name and the leader's address and port. A player on another build, or joining when every seat is taken, is refused with the reason; a build refusal names both versions. Builds match by export version (shown on the main menu), so Linux, Windows and macOS exports of the same version play together; a build run from the editor or `scripts/mech run` is a development build and matches only itself. In the lobby, **Take seat** claims a free seat; a resumed campaign shows each seat's saved name until someone takes it.
- **Drafts.** Every player drafts their own lance at the same time. The leader's draft names the company, and the others' show *Draft your lance* with no name field. **Confirm Lance** locks a draft until every player has confirmed. The company then forms with each lance's mechs piloted by the MechWarriors drafted in their columns, and every player goes to the office.
- **Sync.** The leader's machine holds the campaign. Other players' changes are commands the leader carries out or refuses (refusals appear as *Not possible*). The campaign goes to every player at most five times a second while it changes. Pending hangar changes that still apply survive each update.
- **Leader-only controls for the other players:**
  - The calendar explains that only the leader advances time.
  - The phone's Accept and Abandon buttons are disabled.
  - The star map shows no Embark or Cancel Journey, and plotting explains that only the leader plots travel.
  - The filing cabinet only explains that the leader saves and loads.
  - When the quarterly bill falls due, it opens for them with a disabled *Waiting for leader* button, and it closes once the leader settles it.
  - Scheduled events are shown to every player.
- **Lances in the hangar.** The lance line names its player (*Lance 2 · Sam · 4/4*). Another player's lance mechs are read-only: no installing, removing, armor, Apply, pilot assignment or lance changes.
- **Missions.** Co-op missions are fought as below. Simulated deployments stay available, as local practice on each player's own lance.
- **Saving and loading.** Co-op saves are kept apart from solo saves (see Calendar and persistence). The leader's save records the players' names. The leader's filing cabinet saves and loads co-op saves only. Loading one mid-game always asks first, then ends the game for every player and reopens the host lobby, with the same name and port, holding the loaded save's seats; the other players join again. While the ended game is still telling the other players, the lobby reads *Ending the previous game…*.
- **Disconnects.** When a player drops, every player is told. Time cannot advance until they reconnect, and they rejoin through the lobby to their empty seat, receiving the campaign. When the leader leaves, the others return to the main menu with the reason and keep no copy of the campaign. Leaving through the main menu or the draft asks first; for the leader it ends the game for everyone.

## Co-op missions (confirmed November 9, 2026)

The third step of [co-op multiplayer](adr/0003-cooperative-multiplayer.md): missions fought together. The battle itself is described in the [combat design](combat-design.md#co-op-battles-confirmed-november-9-2026).

- **Scaling.** A co-op company's mission board is generated for all its lances: the challenge rating's default is per lance, and the reward follows the whole opposition's difficulty. The board is regenerated for every lance when the company forms.
- **Quick Launch** belongs to the leader. It is disabled, its tooltip saying why, unless a mission is accepted, no quarterly payment is due and every player is present. The other players' button explains that only the leader launches missions.
- **Force selection.** Quick Launch pulls every player into force selection, from wherever they are, by way of the hangar. Each player chooses 1–4 mechs from their own lance and presses **Ready**; a ready force is locked until **Not Ready**. The list under the choices shows every player's readiness. Only the leader sees the terrain seed and **Back to hangar**, which closes force selection for everyone. **Launch mission** is enabled once every other player is ready.
- **During the mission** the campaign is held: other players' campaign commands are refused until the battle's results are closed.
- **Autosave.** The leader's machine saves the campaign when the mission launches and again once it is settled, over the session's own co-op slot: the one it was resumed from or last saved to, keeping its name. A new co-op company gets a new slot, named after the company, on its first autosave.
- **Results.** Only the leader withdraws and settles. Every player sees the results and the leader's settlement; the others' return button reads *Waiting for leader*, and every player returns to the hangar when the leader leaves the results.
- **Disconnects.** A player who leaves force selection is no longer ready, and the leader cannot launch until they return. A player who drops out of the battle holds it paused. A returning player takes their seat through the lobby and goes straight back into force selection or the battle as it stands. Disconnects are handled as described below.

## Co-op disconnects (confirmed November 9, 2026)

The fourth step of [co-op multiplayer](adr/0003-cooperative-multiplayer.md). A player who quits, closes the game or loses their connection counts as disconnected; a dropped connection is noticed within ten seconds.

- **Outside a mission:** every player is told; time cannot advance and the leader cannot launch until the player returns. Everything else stays available.
- **In force selection or a battle:** a banner over the screen reads *Waiting for Sam to reconnect…* for every player, and clears when they return. A disconnected player holds the battle paused.
- **Stop Waiting.** During the battle the leader's banner offers **Stop Waiting**, which asks first and then ends the game for everyone. The battle is lost, but the campaign keeps the save made when the mission launched: Host Co-op and resume it to fight the mission again. The other players are told the leader stopped waiting.
- **Rejoining.** A player whose connection dropped is offered **Rejoin**, which reconnects to the same leader under the same name and retakes their seat as soon as it is free. They return to the campaign, force selection or the battle as it stands. Joining through **Join Co-op** and **Take seat** works too.
- **The leader leaving** (through the main menu, the draft or by quitting) ends the game for everyone. The other players return to the main menu told *The leader left the game.*, with no Rejoin; a dropped leader connection reads *The connection to the leader was lost.* Either way they keep no copy of the campaign.
