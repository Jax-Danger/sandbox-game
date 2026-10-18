# Changelog

Versioning: patch for minor changes, minor for big features/overhauls. No 1.0 until approved. (No git tags are pushed.)

## v0.14.4 — 2026-10-18
- Plaza paving gets sandstone flag texture aligned to the 1.5 m joint grid; asphalt now tiles at a true 6 m (RoundedBox UVs are in metres) with toned-down tar cracks.

## v0.14.3 — 2026-10-18
- Roads get a procedural asphalt texture (aggregate speckle, wear mottling, tar-sealed cracks, a repair patch) tiled square along the road; previously the concrete texture was stretched 6:1 across town avenues, showing as streaks.

## v0.14.2 — 2026-10-18
- Tractor wheel gets a real agricultural steel rim: rolled barrel with bead flanges, dished centre disc with a pressed ring, eight hand holes, ten wheel nuts and a hub cap on both faces (replaces the thin star-spoke ring).

## v0.14.1 — 2026-10-18
- Interiors: window reveals inside at every facade window (daylight glass, glazing bars, frame, sill) and the garage roller shutter's inside face with rails and drum box.

## v0.14.0 — 2026-10-18
- Building interiors are finished rooms: plank floors (houses) or large floor tiles (shops) on a grout bed, plaster wall linings, skirting boards, an off-white ceiling with cove moulding and a pendant light (was bare exterior-coloured boxes).

## v0.13.11 — 2026-10-18
- Town buildings get services: front downpipes with brackets and kick-out shoes, a caged wall lantern beside every door, and AC condensers with fan grilles and refrigerant lines on commercial side walls.

## v0.13.10 — 2026-10-18
- NPC skin tones capped below ~0.85 albedo so the palest preset no longer blows out to a white mask in full sun.

## v0.13.9 — 2026-10-18
- Basic cube has a raised picture-frame border on every face (dev-cube look); 2x2 panels get a framed edge on both faces (new Meshes.FramedBox).

## v0.13.8 — 2026-10-18
- Glove pass 2: denser palm and thumb sweeps (smoother silhouettes) and a tapered knuckle-guard pad instead of a blunt slab.

## v0.13.7 — 2026-10-18
- Pistol VM: front cocking serrations, slide/frame parting line, extractor claw, takedown lever, mag catch, flared magazine base pad and witness hole.

## v0.13.6 — 2026-10-18
- Buggy body panels: rubber cowl edge trim, moulded nose centre seam, chrome fuel filler and hood pins, tub rivet rows and pressed side character lines.

## v0.13.5 — 2026-10-18
- Jeep body panels: hood shut lines and raised spine, hood-side louvres, rear-quarter character line, rivet rows, tailgate shut line with chain stays; spare wheel now has a steel rim, hub cap and lug nuts.

## v0.13.4 — 2026-10-18
- Airboat deck: aluminium diamond treadplate, hinged bow hatch with seam and handle, four mooring cleats and a dark rub strake along the gunwale.

## v0.13.3 — 2026-10-18
- NPC ears are larger and flare ~25 degrees off the skull so they read from the front instead of hiding flush under the hair line.

## v0.13.2 — 2026-10-18
- Country roads continue the town avenues seamlessly: same 10 m width, height and asphalt material, overlapping the avenue end (no pale narrower slab / visible road joint); they also collide now.

## v0.13.1 — 2026-10-18
- Sidewalk ramps have real sloped collision (plaza and out-of-town ends) so players and vehicles roll up/down instead of hitting a hidden 16 cm step; new self-test.

## v0.13.0 — 2026-10-18
- Atmosphere: clear-weather fog pushed out to 100-284 m (town hills/trees no longer drowned), richer far-hill colour; dusk sky gets a taller forward-scattered glow, layered halo and a crisp limb-darkened sun disc, plus dithering against banding.

## v0.12.10 — 2026-10-18
- Entities (button, camera, light...) keep their real colours when frozen instead of the blue frozen-prop tint; button housing is brushed steel grey.

