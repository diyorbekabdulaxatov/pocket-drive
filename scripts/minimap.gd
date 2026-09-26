extends Control

# North-up overview of the city: roads, buildings and the car's heading.
var world

func _ready() -> void:
	mouse_filter = Control.MOUSE_FILTER_IGNORE

func _process(_delta: float) -> void:
	queue_redraw()

func to_map(x: float, z: float) -> Vector2:
	var scale_factor: float = size.x / (2.0 * world.HALF)
	return Vector2((x + world.HALF) * scale_factor, (z + world.HALF) * scale_factor)

func _draw() -> void:
	if world == null or not is_instance_valid(world.car): return
	var style := StyleBoxFlat.new()
	style.bg_color = Color(0.04, 0.10, 0.14, 0.78)
	style.border_color = Color(0.7, 0.84, 0.86, 0.40)
	style.set_border_width_all(2)
	style.set_corner_radius_all(14)
	draw_style_box(style, Rect2(Vector2.ZERO, size))
	var scale_factor: float = size.x / (2.0 * world.HALF)
	var road := Color(0.72, 0.78, 0.8, 0.9)
	for r in world.road_rects:
		draw_rect(Rect2(to_map(r.position.x, r.position.y), r.size * scale_factor), road)
	draw_circle(to_map(0, 0), world.ROUNDABOUT * scale_factor, road)
	for b in world.buildings:
		draw_rect(Rect2(to_map(b.position.x, b.position.z), Vector2(b.size.x, b.size.z) * scale_factor), Color(0.3, 0.45, 0.52))
	var car: Node3D = world.car
	var at := to_map(car.global_position.x, car.global_position.z)
	var forward := Vector2(-car.global_basis.z.x, -car.global_basis.z.z).normalized()
	var side := Vector2(-forward.y, forward.x)
	draw_colored_polygon(PackedVector2Array([at + forward * 9, at - forward * 5 + side * 5.5, at - forward * 5 - side * 5.5]), Color("f4ad48"))
