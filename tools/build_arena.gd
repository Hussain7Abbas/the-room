extends SceneTree
## Builds maps/arena/Arena.tscn: the 60 x 60 m arena (1.5x the old 40 m room in each direction).
## Re-run after changing the layout:  godot --headless --path . --script res://tools/build_arena.gd
## The output is a normal scene: open it in the editor to nudge things by hand afterwards.
##
## Layout (top-down, +x east, +z south):
##   centre plaza     cobblestone, Golden Knife plinth, ring of barriers, 4 street lamps
##   4 avenues        9 m wide lanes from the plaza to the walls, barriers alternating along them
##   NE (+x,+z)       crate yard: stacked cargo crates forming alleys
##   NW (-x,+z)       barrier maze: zig-zag barrier lines with tall utility boxes
##   SW (-x,-z)       boulder field: scattered rocks of varied height
##   SE (+x,-z)       depot: a raised metal deck with ramps, barrel clusters
##   perimeter lane   4 m clear lane inside the walls, holding all 16 spawn points
## Props are CC0 models from Poly Haven (assets/props/polyhaven, see CREDITS.md). Every prop gets
## a simple box collider sized from its measured bounds, never a mesh collider: sweeps (lunge,
## dodge, drop kick) stay reliable, and the dedicated server needs nothing but primitives.

const OUT := "res://maps/arena/Arena.tscn"
const HALF := 30.0          # inner half-size of the playable floor
const WALL_H := 8.0
const WALL_T := 2.0
const PROP_LIMIT := 26.0    # props stay inside this, leaving the perimeter lane clear
const AVENUE_HALF := 4.5    # avenues are kept clear this far either side of each axis

var arena: Node3D
var props: Node3D
var rng := RandomNumberGenerator.new()
var bounds_cache := {}
var placed: Array[Vector3] = []   # footprint centres, for spacing checks

var mat_floor: StandardMaterial3D
var mat_plaza: StandardMaterial3D
var mat_wall: StandardMaterial3D
var mat_metal: StandardMaterial3D
var mat_containers: Array[StandardMaterial3D] = []

func _init() -> void:
	rng.seed = 7
	arena = Node3D.new()
	arena.name = "Arena"
	arena.set_meta("half_extent", HALF)

	mat_floor = _tex_material("worn_asphalt", 0.25)
	mat_plaza = _tex_material("cobblestone_square", 0.35)
	mat_wall = _tex_material("preconcrete_wall_001", 0.2)
	mat_metal = _tex_material("rusty_metal_02", 0.3)
	for tint in [Color(0.78, 0.32, 0.26), Color(0.3, 0.45, 0.72), Color(0.36, 0.6, 0.38)]:
		var m := _tex_material("rusty_metal_02", 0.35)
		m.albedo_color = tint
		mat_containers.append(m)

	_environment()
	_ground()
	_walls()
	props = _add(Node3D.new(), arena, "Props")
	_plaza()
	_avenues()
	_structures()
	_crate_yard()
	_barrier_maze()
	_boulder_field()
	_depot()
	_spawns()

	var packed := PackedScene.new()
	var err := packed.pack(arena)
	if err == OK:
		err = ResourceSaver.save(packed, OUT)
	print("[build_arena] %s -> %s (%d props)" % [error_string(err), OUT, props.get_child_count()])
	arena.free()
	quit()

# ---------------------------------------------------------------- building blocks

func _add(node: Node, parent: Node, node_name: String) -> Node:
	node.name = node_name
	parent.add_child(node)
	node.owner = arena
	return node

