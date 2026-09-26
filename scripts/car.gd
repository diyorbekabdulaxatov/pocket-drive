class_name DriveCar
extends CharacterBody3D

# Emitted on the first frame the car runs into a wall, tree or post.
signal crashed(impact_speed: float)

var throttle := 0.0
var brake := 0.0
var steering := 0.0
var speed := 0.0
var heading := 0.0
var distance_driven := 0.0
var visual: Node3D
var wheels: Array[Node3D] = []
var front_wheels: Array[Node3D] = []
# Simple interior shown only in the driver camera; the Porsche model has none.
var cockpit: Node3D
var speed_needle: Node3D
var spawn := Vector3(0, 0.08, 48)
# Set by the world: whether a point is paved road, and how far the car may roam.
var road_check: Callable
var world_limit := 120.0
var top_speed := 25.0
var acceleration := 8.5
var wheel_radius := 0.334
var model_index := 0
var cockpit_view := false
var collision: CollisionShape3D
var touching_wall := false

# Driveable cars. A model file may be absent (the GT3 RS is kept out of git), in
# which case it is skipped. "turn" and "scale" fit each model to 1 m units facing -Z;
# the car is lowered so its tyres touch the ground. Calipers either sit inside each
# wheel (found by name tag) or are separate nodes; either way they steer but never spin.
const MODELS := [
	{
		"name": "911 Carrera 4S",
		"scene": "res://assets/porsche/porsche_mobile.glb",
		"scale": 1.0, "turn": 0.0,
		"wheels": {"FL": "WheelFL", "FR": "WheelFR", "RL": "WheelRL", "RR": "WheelRR"},
		"caliper_tag": "_3_", "calipers": {},
		"radius": 0.334, "body": Vector3(1.90, 1.30, 4.45),
		"top_speed": 25.0, "acceleration": 8.5, "redline": 7500.0, "engine_pitch": 1.0,
		"cockpit": true, "eye": Vector3(-0.36, 1.08, 0.22),
		"credits": "Based on (FREE) Porsche 911 Carrera 4S by Karol Miklas.\n\nSource: https://sketchfab.com/3d-models/\nfree-porsche-911-carrera-4s-d01b254483794de3819786d93e0e1ebf\nAuthor: https://sketchfab.com/karolmiklas\n\nModel and this adaptation: Creative Commons BY-SA 4.0\nhttps://creativecommons.org/licenses/by-sa/4.0/\n\nChanges: reduced polygons and textures, removed ground and\nclearcoat shell, separated wheels, adjusted scale and materials.\nVehicle design and trademarks belong to their respective owners.",
	},
	{
		"name": "911 GT3 RS",
		"scene": "res://assets/gt3rs/gt3rs.glb",
		"scale": 100.0, "turn": PI,
		"wheels": {"FL": "3DWheel Front L", "FR": "3DWheel Front R", "RL": "3DWheel Rear L", "RR": "3DWheel Rear R"},
		"caliper_tag": "", "calipers": {"FL": "Calliper Front L_02", "FR": "Calliper Front R_03", "RL": "Calliper Rear L_04", "RR": "Calliper Rear R_05"},
		"radius": 0.35, "body": Vector3(1.95, 1.30, 4.55),
		"top_speed": 30.0, "acceleration": 10.0, "redline": 9000.0, "engine_pitch": 1.12,
		"cockpit": false, "eye": Vector3(-0.37, 1.05, 0.3),
		"credits": "2019 Porsche 911 (991.2) GT3 RS by Ddiaz Design.\n\nSource: https://sketchfab.com/3d-models/\n2019-porsche-911-9912-gt3-rs-97ea84db846e4945961dffb8b2d7af18\nAuthor: https://sketchfab.com/ddiaz-design\n\nLicense: Creative Commons BY-NC-SA 4.0 (non-commercial use only)\nhttps://creativecommons.org/licenses/by-nc-sa/4.0/\n\nChanges: scaled, rotated and lowered to fit the game; wheels\nseparated for steering and spin.\nVehicle design and trademarks belong to their respective owners.",
	},
]

static func available_models() -> Array[int]:
	var out: Array[int] = []
	for i in MODELS.size():
		if ResourceLoader.exists(MODELS[i].scene): out.append(i)
	return out

func info() -> Dictionary:
	return MODELS[model_index]

func _ready() -> void:
	floor_snap_length = 0.4
	collision = CollisionShape3D.new()
	collision.shape = BoxShape3D.new()
	add_child(collision)
	build_cockpit()
	load_model(model_index)
	reset_car()

func next_model() -> void:
	var models := available_models()
	load_model(models[(models.find(model_index) + 1) % models.size()])
	speed = 0