## v0.12.9 — 2026-10-18
- Red button dome is a smooth glossy cap (white albedo instead of the patterned default texture; lathe poles now use the axis normal everywhere, removing star-shaped specks on domes and nose cones).

## v0.12.8 — 2026-10-18
- Smoother gloves: Catmull-Rom densified finger paths, softer knuckle bumps, tapered knuckle pads; rolled sleeve hem with rounded lips and low-frequency folds (no jagged sleeve end).

## v0.12.7 — 2026-10-18
- RPG aim centres the optic eyepiece on the crosshair (dropped the fixed shoulder shift; sight bone compensates for rig pitch).

## v0.12.6 — 2026-10-18
- Toolgun rear screen shows content: glowing TOOLGUN label, charge bar and crosshair on a dark display instead of a blank pale glow (screens/vm/r2/raw/s7m-vm_toolgun.png vs s7a-tool-c.png)

## v0.12.5 — 2026-10-18
- Physgun rear is a knurled steel end ring with a glowing cyan power cell behind a six-rib vent instead of a plain dome; glowing cell windows between the cooling rings (screens/vm/r2/raw/s7k-phys-c.png vs s7i-phys-c.png)

## v0.12.4 — 2026-10-18
- Crowbar paint is chipped and scuffed (new Finish.Worn with ProcTex.WornPaint, wear concentrated along two knocked edges) instead of a glossy tube (screens/vm/r2/raw/s7h-crow-c.png vs s7a-crow-c.png)

## v0.12.3 — 2026-10-18
- Player avatar skin tone toned down so first-person driving arms no longer blow out to white in sunlight (screens/s7-seatfp-jeep-d.png)

## v0.12.2 — 2026-10-18
- Driver hands wrap the steering-wheel rim (grip variant of the lower arm with curled fingers and thumb); jeep gets a dashboard with chrome-bezel speedometer/tachometer (ticks, red needles), small gauges, toggle switches, glovebox and grab bar (screens/s7-seatfp-jeep-c.png vs s7-seatfp-jeep.png)

## v0.12.1 — 2026-10-18
- Airboat sits on its hull on land (skid rollers raised, shorter travel) instead of hovering; buggy's old painted shock sticks removed now that real coil-overs exist (screens/s7-boat-b-c.png vs s7-boat-a-c.png)

## v0.12.0 — 2026-10-18
- Vehicles get a real underbody: frame rails, cross-members, skid plate, exhaust with muffler and tip, fuel tank, plus coil-over shocks, buggy A-arms, jeep live axles with differentials and leaf springs, and brake drums that all follow each wheel's suspension travel and steering (screens/s7-jeep-b-c.png, s7-buggy-c-c.png, s7-buggy-d-c.png)

## v0.11.7 — 2026-10-18
- View-model gloves get a fine knit fabric texture with stitched side seams (ProcTex.GloveKnit) instead of plain clay-like shading (screens/vm/r2/raw/s7g-pistolhand.png; before s7a-pistolhand.png)

## v0.11.6 — 2026-10-18
- Wooden VM parts get a fine walnut grain texture; shotgun receiver gains ejection port with bolt, loading port, trigger-group pins, side seams, top-tang safety and tang plate (screens/vm/r2/raw/s7f-sg-c.png; before s7a-shotgun-c.png)

## v0.11.5 — 2026-10-18
- SMG view model: receiver seam, takedown pins, ejection port with bolt face, forward-assist bump, collapsed wire stock with rubber butt, red dot rear lens, windage turret and crossbolt (screens/vm/r2/raw/s7d-smg-c.png; before s7a-smg-c.png)

## v0.11.4 — 2026-10-18
- Traffic cones are bottom-heavy (ModelDef.Ballast lowers the body origin/centre of mass 0.24 m below the geometric centre) and rock back upright instead of toppling; self-test tips one 32 degrees and checks it recovers

