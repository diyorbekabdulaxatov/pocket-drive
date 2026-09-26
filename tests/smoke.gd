extends SceneTree

var failures: Array[String] = []
func check(ok: bool, message: String) -> void:
	if not ok: failures.append(message); push_error(message)
	else: print("PASS: " + message)

func _initialize() -> void:
	call_deferred("run_checks")

func ticks(count: int) -> void:
	for n in count: await physics_frame

func tyre_bounds(tyre: MeshInstance3D, frame: Node3D) -> AABB:
	return (frame.global_transform.affine_inverse() * tyre.global_transform) * tyre.get_aabb()

func check_wheel_geometry(car: DriveCar) -> void:
	var hubs: Dictionary = {}
	for wheel in car.wheels:
		var id := String(wheel.name).trim_prefix("Wheel")
		var pivot := wheel.get_parent() as Node3D
		hubs[id] = car.to_local(pivot.global_position)
		var tyre: MeshInstance3D
		var spinning_calipers := 0
		for child in wheel.find_children("Cylinder*", "MeshInstance3D", true, false):
			if "_2_" in String(child.name): tyre = child
			if "_3_" in String(child.name): spinning_calipers += 1
		check(spinning_calipers == 0, id + " spinning wheel excludes calipers")
		check(tyre != null, id + " tyre mesh exists")
		if tyre == null: continue
		var bounds := tyre_bounds(tyre, wheel)
		check(absf(bounds.size.y - bounds.size.z) < 0.01, id + " tyre is round around local X")
		check(bounds.get_center().length() < 0.01, id + " tyre geometry is centered on hub")
		print("MEASURE ", id, " tyre size=", bounds.size, " centre=", bounds.get_center(), " spawn pivot=", pivot.global_position)
		var original_rotation := wheel.rotation
		var before := tyre_bounds(tyre, pivot).get_center()
		var calipers: Array[Node3D] = []
		var caliper_transforms: Array[Transform3D] = []
		for child in pivot.find_children("Cylinder*", "MeshInstance3D", true, false):
			if "_3_" in String(child.name):
				calipers.append(child)
				caliper_transforms.append(child.global_transform)
		check(calipers.size() == 1, id + " steering pivot retains one caliper")
		wheel.rotation.x += PI / 2
		check(tyre_bounds(tyre, pivot).get_center().distance_to(before) < 0.001, id + " 90-degree spin keeps tyre centre fixed")
		for i in calipers.size():
			check(calipers[i].global_transform.is_equal_approx(caliper_transforms[i]), id + " caliper stays fixed during spin")
		wheel.rotation = original_rotation
		# The caliper follows its pivot when steering; it must not be attached
		# directly to the car body as a workaround for its unwanted spinning.
		var pivot_rotation := pivot.rotation
		var local_calipers: Array[Transform3D] = []
		for caliper in calipers: local_calipers.append(pivot.global_transform.affine_inverse() * caliper.global_transform)
		pivot.rotation.y += 0.3
		for i in calipers.size():
			check((pivot.global_transform.affine_inverse() * calipers[i].global_transform).is_equal_approx(local_calipers[i]), id + " caliper follows steering pivot")
		pivot.rotation = pivot_rotation
	for axle in ["F", "R"]:
		if not hubs.has(axle + "L") or not hubs.has(axle + "R"):
			check(false, axle + " axle has both hubs")
			continue
		var left: Vector3 = hubs[axle + "L"]
		var right: Vector3 = hubs[axle + "R"]
		check(absf(left.z - right.z) < 0.01, axle + " axle hubs align in Z")
		check(left.x < 0 and right.x > 0 and absf(left.x + right.x) < 0.01, axle + " axle hubs mirror in X")