func load_model(index: int) -> void:
	model_index = index
	var spec := info()
	top_speed = spec.top_speed
	acceleration = spec.acceleration
	wheel_radius = spec.radius
	(collision.shape as BoxShape3D).size = spec.body
	collision.position.y = spec.body.y / 2
	if visual:
		remove_child(visual)
		visual.queue_free()
	wheels.clear()
	front_wheels.clear()
	visual = Node3D.new()
	add_child(visual)
	visual.transform = Transform3D()
	var model: Node3D = load(spec.scene).instantiate()
	model.scale = Vector3.ONE * spec.scale
	model.rotation.y = spec.turn
	visual.add_child(model)
	var lowest := INF
	for id in spec.wheels:
		var wheel := model.find_child(spec.wheels[id], true, false) as Node3D
		if wheel: lowest = minf(lowest, visual.to_local(wheel.global_position).y - wheel_radius)
	if lowest != INF: model.position.y -= lowest
	for id in ["FL", "FR", "RL", "RR"]:
		var wheel := model.find_child(spec.wheels[id], true, false) as Node3D
		if wheel == null:
			push_error("%s wheel missing: %s" % [spec.name, id])
			continue
		# pivot steers (front only); spinner rolls around the car's own axle axis.
		var pivot := Node3D.new()
		pivot.name = "Steering" + id
		visual.add_child(pivot)
		pivot.position = visual.to_local(wheel.global_position)
		var spinner := Node3D.new()
		spinner.name = "Wheel" + id
		pivot.add_child(spinner)
		wheel.reparent(spinner, true)
		if spec.caliper_tag != "":
			for child in wheel.find_children("*", "MeshInstance3D", true, false):
				if spec.caliper_tag in String(child.name): child.reparent(pivot, true)
		elif spec.calipers.has(id):
			var caliper := model.find_child(spec.calipers[id], true, false) as Node3D
			if caliper: caliper.reparent(pivot, true)
		wheels.append(spinner)
		if id.begins_with("F"): front_wheels.append(pivot)
	set_cockpit_view(cockpit_view)

func cockpit_part(mesh: Mesh, at: Vector3, color: Color, parent: Node3D, basis := Basis(), unshaded := false) -> MeshInstance3D:
	var m := StandardMaterial3D.new()
	m.albedo_color = color
	m.roughness = 0.7
	m.metallic_specular = 0.2
	if unshaded: m.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	var node := MeshInstance3D.new()
	node.mesh = mesh
	node.material_override = m
	node.transform = Transform3D(basis, at)
	node.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	parent.add_child(node)
	return node

func slab(size: Vector3) -> BoxMesh:
	var b := BoxMesh.new()
	b.size = size
	return b

# A bar between two points, used for the windscreen pillars.
func bar(a: Vector3, b: Vector3, thickness: float, color: Color) -> void:
	cockpit_part(slab(Vector3(thickness, thickness, a.distance_to(b))), (a + b) / 2, color, cockpit, Basis.looking_at(b - a, Vector3.UP))