## v0.11.3 — 2026-10-18
- Town sidewalks get a raised kerb, expansion joints every 1.5 m and sloped dropped-kerb ends onto the plaza and the grass instead of blunt slab ends (screens/s7-walkend-c.png, s7-walkend-e.png; before s7-walkend-a/b.png)

## v0.11.2 — 2026-10-18
- NPC heads are sculpted (Meshes.Sculpt): tapered jaw and rounded chin, cheekbones, brow ridge, eye sockets, temples; shell ears with a dished bowl and lobe; slightly larger eyes (screens/s7-face-ab.png before, s7-face-d-crop2.png after)

## v0.11.1 — 2026-10-18
- Sign/plate text uses real Inter Bold outlines (curves, counters) instead of the 5x7 block font; PUSH plate text sits proud of the plate (screens/s7-button-d-crop.png)

## v0.11.0 — 2026-10-18
- Custom sky shader replaces the engine's procedural sky: blue zenith to fog-matched horizon (no greenish dusk, no horizon seam), orange sun glow at dawn/dusk, stars and a moon at night; horizon skirt follows the fog colour (screens/s7-night-d.png, s7-dusksun-e.png)

## v0.10.14 — 2026-10-18
- Shipment world models measure every vertex and re-fit once, so the RPG warhead now sits fully inside the lid (screens/s7-ship-top.png)

## v0.10.13 — 2026-10-18
- Flatgrass perimeter walls read as poured panels: pilasters every 12 m, formwork joints, dark skirting and an orange shoulder band.

## v0.10.12 — 2026-10-18
- Lake surface gets a procedural wind-ripple texture with glint streaks; review screenshot states no longer autosave, so shot props stop piling up in the demo worlds.

## v0.10.11 — 2026-10-18
- Sofa scatter pillows are plump rounded-square sweeps instead of hexagon-looking box cushions.

## v0.10.10 — 2026-10-18
- Floor lamp uses a box collider so it stands on its base instead of tipping over on spawn.

## v0.10.9 — 2026-10-18
- Bed throw is cream knit with two deep-red stripes that stand proud of it (the old grey throw z-fought into a road-like dashed line).

## v0.10.8 — 2026-10-18
- Ramp prop is a steel deck between raised side rails with anti-slip cleats and end lips, instead of a plain slab.

## v0.10.7 — 2026-10-18
- Apartments: French door with a Juliet balcony on turned balusters in the upper centre bay, and flower boxes under the upper windows.

## v0.10.6 — 2026-10-18
- General Store gets striped canvas awnings with scalloped valances over each window and a fruit stand by the door.

## v0.10.5 — 2026-10-18
- Police station gets a flagpole on the front lawn: concrete footing, tapered pole, gold finial and a rippled blue-and-white flag.

## v0.10.4 — 2026-10-18
- ATM kiosk back and sides: service door with lock, wraparound brand stripe, louvre vents and a steel plinth.

## v0.10.3 — 2026-10-18
- Pole spawn icon lies diagonally so it fills its tile instead of reading as a hairline.

## v0.10.2 — 2026-10-18
- Houses get a front garden: white picket fence with a gate gap on the path and round shrubs along the facade.

## v0.10.1 — 2026-10-18
- Horizon skirt: a fully fogged ring past the fog end follows the camera and hides the sky's brown below-horizon band from the air on every map.

## v0.10.0 — 2026-10-18
- Downtown outskirts: farmland beyond the play square, a merged tree line, low hills in the fog and country roads continuing each avenue, so the town no longer ends at a sky edge.

## v0.9.20 — 2026-10-18
- Button push-station: PUSH plate no longer mirrored, and a second plate on the back.

## v0.9.19 — 2026-10-18
- Sun shadow strength 0.82 so outdoor shadow cores keep some skylight instead of reading inked-in.

## v0.9.18 — 2026-10-18
- Hemisphere ambient a little stronger and less blue so shadowed sides read neutral instead of navy.