func _tex_material(tex: String, scale: float) -> StandardMaterial3D:
	var base := "res://assets/environment/textures/%s/%s_" % [tex, tex]
	var m := StandardMaterial3D.new()
	m.albedo_texture = load(base + "diff_1k.jpg")
	m.normal_enabled = true
	m.normal_texture = load(base + "nor_gl_1k.jpg")
	m.roughness_texture = load(base + "rough_1k.jpg")
	m.roughness_texture_channel = BaseMaterial3D.TEXTURE_CHANNEL_RED
	# World triplanar: the texture tiles by metres on every box, whatever its size.
	m.uv1_triplanar = true
	m.uv1_world_triplanar = true
	m.uv1_scale = Vector3.ONE * scale
	# Anisotropic, so Settings → Graphics → Texture Quality's filtering level applies.
	m.texture_filter = BaseMaterial3D.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS_ANISOTROPIC
	return m

## A solid block (visual + collider) with its bottom-centre at `pos`.
func _block(block_name: String, parent: Node, pos: Vector3, size: Vector3, mat: Material, rot_y := 0.0, rot_x := 0.0) -> void:
	var pivot := _add(Node3D.new(), parent, block_name) as Node3D
	pivot.position = pos
	pivot.rotation = Vector3(rot_x, rot_y, 0)
	var mesh := MeshInstance3D.new()
	mesh.mesh = BoxMesh.new()
	(mesh.mesh as BoxMesh).size = size
	mesh.material_override = mat
	mesh.position = Vector3(0, size.y / 2, 0)
	_add(mesh, pivot, "Mesh")
	var body := _add(StaticBody3D.new(), pivot, "Collision") as StaticBody3D
	var shape := CollisionShape3D.new()
	shape.shape = BoxShape3D.new()
	(shape.shape as BoxShape3D).size = size
	shape.position = Vector3(0, size.y / 2, 0)
	_add(shape, body, "Shape")

func _model_bounds(model: String) -> AABB:
	if bounds_cache.has(model):
		return bounds_cache[model]
	var scene: Node = load(_model_path(model)).instantiate()
	var aabb := AABB()
	var first := true
	for mi in scene.find_children("*", "MeshInstance3D", true, false):
		var t := Transform3D.IDENTITY
		var n: Node = mi
		while n != null and n is Node3D:
			t = (n as Node3D).transform * t
			n = n.get_parent()
		var box: AABB = t * (mi as MeshInstance3D).get_aabb()
		aabb = box if first else aabb.merge(box)
		first = false
	scene.free()
	bounds_cache[model] = aabb
	return aabb

func _model_path(model: String) -> String:
	return "res://assets/props/polyhaven/%s/%s.gltf" % [model, model]

## A Poly Haven prop at `pos` (on the ground), scaled uniformly, with a box collider fitted to it.
func _prop(model: String, pos: Vector3, scale: float, rot_y: float, lift := 0.0) -> Node3D:
	var aabb := _model_bounds(model)
	var pivot := _add(Node3D.new(), props, "%s_%d" % [model, props.get_child_count()]) as Node3D
	pivot.position = pos + Vector3(0, lift, 0)
	pivot.rotation = Vector3(0, rot_y, 0)
	var visual: Node3D = load(_model_path(model)).instantiate()
	visual.scale = Vector3.ONE * scale
	visual.position = Vector3(0, -aabb.position.y * scale, 0)  # sit exactly on the ground
	_add(visual, pivot, "Model")
	var body := _add(StaticBody3D.new(), pivot, "Collision") as StaticBody3D
	var shape := CollisionShape3D.new()
	shape.shape = BoxShape3D.new()
	(shape.shape as BoxShape3D).size = aabb.size * scale
	var c := aabb.get_center() * scale
	shape.position = Vector3(c.x, aabb.size.y * scale / 2, c.z)
	_add(shape, body, "Shape")
	placed.append(pos)
	return pivot

func _clear_of_avenues(p: Vector3) -> bool:
	return absf(p.x) > AVENUE_HALF + 1.5 and absf(p.z) > AVENUE_HALF + 1.5 and p.length() > 10.0

func _spaced(p: Vector3, spacing: float) -> bool:
	for q in placed:
		if Vector2(p.x - q.x, p.z - q.z).length() < spacing:
			return false
	return true

