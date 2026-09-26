extends SceneTree

var failures: Array[String] = []
func check(ok: bool, message: String) -> void:
	if not ok: failures.append(message); push_error(message)
	else: print("PASS: " + message)

func _initialize() -> void:
	call_deferred("run_checks")

func ticks(count: int) -> void:
	for n in count: await physics_frame

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
	check(car.wheels.size() == 4 and car.front_wheels.size() == 2, "Four Porsche wheels and two steering pivots loaded")
	check(absf(car.position.y) < 0.12, "Porsche sits at ground level")
	for wheel in car.wheels:
		check(wheel.position.length() < 0.001, "Wheel rotates around its own pivot")
	hud.set_process(false)
	car.throttle = 1
	await ticks(120)
	check(car.speed > 10 and car.position.z < 35, "Accelerates and moves forward")
	car.throttle = 0
	car.brake = 1
	await ticks(180)
	check(car.speed < -2, "Brake stops the car and engages reverse")
	car.reset_car()
	car.throttle = 1
	car.steering = 1
	await ticks(90)
	check(car.position.x > 1 and car.heading < -0.2, "Right steering turns right")
	car.reset_car()
	car.position = Vector3(106, 0.1, 0)
	car.heading = -PI / 2
	car.throttle = 1
	await ticks(120)
	check(car.position.x < 109, "Boundary collision prevents escape")
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