## v0.9.17 — 2026-10-18
- Shipment crates measure the weapon model's real mesh bounds, centre it on the lid, sit it flush and shrink long guns to fit.

## v0.9.16 — 2026-10-18
- Shotgun view model: left hand grips the rear half of the pump, shortening the left-arm reach.

## v0.9.15 — 2026-10-18
- Bouncy Ball entity gets a smooth high-res shell, yellow seam band and valve like its icon; gallery set gal_ent_<ids> for entity close-ups.

## v0.9.14 — 2026-10-18
- Rolling Hills boulders are chiselled, flat-shaded plane-cut rocks instead of smooth blobs; trees no longer spawn inside boulders.

## v0.9.13 — 2026-10-18
- Billboard poster: two-step sky gradient, rolling hills behind the meadow and two puffy clouds.

## v0.9.12 — 2026-10-18
- NPC and player shirts get five buttons following the placket curve, a pocket flap and a pocket button.

## v0.9.11 — 2026-10-18
- Rolling Hills lake shore is clipped at its height contour, so the sand band has a smooth curved edge instead of stair-stepped grid triangles; boulders toned darker.

## v0.9.10 — 2026-10-18
- Jeep and buggy headlamps: chrome bezel ring, fresnel rings moulded in the lens and a bulb reflector; lens no longer blows out to flat white.

## v0.9.9 — 2026-10-18
- Jeep windshield is tinted glass with two sheen streaks, a split-screen centre post and parked wiper arms (was an opaque light-blue slab).

## v0.9.8 — 2026-10-18
- Hostile walkers carry a red crowbar (claw hook and steel chisel end) in the right hand.

## v0.9.7 — 2026-10-18
- Town materials pass: garage roller shutter and frame toned from glaring white to galvanised grey (grimier bottom slats), darker concrete apron, sidewalks toned down, and building paths are five concrete flags with dark joints on a gravel bed.

## v0.9.6 — 2026-10-18
- Light entity: frosted white globe (was showing the crate wood texture) inside a six-rib steel wire cage with two hoops and a ribbed brass screw collar.

## v0.9.5 — 2026-10-18
- Camera entity: carry handle, knurled focus and zoom rings, red tally LED, flip-out side monitor, rear battery with latch, tripod plate, lens ring and glint; deeper, dimmer lens glass; no crate wood texture.

## v0.9.4 — 2026-10-18
- Button entity is an industrial push station: crisp steel housing (no crate wood texture), yellow top plate with four screws, black/yellow hazard collar, PUSH plate and a plinth around the red mushroom button.

## v0.9.3 — 2026-10-18
- Potted plant rebuilt: terracotta pot with rolled rim, foot and saucer, mulch chips, three arching stems and ~45 individual V-folded, drooping leaf blades in two greens (replaces three foliage blobs).

## v0.9.2 — 2026-10-18
- Explosive barrel wears curved yellow hazard labels with black border stripes and a flame mark on both sides (Meshes.CylPatch for decals that hug cylinders).

## v0.9.1 — 2026-10-18
- Police NPCs wear their kit: holstered pistol world model on the right hip, side-handle baton on a belt ring on the left, gold shield badge and silver name bar; police shirt toned to navy. Gallery state gal_police.

## v0.9.0 — 2026-10-18
- Weapon world models: guns are built at real size from their sculpted view-model parts (WorldWeapons); RP weapon shipments now show the weapon strapped on the lid (long guns laid diagonally). Gallery state gal_ship / gal_ship_close.

## v0.8.30 — 2026-10-18
- Crowbar view model: steel collar above the grip tape, worn bare-steel band at the bend, claw nail slot and chisel bevel line.

## v0.8.29 — 2026-10-18
- Town colour pass: lawn toned from bright green to a natural muted turf and plaza tree crowns use softer olive leaf greens (matching the Rolling Hills pass).

