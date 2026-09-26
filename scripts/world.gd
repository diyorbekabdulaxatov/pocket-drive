extends Node3D

const CarScript = preload("res://scripts/car.gd")
const HudScript = preload("res://scripts/hud.gd")
var car: DriveCar
var camera: Camera3D
var hud: CanvasLayer
var mats: Dictionary = {}
var random := RandomNumberGenerator.new()
var camera_ready := false

func mat(id: String, color: String) -> StandardMaterial3D:
	if mats.has(id): return mats[id]
	var m := StandardMaterial3D.new()
	m.albedo_color = Color(color)
	m.roughness = 0.9
	mats[id] = m
	return m

func box(size: Vector3, at: Vector3, material: Material, solid := false) -> MeshInstance3D:
	var mesh := MeshInstance3D.new()
	var shape := BoxMesh.new()
	shape.size = size
	mesh.mesh = shape
	mesh.material_override = material
	mesh.position = at
	add_child(mesh)
	if solid:
		var body := StaticBody3D.new()
		var collision := CollisionShape3D.new()
		var bounds := BoxShape3D.new()
		bounds.size = size
		collision.shape = bounds
		body.add_child(collision)
		mesh.add_child(body)
	return mesh

func tree(at: Vector3, scale_factor := 1.0) -> void:
	box(Vector3(0.45, 2.8, 0.45) * scale_factor, at + Vector3(0, 1.4, 0) * scale_factor, mat("trunk", "8a6d50"), true)
	var leaves := MeshInstance3D.new()
	var shape := SphereMesh.new()
	shape.radius = 2.1 * scale_factor
	shape.height = 4.8 * scale_factor
	shape.radial_segments = 7
	shape.rings = 3
	leaves.mesh = shape
	leaves.position = at + Vector3(0, 4.3, 0) * scale_factor
	leaves.material_override = mat("leaf", "438b6a")
	add_child(leaves)

func building(at: Vector3, dimensions: Vector3, index: int) -> void:
	var colors := ["e7b48c", "e4d9b5", "a3c9c2", "baafc8", "d78266"]
	box(dimensions, at + Vector3(0, dimensions.y / 2, 0), mat("wall%d" % index, colors[index % colors.size()]), true)
	box(Vector3(dimensions.x + 0.5, 0.35, dimensions.z + 0.5), at + Vector3(0, dimensions.y + 0.15, 0), mat("roof", "e9e4d2"))
	for y in range(2, int(dimensions.y) - 1, 3):
		for x in [-dimensions.x * 0.28, dimensions.x * 0.28]:
			for z in [-1, 1]:
				box(Vector3(1.5, 1.65, 0.06), at + Vector3(x, y, z * (dimensions.z / 2 + 0.04)), mat("window", "385964"))
		for z in [-dimensions.z * 0.28, dimensions.z * 0.28]:
			for x in [-1, 1]:
				box(Vector3(0.06, 1.65, 1.5), at + Vector3(x * (dimensions.x / 2 + 0.04), y, z), mat("window", "385964"))