# ---------------------------------------------------------------- layout

func _environment() -> void:
	var env := Environment.new()
	env.background_mode = Environment.BG_COLOR
	env.background_color = Color(0.07, 0.08, 0.1)
	env.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	env.ambient_light_color = Color(1, 1, 1)
	env.ambient_light_energy = 0.6
	env.tonemap_mode = Environment.TONE_MAPPER_FILMIC
	# Light distance fog: across 60 m the far side reads as depth instead of clutter.
	env.fog_enabled = true
	env.fog_light_color = Color(0.62, 0.72, 0.86)
	env.fog_density = 0.0025
	env.fog_sky_affect = 0.0
	var world := WorldEnvironment.new()
	world.environment = env
	world.set_script(load("res://maps/arena/RoomEnvironment.cs"))
	_add(world, arena, "WorldEnvironment")

	var sun := DirectionalLight3D.new()
	sun.rotation_degrees = Vector3(-50, -35, 0)
	sun.light_energy = 1.15
	sun.shadow_enabled = true
	sun.directional_shadow_max_distance = 60.0
	_add(sun, arena, "Sun")

func _ground() -> void:
	_block("Floor", arena, Vector3(0, -1, 0), Vector3(HALF * 2 + WALL_T * 2, 1, HALF * 2 + WALL_T * 2), mat_floor)
	# Plaza paving: a thin visual disc (no collider: the floor's collider is right under it).
	var disc := MeshInstance3D.new()
	var cyl := CylinderMesh.new()
	cyl.top_radius = 9.5
	cyl.bottom_radius = 9.5
	cyl.height = 0.04
	cyl.radial_segments = 48
	disc.mesh = cyl
	disc.material_override = mat_plaza
	disc.position = Vector3(0, 0.01, 0)
	_add(disc, arena, "PlazaPaving")

func _walls() -> void:
	var walls := _add(Node3D.new(), arena, "Walls")
	var length := HALF * 2 + WALL_T * 2
	var off := HALF + WALL_T / 2
	_block("WallNorth", walls, Vector3(0, 0, -off), Vector3(length, WALL_H, WALL_T), mat_wall)
	_block("WallSouth", walls, Vector3(0, 0, off), Vector3(length, WALL_H, WALL_T), mat_wall)
	_block("WallEast", walls, Vector3(off, 0, 0), Vector3(WALL_T, WALL_H, length), mat_wall)
	_block("WallWest", walls, Vector3(-off, 0, 0), Vector3(WALL_T, WALL_H, length), mat_wall)

func _plaza() -> void:
	# The Golden Knife sits at (0, 1.5, 0) above this plinth (core/MatchServer.cs).
	var plinth := _add(Node3D.new(), arena, "CenterPlinth") as Node3D
	var mesh := MeshInstance3D.new()
	var cyl := CylinderMesh.new()
	cyl.top_radius = 2.0
	cyl.bottom_radius = 2.2
	cyl.height = 1.0
	cyl.radial_segments = 8
	mesh.mesh = cyl
	mesh.material_override = mat_metal
	mesh.position = Vector3(0, 0.5, 0)
	_add(mesh, plinth, "Mesh")
	var body := _add(StaticBody3D.new(), plinth, "Collision") as StaticBody3D
	var shape := CollisionShape3D.new()
	var cs := CylinderShape3D.new()
	cs.radius = 2.1
	cs.height = 1.0
	shape.shape = cs
	shape.position = Vector3(0, 0.5, 0)
	_add(shape, body, "Shape")

	# Ring of barriers with gaps to get in: cover around the prize.
	for k in 6:
		var a := deg_to_rad(30.0 + 60.0 * k)
		var p := Vector3(cos(a), 0, sin(a)) * 6.0
		_prop("concrete_road_barrier", p, 1.4, -a + PI / 2)
	for k in 4:
		var a := deg_to_rad(45.0 + 90.0 * k)
		_prop("street_lamp_01", Vector3(cos(a), 0, sin(a)) * 8.5, 1.3, -a)