## v0.8.28 — 2026-10-18
- Physics gun is slimmer: body, rings, collar, prongs, hoses and grip hand ~23% narrower and the bulbous rear dome shortened, so it no longer fills the lower right of the screen.

## v0.8.27 — 2026-10-18
- Arrow sign stands on a galvanised post with cap, clamps, back rails and a weighted foot (collider now 0.9 x 2.2 x 0.4) instead of a loose panel that fell flat.

## v0.8.26 — 2026-10-18
- Icons frame on the view-axis silhouette radius (bouncy ball no longer clipped into a faceted square); bouncy ball icon gets a seam band and valve; explosive barrel label faces the camera.

## v0.8.25 — 2026-10-18
- Hills tree crowns use deeper, less saturated leaf greens to sit with the toned-down turf

## v0.8.24 — 2026-10-18
- Rolling Hills turf toned down from neon green to a natural meadow green

## v0.8.23 — 2026-10-18
- Explosive barrel entity icon uses the steel drum mesh

## v0.8.22 — 2026-10-18
- Pole gets end caps and two clamp bands instead of a bare cylinder

## v0.8.21 — 2026-10-18
- Barrel is a steel drum now: recessed heads inside rolled chimes, two swaged rolling hoops and shallow ribs

## v0.8.20 — 2026-10-18
- Vehicles tab shows rendered thumbnails of the buggy, jeep and airboat (same meshes as in game) instead of a generic car glyph

## v0.8.19 — 2026-10-18
- Rolling Hills trees get three forked branches running up into the crown

## v0.8.18 — 2026-10-18
- Rolling Hills: grass tufts scattered over the hills, the spawn deck is gapped boards on a rim frame with corner posts, darker boulders

## v0.8.17 — 2026-10-18
- Rolling Hills lake: sandy shoreline band draped over the terrain at the waterline, reed clumps with seed heads, a plank jetty on round piles, deeper water colour; shot state hillsat_x_y_z_yaw_pitch

## v0.8.16 — 2026-10-18
- Garage gets a service bay: ribbed roller shutter in a steel frame with hood box and guide rails, handle, yellow bollards and a concrete apron

## v0.8.15 — 2026-10-18
- Plaza fountain: upper tier with a slim stem, small bowl and finial, eight water sheets spilling from the bowl, pools in both bowls, ripple rings and deeper water colour

## v0.8.14 — 2026-10-18
- House roofs get shingle courses: a shadow line every course up both slopes with staggered butt joints

## v0.8.13 — 2026-10-18
- Houses: the porch canopy is a solid shingled slab with a white fascia and soffit on braced brackets (the old one-sided quad was invisible, leaving bare brackets), plus a front step and door mat

## v0.8.12 — 2026-10-18
- Spawn-menu icons for signs and the billboard now show their printed faces instead of the backs/edges

## v0.8.11 — 2026-10-18
- Vehicle seat remodelled as a bucket seat (fluted insert, side bolsters, contrast stitching, headrest posts, steel pan, slide rails and lever); trash can gets corrugation hoops, foot ring, drop handles on lugs and a lid grab handle

## v0.8.10 — 2026-10-18
- NPC trousers get side seams and a pressed front crease; shoes get a tongue, laces, heel counter and toe-cap seam; gallery state gal_npcbody for full-body review

## v0.8.9 — 2026-10-18
- Town plaza paving: 1.5 m joint grid, darker kerb border and two rings of granite setts around the fountain

## v0.8.8 — 2026-10-18
- Town: commercial buildings get plinths, corner quoin pilasters, cornice bands and belt courses on all four sides; plaza lamps are Victorian lanterns (fluted foot, collar, ladder bar, four-post glowing lantern under a peaked cap)

## v0.8.7 — 2026-10-18
- Jeep: door cut-out trim, side steps, hinge pegs, fuel filler cap, jerrycan X pressings and triple handle

