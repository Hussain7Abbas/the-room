extends SceneTree
## Bakes assets/props/knife/knife.obj (a 106-surface OBJ in centimetres, lying diagonally) into
## assets/props/knife/knife_mesh.res: one surface per part, real materials, metres, and a
## predictable frame, so the hand offset in knife.tscn stays simple.
##   origin = centre of the grip, +Y = toward the blade tip, X = the blade's flat normal.
## Re-run after replacing the OBJ:
##   godot --headless --path . --script res://tools/bake_knife.gd

const SOURCE := "res://assets/props/knife/knife.obj"
const OUTPUT := "res://assets/props/knife/knife_mesh.res"
const LENGTH_METRES := 0.45 # pommel to tip

func _init() -> void:
	var src: ArrayMesh = load(SOURCE)

	# The OBJ's materials, by part (identified by rendering each in a different colour).
	var parts := {
		"blinn1SG": _metal(Color(0.78, 0.80, 0.83), 0.28),    # blade
		"lambert4SG": _metal(Color(0.92, 0.93, 0.95), 0.12),  # bevelled cutting edge
		"lambert2SG": _metal(Color(0.69, 0.53, 0.30), 0.38),  # bronze guard + pommel
		"lambert3SG": _leather(),                             # grip wrap
	}

	# Frame from the geometry: grip centre, tip = farthest vertex from it, pommel = farthest from the tip.
	var grip := Vector3.ZERO
	var grip_count := 0
	var all_points: Array[Vector3] = []
	for s in src.get_surface_count():
		var verts: PackedVector3Array = src.surface_get_arrays(s)[Mesh.ARRAY_VERTEX]
		var is_grip := src.surface_get_material(s).resource_name == "lambert3SG"
		for v in verts:
			all_points.append(v)
			if is_grip:
				grip += v
				grip_count += 1
	grip /= max(grip_count, 1)
	var tip := _farthest(all_points, grip)
	var pommel := _farthest(all_points, tip)

	var y_axis := (tip - grip).normalized()
	var x_axis := (Vector3.RIGHT - y_axis * Vector3.RIGHT.dot(y_axis)).normalized() # the blade is flat in X
	var z_axis := x_axis.cross(y_axis)
	var scale := LENGTH_METRES / tip.distance_to(pommel)
	# Rows = new axes, so this maps a source point into the knife's own frame.
	var basis := Basis(x_axis, y_axis, z_axis).transposed().scaled(Vector3.ONE * scale)
	var to_knife := Transform3D(basis, basis * -grip)

	var out := ArrayMesh.new()
	for material_name in parts:
		var st := SurfaceTool.new()
		st.begin(Mesh.PRIMITIVE_TRIANGLES)
		var any := false
		for s in src.get_surface_count():
			if src.surface_get_material(s).resource_name == material_name:
				st.append_from(src, s, to_knife)
				any = true
		if not any:
			continue
		st.set_material(parts[material_name])
		st.index()
		st.commit(out)
		out.surface_set_name(out.get_surface_count() - 1, material_name)

	var err := ResourceSaver.save(out, OUTPUT)
	print("[bake_knife] %d surfaces -> %s (%s), aabb %s" % [out.get_surface_count(), OUTPUT, error_string(err), out.get_aabb()])
	quit()

func _farthest(points: Array[Vector3], from: Vector3) -> Vector3:
	var best := from
	var best_d := -1.0
	for p in points:
		var d := p.distance_squared_to(from)
		if d > best_d:
			best_d = d
			best = p
	return best

func _metal(color: Color, roughness: float) -> StandardMaterial3D:
	var m := StandardMaterial3D.new()
	m.albedo_color = color
	m.metallic = 1.0
	m.roughness = roughness
	return m

func _leather() -> StandardMaterial3D:
	var m := StandardMaterial3D.new()
	m.albedo_color = Color(0.23, 0.15, 0.10)
	m.roughness = 0.85
	return m
