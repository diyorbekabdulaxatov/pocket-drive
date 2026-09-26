class_name DriveCar
extends CharacterBody3D

var throttle := 0.0
var brake := 0.0
var steering := 0.0
var speed := 0.0
var heading := 0.0
var distance_driven := 0.0
var visual: Node3D
var wheels: Array[Node3D] = []
var front_wheels: Array[Node3D] = []
var spawn := Vector3(0, 0.08, 48)
# Set by the world: whether a point is paved road, and how far the car may roam.
var road_check: Callable
var world_limit := 120.0
const TOP_SPEED := 25.0

const PorscheModel = preload("res://assets/porsche/porsche_mobile.glb")

func _ready() -> void:
	floor_snap_length = 0.4
	var collision := CollisionShape3D.new()
	var box := BoxShape3D.new()
	box.size = Vector3(1.90, 1.30, 4.45)
	collision.shape = box
	collision.position.y = 0.65
	add_child(collision)
	visual = Node3D.new()
	add_child(visual)
	var model: Node3D = PorscheModel.instantiate()
	visual.add_child(model)
	for id in ["FL", "FR", "RL", "RR"]:
		var wheel := model.find_child("Wheel" + id, true, false) as Node3D
		if wheel == null:
			push_error("Porsche wheel missing: " + id)
			continue
		var pivot := Node3D.new()
		pivot.name = "Steering" + id
		visual.add_child(pivot)
		pivot.global_position = wheel.global_position
		wheel.reparent(pivot, true)
		# Brake calipers steer with the hub but must not spin with the tyre.
		for child in wheel.find_children("Cylinder*", "MeshInstance3D", true, false):
			if "_3_" in String(child.name):
				child.reparent(pivot, true)
		wheels.append(wheel)
		if id.begins_with("F"): front_wheels.append(pivot)
	reset_car()

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
	var limit := 12.0 if on_grass else TOP_SPEED
	if brake > 0:
		if speed > 0.4: speed = move_toward(speed, 0, 22 * brake * delta)
		else: speed = move_toward(speed, -7.0, 5.0 * brake * delta)
	elif throttle > 0:
		if speed < -0.4: speed = move_toward(speed, 0, 18 * throttle * delta)
		else: speed = move_toward(speed, limit, 8.5 * throttle * delta)
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
	for i in get_slide_collision_count():
		var normal := get_slide_collision(i).get_normal()
		if absf(normal.y) < 0.4 and absf(forward.dot(normal)) > 0.35:
			speed = move_toward(speed, 0, 40 * delta)
	distance_driven += Vector2(global_position.x - before.x, global_position.z - before.z).length()
	visual.rotation.z = lerp(visual.rotation.z, steering * speed * 0.0025, 8 * delta)
	for pivot in front_wheels: pivot.rotation.y = -steering * 0.35
	for wheel in wheels: wheel.rotation.x -= speed * delta / 0.334
	if position.y < -10 or absf(position.x) > world_limit or absf(position.z) > world_limit: reset_car()