## v0.8.6 — 2026-10-18
- Airboat: wire fan guard (12 spokes, two inner hoops), three linked rudder blades with a tie bar, grey rub rails along the sheer, non-slip deck pad, bow cleat and red/green navigation lights; smaller NPC collar flaps

## v0.8.5 — 2026-10-18
- NPC faces: white eyeballs with dark irises and catch-lights, upper lid creases and lower lids, heavier brows, shaped lips with a mouth line, slimmer nose, sideburns, a side-parted fringe and a hairline set back off the brows

## v0.8.4 — 2026-10-18
- Big and small crates are real crates now: inset core, proud edge battens on all 12 edges and a diagonal brace on every face

## v0.8.3 — 2026-10-18
- Remodelled street lamp (bolted base plate, cast foot with access hatch, tapered shaft with collars, scroll-braced arm, cobra head with glowing lens), gas cylinder (domed shoulder, foot ring, label bands, valve guard, brass valve with hand wheel) and gear (rim, web with lightening holes, keyed hub, bolts, trapezoid teeth); gallery close-ups aim at the subject

## v0.8.2 — 2026-10-18
- Remodelled cinder block (open cores), Jersey barrier (light concrete, fork slots, reflectors, connector loops, stencil), steel pipe (threads, hex coupling, cap, seam, band), sofa (frame, rolled arms, piped cushions, feet, pillows), office chair (contoured mesh back, lumbar, spine, star base with casters, arm pads), floor lamp (drum shade, rims, glowing bulb, pull chain), billboard (lettered poster, catwalk, flood lights, bracing).

## v0.8.1 — 2026-10-18
- Tyres get real tread: staggered blocks + centre rib on car/buggy/jeep/small wheels, chevron lugs on the tractor wheel (was a smooth lathe).

## v0.8.0 — 2026-10-18
- Town facades: real window assemblies on every wall (reveal, casing, sash, glazing bars, sheen, sloped sill with drip, lintel/keystone, louvred shutters on houses), lap siding + corner boards + stone plinth + number plaques on houses, pilasters/plinth/dentil cornice and lettered signs (POLICE, GENERAL STORE, APARTMENTS, GARAGE) on commercial blocks; 5x7 Glyphs font; townat_x_y_z_yaw_pitch shot state.

## v0.7.2 — 2026-10-18
- Hands spawn-menu icon: a modelled open hand (palm creases, knuckles, nails, thumb, shirt cuff) turned to the icon camera, instead of the glove seen edge-on as a dark blob.

## v0.7.1 — 2026-10-18
- Buggy rear arches rebuilt at true ride height (flared arch, rolled lip, side skirt into the tub; no longer floating like a spoiler); tail lights and plate lettering; gal_car_<kind>_<angle>_<dist> shot state.

## v0.7.0 — 2026-10-18
- Remodelled pallet (gapped boards, blocks, nails, stamp), I-beam (root fillets, bolt holes, stencil), sash window (two sashes, glazing bars, sill, lock, lifts), sawhorse (2x4 legs, brackets, striped board), stop/arrow/warning signs (lettering, borders, telespar post, clamps), TV (picture on, pedestal), bed (posts, panels, duvet, pillows, throw), fridge (doors, gaskets, bar handles, grille); gal_one_<ids> close-up shot state.

## v0.6.8 — 2026-10-18
- NPC shirt colour mismatch fixed (aim highlight now tints the whole NPC/model, not just the root mesh; gallery shots never highlight; limbs use matte cloth instead of rubber albedo); pointed fold-down collar flaps.

## v0.6.7 — 2026-10-18
- NPCs/avatar: four separate curled fingers on each hand (was a mitten slab), shirt collar, button placket and chest pocket.

## v0.6.6 — 2026-10-18
- Flatgrass: ramp decks get anti-slip strips and yellow edge bands; all walls get coping caps, the mid wall a plinth.

## v0.6.5 — 2026-10-18
- Rolling Hills: ~80 leaf-clump trees scattered over the hills (trunks collide; none in the lake or on spawn), boulders are lumpy rocks instead of rotated boxes.