func build_cockpit() -> void:
	cockpit = Node3D.new()
	cockpit.name = "Cockpit"
	cockpit.visible = false
	add_child(cockpit)
	var trim := Color(0.09, 0.09, 0.1)
	var leather := Color(0.16, 0.14, 0.13)
	# Dashboard, driver's instrument hood, centre console and door panels.
	cockpit_part(slab(Vector3(1.56, 0.22, 0.5)), Vector3(0, 0.8, -0.6), trim, cockpit)
	cockpit_part(slab(Vector3(0.48, 0.1, 0.24)), Vector3(-0.36, 0.95, -0.43), trim, cockpit)
	cockpit_part(slab(Vector3(0.26, 0.28, 0.7)), Vector3(0, 0.62, -0.05), leather, cockpit)
	for side in [-1.0, 1.0]:
		cockpit_part(slab(Vector3(0.08, 0.4, 1.7)), Vector3(side * 0.84, 0.74, 0), leather, cockpit)
		cockpit_part(slab(Vector3(0.14, 0.05, 1.7)), Vector3(side * 0.8, 0.95, 0), trim, cockpit)
		bar(Vector3(side * 0.7, 0.9, -0.84), Vector3(side * 0.6, 1.24, -0.14), 0.08, trim)
	cockpit_part(slab(Vector3(1.3, 0.06, 0.16)), Vector3(0, 1.25, -0.12), trim, cockpit)
	cockpit_part(slab(Vector3(0.2, 0.055, 0.03)), Vector3(0.02, 1.19, -0.24), trim, cockpit)
	cockpit_part(slab(Vector3(0.18, 0.04, 0.01)), Vector3(0.02, 1.19, -0.22), Color(0.3, 0.36, 0.42), cockpit)
	# Three round gauges; the centre one has a needle driven by speed.
	var face := Basis(Vector3.RIGHT, PI / 2)
	for n in 3:
		var radius := 0.07 if n == 1 else 0.055
		var at := Vector3(-0.36 + (n - 1) * 0.135, 0.91, -0.305)
		var dial := CylinderMesh.new()
		dial.top_radius = radius
		dial.bottom_radius = radius
		dial.height = 0.01
		cockpit_part(dial, at, Color(0.03, 0.03, 0.035), cockpit, face)
		var rim := TorusMesh.new()
		rim.inner_radius = radius - 0.006
		rim.outer_radius = radius
		cockpit_part(rim, at + Vector3(0, 0, 0.004), Color(0.85, 0.87, 0.9), cockpit, face, true)
		if n == 1:
			speed_needle = Node3D.new()
			speed_needle.position = at + Vector3(0, 0, 0.008)
			cockpit.add_child(speed_needle)
			cockpit_part(slab(Vector3(0.006, 0.055, 0.004)), Vector3(0, 0.026, 0), Color(1.0, 0.45, 0.15), speed_needle, Basis(), true)
	# Steering wheel, tilted towards the driver; spokes point left, right and down.
	var wheel_pivot := Node3D.new()
	wheel_pivot.position = Vector3(-0.36, 0.86, -0.12)
	wheel_pivot.rotation.x = PI / 2 - 0.35
	cockpit.add_child(wheel_pivot)
	var ring := TorusMesh.new()
	ring.inner_radius = 0.165
	ring.outer_radius = 0.19
	cockpit_part(ring, Vector3.ZERO, leather, wheel_pivot)
	var hub := CylinderMesh.new()
	hub.top_radius = 0.05
	hub.bottom_radius = 0.05
	hub.height = 0.04
	cockpit_part(hub, Vector3.ZERO, trim, wheel_pivot)
	for angle in [0.0, PI, PI / 2]:
		cockpit_part(slab(Vector3(0.17, 0.02, 0.03)), Vector3(cos(angle) * 0.09, 0, sin(angle) * 0.09), trim, wheel_pivot, Basis(Vector3.UP, -angle))

# The driver camera shows the built cockpit for models without an interior.
func set_cockpit_view(enabled: bool) -> void:
	cockpit_view = enabled
	var built: bool = enabled and info().cockpit
	cockpit.visible = built
	visual.visible = not built

func reset_car() -> void:
	position = spawn
	heading = 0
	rotation = Vector3.ZERO
	speed = 0
	velocity = Vector3.ZERO
	throttle = 0
	brake = 0
	steering = 0

func _physics_process(delta: float) -> void:
	var before := global_position
	var on_grass: bool = road_check.is_valid() and not road_check.call(global_position)
	var limit := 12.0 if on_grass else top_speed
	if brake > 0:
		if speed > 0.4: speed = move_toward(speed, 0, 22 * brake * delta)
		else: speed = move_toward(speed, -7.0, 5.0 * brake * delta)
	elif throttle > 0:
		if speed < -0.4: speed = move_toward(speed, 0, 18 * throttle * delta)
		else: speed = move_toward(speed, limit, acceleration * throttle * delta)
	else:
		speed = move_toward(speed, 0, (1.8 + absf(speed) * 0.07) * delta)
	if on_grass and speed > limit: speed = move_toward(speed, limit, 10 * delta)
	var turn_rate := 1.15 / (1.0 + absf(speed) / 28.0)
	heading -= steering * turn_rate * clampf(speed / 5.0, -1, 1) * delta
	rotation.y = heading
	var forward := -global_basis.z
	velocity.x = forward.x * speed
	velocity.z = forward.z * speed
	if is_on_floor(): velocity.y = -0.1
	else: velocity.y -= 22 * delta
	move_and_slide()
	var impact_speed := absf(speed)
	var hit_wall := false
	for i in get_slide_collision_count():
		var normal := get_slide_collision(i).get_normal()
		if absf(normal.y) < 0.4 and absf(forward.dot(normal)) > 0.35:
			speed = move_toward(speed, 0, 40 * delta)
			hit_wall = true
	if hit_wall and not touching_wall and impact_speed > 3.0: crashed.emit(impact_speed)
	touching_wall = hit_wall
	distance_driven += Vector2(global_position.x - before.x, global_position.z - before.z).length()
	visual.rotation.z = lerp(visual.rotation.z, steering * speed * 0.0025, 8 * delta)
	for pivot in front_wheels: pivot.rotation.y = -steering * 0.35
	if cockpit.visible:
		speed_needle.rotation.z = 2.2 - clampf(absf(speed) / top_speed, 0, 1) * 4.4
	for wheel in wheels: wheel.rotation.x -= speed * delta / wheel_radius
	if position.y < -10 or absf(position.x) > world_limit or absf(position.z) > world_limit: reset_car()