func _ready() -> void:
	random.seed = 1234
	var environment := WorldEnvironment.new()
	var env := Environment.new()
	env.background_mode = Environment.BG_SKY
	var sky := Sky.new()
	var sky_mat := ProceduralSkyMaterial.new()
	sky_mat.sky_top_color = Color("68aacb")
	sky_mat.sky_horizon_color = Color("c8e1e2")
	sky_mat.ground_horizon_color = Color("c8e1e2")
	sky_mat.ground_bottom_color = Color("82ac8a")
	sky.sky_material = sky_mat
	env.sky = sky
	env.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	env.ambient_light_color = Color("dae7ee")
	env.ambient_light_energy = 0.7
	env.tonemap_mode = Environment.TONE_MAPPER_FILMIC
	environment.environment = env
	add_child(environment)
	var sun := DirectionalLight3D.new()
	sun.rotation_degrees = Vector3(-48, -28, 0)
	sun.light_color = Color("fff0d7")
	sun.light_energy = 1.4
	sun.shadow_enabled = true
	sun.directional_shadow_max_distance = 65
	add_child(sun)
	box(Vector3(240, 1, 240), Vector3(0, -0.5, 0), mat("grass", "8bb38a"), true)
	# Road grid: three vertical and three horizontal streets.
	for axis in [-64, 0, 64]:
		box(Vector3(14, 0.025, 144), Vector3(axis, 0.013, 0), mat("road", "42535b"))
		box(Vector3(144, 0.03, 14), Vector3(0, 0.017, axis), mat("road", "42535b"))
		for step in range(-68, 69, 8):
			if absf(step) < 9 or absf(absf(step) - 64) < 9: continue
			box(Vector3(0.16, 0.015, 3.5), Vector3(axis, 0.04, step), mat("lines", "ede1b4"))
			box(Vector3(3.5, 0.015, 0.16), Vector3(step, 0.045, axis), mat("lines", "ede1b4"))
		for side in [-1, 1]:
			box(Vector3(0.15, 0.01, 140), Vector3(axis + side * 6.5, 0.045, 0), mat("edge", "c5d0cc"))
			box(Vector3(140, 0.01, 0.15), Vector3(0, 0.05, axis + side * 6.5), mat("edge", "c5d0cc"))
	# A flat park and three blocks, leaving generous corners for turning.
	for x in [-32, 32]:
		for z in [-32, 32]:
			box(Vector3(47, 0.06, 47), Vector3(x, 0.03, z), mat("pavement", "c9cbb8"))
			if x == 32 and z == 32:
				box(Vector3(41, 0.07, 41), Vector3(x, 0.05, z), mat("park", "80aa76"))
				for tx in [20, 43]:
					for tz in [20, 43]: tree(Vector3(tx, 0.1, tz), 1.1)
				box(Vector3(3, 0.08, 41), Vector3(x, 0.09, z), mat("path", "dfd4b8"))
				box(Vector3(41, 0.08, 3), Vector3(x, 0.095, z), mat("path", "dfd4b8"))
			else:
				for dx in [-11, 11]:
					for dz in [-11, 11]:
						building(Vector3(x + dx, 0.065, z + dz), Vector3(13, random.randf_range(5, 13), 13), random.randi_range(0, 4))
	# Open maneuvering area west of the town.
	box(Vector3(30, 0.03, 60), Vector3(-88, 0.017, 0), mat("road", "42535b"))
	box(Vector3(24, 0.03, 14), Vector3(-77, 0.02, 0), mat("road", "42535b"))
	for z in range(-24, 25, 6):
		box(Vector3(6, 0.01, 0.12), Vector3(-98, 0.04, z), mat("edge", "c5d0cc"))
	# Low boundary walls keep the test area contained.
	for side in [-1, 1]:
		box(Vector3(220, 1.3, 1), Vector3(0, 0.65, side * 109), mat("fence", "dedbc7"), true)
		box(Vector3(1, 1.3, 218), Vector3(side * 109, 0.65, 0), mat("fence", "dedbc7"), true)
		for n in range(-90, 100, 18):
			tree(Vector3(n, 0, side * 85), random.randf_range(0.8, 1.3))
			if side == 1: tree(Vector3(85, 0, n), random.randf_range(0.8, 1.3))
	car = CarScript.new()
	car.name = "Car"
	add_child(car)
	camera = Camera3D.new()
	camera.name = "FollowCamera"
	camera.fov = 65
	camera.far = 300
	camera.current = true
	add_child(camera)
	hud = CanvasLayer.new()
	hud.set_script(HudScript)
	hud.car = car
	add_child(hud)
	hud.reset_requested.connect(func(): car.reset_car(); camera_ready = false)

func _physics_process(delta: float) -> void:
	var target := car.global_position + Vector3(0, 1.3, 0)
	var desired: Vector3 = target + car.global_basis.z * (5.3 + absf(car.speed) * 0.035) + Vector3(0, 2.6, 0)
	var query := PhysicsRayQueryParameters3D.create(target, desired)
	query.exclude = [car.get_rid()]
	var hit := get_world_3d().direct_space_state.intersect_ray(query)
	if not hit.is_empty(): desired = hit.position + hit.normal * 0.4
	if not camera_ready:
		camera.global_position = desired
		camera_ready = true
	else: camera.global_position = camera.global_position.lerp(desired, 1 - exp(-7 * delta))
	camera.look_at(target + -car.global_basis.z * 2.8, Vector3.UP)