## v0.6.4 — 2026-10-18
- Tool gun view model: sculpted shell tapering into the emitter, panel seam, side vent slots, top fin rail and three glowing emitter coils.

## v0.6.3 — 2026-10-18
- Town plaza: laws board on turned posts with finials, framed back panel and a little roof; ATM is a rounded kiosk with screen bezel and hood, angled keypad shelf, card and cash slots. New screenshot states `townatm`, `townboard`.

## v0.6.2 — 2026-10-18
- Foliage: new noise-displaced leaf-clump mesh; town trees are four clustered clumps in three greens with visible branches, potted plant uses the same clumps over soil.

## v0.6.1 — 2026-10-18
- Jeep: seven-slot grille, side mirrors on stalks, tail lamps, hood latches, jerrycan. Airboat: engine with finned heads and exhaust stacks, fan guard spokes.

## v0.6.0 — 2026-10-18
- Town buildings overhaul: houses get pitched gable roofs with eaves, fascia, ridge cap, gable walls, brick chimneys and porch canopies on brackets; commercial buildings get parapets all round with coping, a string course, entrance awnings, rooftop AC units and vents. (Overnight model pass from v0.5.1–0.5.10 rolled up into this minor.)

## v0.5.10 — 2026-10-18
- Buggy: proper dune-buggy body — raised nose cowl with dash, slim rear wheel arches, side nerf bars, front shocks, rear flat-four engine with finned heads, air filter, twin exhausts and a number plate.

## v0.5.9 — 2026-10-17
- Jump height is now identical at any frame rate (1.056 m at 30 and 144 FPS; was 1.27 vs 1.10): gravity applies on the take-off frame too. Self-test checks the peaks match.

## v0.5.8 — 2026-10-17
- Spawn-menu icons: tight framing from the real rendered bounds (thin and tall items fill the tile), fill + rim lights so dark guns read, weapons shown in clean side profile instead of end-on.

## v0.5.7 — 2026-10-17
- NPCs/avatar: slimmer, lower deltoids and sloping trapezius yokes (no more puffed knobs above the arms), chest bridges into the shoulders; thighs extend up into the hips (no dimple at the top); sleeve roll tapers into the forearm.

## v0.5.6 — 2026-10-17
- Props: office chair with five-star base, casters, armrests and back support; desk with inset drawer fronts, pulls, modesty panel and feet; metal crate with inset panels, corner caps and handles; fence with pointed pickets and capped posts; cinder block with rounded cores.

## v0.5.5 — 2026-10-17
- Props: wooden chair with curved top rail, spindle back, stretchers and seat cushion; table with apron rails and edge-banded top; bookshelf with crown, plinth and rows of individual books; filing cabinet with inset drawer fronts, label holders and bar pulls; billboard with framed inset face, round posts with cross-bracing, catwalk and lamps.

## v0.5.4 — 2026-10-17
- View-model hands: open-hand fingers fan apart toward the tips, curl progressively toward the pinky, slimmer tapered digits — four real fingers instead of a mitten.

## v0.5.3 — 2026-10-17
- Flatgrass ramps are solid: the painted deck now sits on an inset concrete wedge down to the ground (no more floating planks).

## v0.5.2 — 2026-10-17
- RPG view model: optic rebuilt as a lathed scope (eyecup, coated eyepiece, turret ring, reticle with chevron), wide mount plate instead of a thin stalk; tube rear is a rolled lip into a sooty recessed bore instead of a flat cap.

## v0.5.1 — 2026-10-17
- Roleplay: **K** on a door you own gives a key to the nearest citizen / police / friendly NPC within 8 m; **Shift+K** takes all keys back. The door hint shows how many keys are shared. Self-test added.
- Version shown on the main menu (`PROWL ENGINE · PHYSICS SANDBOX · v0.5.1`); version lives in `Sandbox.csproj`.

## v0.5.0
- See README "New in v0.5.0".