func check_city(world, car: DriveCar) -> void:
	check(world.HALF * 2 >= 600, "City spans at least 600 m")
	check(world.is_on_road(car.spawn), "Spawn is on a road")
	check(world.buildings.size() >= 100, "City has %d buildings" % world.buildings.size())
	var on_road := 0
	for b in world.buildings:
		var footprint := Rect2(b.position.x, b.position.z, b.size.x, b.size.z)
		var nearest := Vector2.ZERO.clamp(footprint.position, footprint.end)
		if nearest.length() < world.ROUNDABOUT: on_road += 1
		for r in world.road_rects:
			if footprint.intersects(r): on_road += 1
	check(on_road == 0, "No building stands on a road")
	var blocked := 0
	for t in world.trees:
		if world.is_on_road(t) and absf(t.x) > 2.5 and absf(t.z) > 2.5: blocked += 1
	check(blocked == 0, "No tree stands on a road outside the boulevard median")
	var meshes: int = world.find_children("*", "MeshInstance3D", false, false).size()
	check(meshes < 300, "Static city is batched into %d meshes" % meshes)

func run_checks() -> void:
	var world = load("res://main.tscn").instantiate()
	root.add_child(world)
	await ticks(5)
	var car = world.car
	var hud = world.hud
	for dimensions in [Vector2i(1280, 720), Vector2i(2340, 1080)]:
		root.size = dimensions
		await ticks(2)
		var viewport_rect: Rect2 = hud.root.get_global_rect()
		for id in hud.pads:
			var rect: Rect2 = hud.pads[id].get_global_rect()
			check(viewport_rect.encloses(rect) and rect.size.x >= 100 and rect.size.y >= 100, "Visible touch control %s at %s" % [id, dimensions])
			check(not rect.intersects(world.minimap.get_global_rect()), "Minimap clear of %s at %s" % [id, dimensions])
		check(viewport_rect.encloses(world.minimap.get_global_rect()), "Minimap visible at %s" % [dimensions])
	check(car.wheels.size() == 4 and car.front_wheels.size() == 2, "Four Porsche wheels and two steering pivots loaded")
	check(absf(car.position.y) < 0.12, "Porsche sits at ground level")
	car.reset_car()
	check_wheel_geometry(car)
	check_city(world, car)
	hud.set_process(false)
	car.throttle = 1
	await ticks(120)
	check(car.speed > 10 and car.position.z < car.spawn.z - 13, "Accelerates and moves forward")
	car.throttle = 0
	car.brake = 1
	await ticks(180)
	check(car.speed < -2, "Brake stops the car and engages reverse")
	car.reset_car()
	car.throttle = 1
	car.steering = 1
	await ticks(90)
	check(car.position.x > car.spawn.x + 1 and car.heading < -0.2, "Right steering turns right")
	car.reset_car()
	car.position = Vector3(world.HALF - 4, 0.1, 0)
	car.heading = -PI / 2
	car.throttle = 1
	await ticks(120)
	check(car.position.x < world.HALF, "Boundary collision prevents escape")
	car.reset_car()
	car.position = Vector3(0, 0.08, 17)
	car.throttle = 1
	await ticks(120)
	check(car.position.z > world.ISLAND + 1.5, "Roundabout island blocks the car")
	car.reset_car()
	car.position = Vector3(300, 0.08, 0)
	check(not world.is_on_road(car.position), "Perimeter verge is off-road")
	car.throttle = 1
	await ticks(240)
	check(car.speed > 11 and car.speed <= 12.01, "Off-road speed is limited to 12 m/s")
	car.reset_car()
	check(car.speed == 0 and car.position.is_equal_approx(car.spawn), "Reset restores spawn and stops motion")
	for item in [[0, "gas"], [1, "left"]]:
		var touch := InputEventScreenTouch.new()
		touch.index = item[0]
		touch.pressed = true
		touch.position = hud.pads[item[1]].get_global_rect().get_center()
		hud._input(touch)
	hud._process(0.016)
	check(car.throttle == 1 and car.steering == -1, "Two-finger gas and steering work together")
	var release := InputEventScreenTouch.new()
	release.index = 0
	release.pressed = false
	hud._input(release)
	hud._process(0.016)
	check(car.throttle == 0 and car.steering == -1, "Releasing one finger preserves the other control")
	hud.set_paused(true)
	check(paused and car.throttle == 0 and hud.fingers.is_empty(), "Pause clears held controls")
	hud.set_paused(false)
	check(not paused, "Resume restores simulation")
	world.queue_free()
	await process_frame
	print("SMOKE RESULT: ", "PASS" if failures.is_empty() else "FAIL")
	quit(0 if failures.is_empty() else 1)