func _avenues() -> void:
	# Cover along each avenue, alternating sides, so a sprint down the lane still has breaks.
	var dirs := [Vector3(1, 0, 0), Vector3(-1, 0, 0), Vector3(0, 0, 1), Vector3(0, 0, -1)]
	for dir in dirs:
		var side := Vector3(dir.z, 0, -dir.x)
		var i := 0
		for d in [13.0, 21.0]:
			var p: Vector3 = dir * d + side * (2.8 if i % 2 == 0 else -2.8)
			var model := "concrete_road_barrier_02" if i % 2 == 0 else "concrete_road_barrier"
			_prop(model, p, 1.45, atan2(dir.x, dir.z) + PI / 2)
			i += 1

## Big cover: shipping containers (some stacked) and an L-shaped concrete wall. Placed before the
## small props, which then keep their distance from these.
func _structures() -> void:
	var s := _add(Node3D.new(), arena, "Structures")
	# Crate yard (+x,+z): a stacked pair and a single.
	_container(s, Vector3(19, 0, 12), 0.0, 1)
	_container(s, Vector3(12, 0, 22), PI / 2, 2)
	# Depot (+x,-z): one along the east lane edge, next to the deck.
	_container(s, Vector3(25, 0, -17), PI / 2, 1)
	# Barrier maze (-x,+z): a landmark at its far corner.
	_container(s, Vector3(-21, 0, 24), 0.0, 1)
	# Boulder field (-x,-z): an L-shaped wall to fight around.
	_l_wall(s, "WallL1", Vector3(-22, 0, -14), 0.0)

func _container(parent: Node, pos: Vector3, rot_y: float, stack: int) -> void:
	var size := Vector3(6.1, 2.6, 2.44)
	for level in stack:
		var m: StandardMaterial3D = mat_containers[rng.randi() % mat_containers.size()]
		_block("Container%d" % parent.get_child_count(), parent, pos + Vector3(0, size.y * level, 0), size, m, rot_y + (0.08 if level == 1 else 0.0))
	var along := Vector3(cos(rot_y), 0, -sin(rot_y))
	for t in [-2.5, 0.0, 2.5]:
		placed.append(pos + along * t)

func _l_wall(parent: Node, wall_name: String, corner: Vector3, rot_y: float) -> void:
	var x_dir := Vector3(cos(rot_y), 0, -sin(rot_y))
	var z_dir := Vector3(sin(rot_y), 0, cos(rot_y))
	_block(wall_name + "A", parent, corner + x_dir * 4.0, Vector3(8.0, 2.6, 0.6), mat_wall, rot_y)
	_block(wall_name + "B", parent, corner + z_dir * 3.0, Vector3(0.6, 2.6, 6.0), mat_wall, rot_y)
	for t in [0.0, 2.0, 4.0, 6.0, 8.0]:
		placed.append(corner + x_dir * t)
	for t in [2.0, 4.0, 6.0]:
		placed.append(corner + z_dir * t)

func _crate_yard() -> void:
	# NE: stacked cargo crates forming alleys.
	var spots := [Vector3(11, 0, 11), Vector3(25, 0, 9), Vector3(10, 0, 17), Vector3(18, 0, 18),
		Vector3(24, 0, 24), Vector3(19, 0, 25), Vector3(25, 0, 17)]
	for s in spots:
		if not _spaced(s, 4.0):
			continue  # a container is already there
		var rot := (PI / 2) * (rng.randi() % 2)
		_prop("wooden_crate_02", s, 3.0, rot)
		if rng.randf() < 0.55:
			_prop("wooden_crate_01", s, 3.0, rot + PI / 2, 0.46 * 3.0)  # stacked on top
		if rng.randf() < 0.5:
			_prop("old_military_crate", s + Vector3(rng.randf_range(-2, 2), 0, 2.6), 2.2, rng.randf() * TAU)

func _barrier_maze() -> void:
	# NW: zig-zag barrier lines with gaps, tall utility boxes as posts.
	var row := 0
	for z in [10.0, 16.0, 22.0]:
		var x := -8.5 - (2.5 if row % 2 == 0 else 0.0)
		var n := 0
		while x > -PROP_LIMIT:
			if n % 4 != 3 and _spaced(Vector3(x, 0, z), 2.0):  # every fourth slot is a gap
				_prop("concrete_road_barrier_02" if n % 2 == 0 else "concrete_road_barrier", Vector3(x, 0, z), 1.45, PI / 2)
			x -= 2.35
			n += 1
		row += 1
	for p in [Vector3(-13, 0, 13), Vector3(-22, 0, 19), Vector3(-25, 0, 13)]:
		_prop("utility_box_02" if rng.randf() < 0.5 else "utility_box_01", p, 1.6, rng.randf() * TAU)

func _boulder_field() -> void:
	# SW: scattered rocks, big and small, placed with spacing so there are always ways through.
	var tries := 0
	var count := 0
	while count < 7 and tries < 400:
		tries += 1
		var p := Vector3(-rng.randf_range(9, PROP_LIMIT), 0, -rng.randf_range(9, PROP_LIMIT))
		if not _clear_of_avenues(p) or not _spaced(p, 5.5):
			continue
		var big := rng.randf() < 0.55
		_prop("namaqualand_boulder_04" if big else "namaqualand_boulder_02", p, 1.25 if big else 1.6, rng.randf() * TAU)
		count += 1

func _depot() -> void:
	# SE: a raised metal deck (height play) with two ramps, and barrel clusters.
	var decks := _add(Node3D.new(), arena, "Decks")
	_deck(decks, "DeckA", Vector3(17, 0, -19), Vector2(8, 8), 2.2, [Vector3(-1, 0, 0), Vector3(0, 0, 1)])
	for c in [Vector3(10, 0, -25), Vector3(23, 0, -9), Vector3(24, 0, -26)]:
		for k in 3 + rng.randi() % 2:
			var off := Vector3(rng.randf_range(-1.6, 1.6), 0, rng.randf_range(-1.6, 1.6))
			_prop("barrel_01" if k % 2 == 0 else "barrel_03", c + off, 1.3, rng.randf() * TAU)

func _deck(parent: Node, deck_name: String, center: Vector3, size: Vector2, height: float, ramp_sides: Array) -> void:
	_block(deck_name, parent, center, Vector3(size.x, height, size.y), mat_metal)
	for side in ramp_sides:
		var run := height / tan(deg_to_rad(16.0))
		var length := sqrt(run * run + height * height)
		var half_extent: float = (size.x if absf(side.x) > 0 else size.y) / 2.0
		var base: Vector3 = center + side * (half_extent + run / 2.0)
		# A tilted slab whose top surface runs from the ground up to the deck's edge.
		var yaw := atan2(side.x, side.z)
		_block("%sRamp%d" % [deck_name, ramp_sides.find(side)], parent, base + Vector3(0, height / 2.0 - 0.2, 0),
			Vector3(4.0, 0.4, length), mat_metal, yaw, deg_to_rad(16.0))

func _spawns() -> void:
	# 16 spawns in the clear perimeter lane, 4 per side, each facing the centre.
	var spawns := _add(Node3D.new(), arena, "SpawnPoints")
	var lane := HALF - 3.0
	var i := 0
	for along in [-18.0, -6.0, 6.0, 18.0]:
		for p in [Vector3(along, 1, -lane), Vector3(along, 1, lane), Vector3(-lane, 1, along), Vector3(lane, 1, along)]:
			var m := Marker3D.new()
			_add(m, spawns, "Spawn%d" % (i + 1))
			m.position = p
			m.rotation = Vector3(0, atan2(p.x, p.z), 0)  # -Z (forward) points at the centre
			i += 1
