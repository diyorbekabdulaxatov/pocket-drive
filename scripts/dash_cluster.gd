extends Control

# Instrument cluster: speedometer on the left, minimap in the middle, rev
# counter on the right. Reads the car and its engine sound model each frame.
var car: DriveCar
var audio: Node
const DIAL := 84.0
const MAP := 172.0
const START := PI * 0.75  # dials sweep 270 degrees clockwise from bottom-left
const SWEEP := PI * 1.5
const CYAN := Color(0.35, 0.8, 1.0)
const LIGHT := Color(0.93, 0.96, 0.97)

func _ready() -> void:
	mouse_filter = Control.MOUSE_FILTER_IGNORE

func _process(_delta: float) -> void:
	queue_redraw()

# Places the minimap in the middle of the cluster.
func hold_minimap(map: Control) -> void:
	add_child(map)
	map.position = Vector2((size.x - MAP) / 2, (size.y - MAP) / 2)
	map.size = Vector2(MAP, MAP)

func speed_dial_max() -> float:
	return ceilf(car.top_speed * 3.6 / 20.0) * 20.0 + 20.0

func dial(centre: Vector2, fraction: float, marks: int, red_from: float) -> void:
	draw_circle(centre, DIAL, Color(0.02, 0.06, 0.1, 0.9))
	draw_arc(centre, DIAL - 3, 0, TAU, 48, CYAN, 3, true)
	draw_arc(centre, DIAL - 16, START, START + SWEEP, 40, Color(0.2, 0.3, 0.38), 8, true)
	draw_arc(centre, DIAL - 16, START, START + SWEEP * clampf(fraction, 0, 1), 40, CYAN if fraction < red_from else Color(1.0, 0.35, 0.3), 8, true)
	if red_from < 1.0:
		draw_arc(centre, DIAL - 28, START + SWEEP * red_from, START + SWEEP, 16, Color(1.0, 0.3, 0.25, 0.85), 5, true)
	for k in marks + 1:
		var a := START + SWEEP * k / marks
		draw_line(centre + Vector2.from_angle(a) * (DIAL - 34), centre + Vector2.from_angle(a) * (DIAL - 24), LIGHT, 2, true)
	var needle := START + SWEEP * clampf(fraction, 0, 1.05)
	draw_line(centre, centre + Vector2.from_angle(needle) * (DIAL - 22), Color(1.0, 0.45, 0.2), 4, true)
	draw_circle(centre, 9, Color(0.75, 0.8, 0.83))

func text_centered(value: String, at: Vector2, font_size: int, colour: Color) -> void:
	var font := ThemeDB.fallback_font
	var w := font.get_string_size(value, HORIZONTAL_ALIGNMENT_LEFT, -1, font_size).x
	draw_string(font, at - Vector2(w / 2, 0), value, HORIZONTAL_ALIGNMENT_LEFT, -1, font_size, colour)

func _draw() -> void:
	if not is_instance_valid(car): return
	var panel := StyleBoxFlat.new()
	panel.bg_color = Color(0.02, 0.07, 0.12, 0.72)
	panel.border_color = Color(0.35, 0.8, 1.0, 0.55)
	panel.set_border_width_all(2)
	panel.set_corner_radius_all(int(size.y / 2))
	draw_style_box(panel, Rect2(Vector2.ZERO, size))
	var left := Vector2(DIAL + 8, size.y / 2)
	var right := Vector2(size.x - DIAL - 8, size.y / 2)
	var kmh := absf(car.speed) * 3.6
	dial(left, kmh / speed_dial_max(), 6, 2.0)
	text_centered(str(roundi(kmh)), left + Vector2(0, 46), 34, LIGHT)
	text_centered("KM/H", left + Vector2(0, 70), 24, Color(0.7, 0.8, 0.85))
	var redline: float = car.info().get("redline", 7500.0)
	var rpm: float = audio.rpm if is_instance_valid(audio) else 0.0
	dial(right, rpm / (ceilf(redline / 1000.0) * 1000.0), int(ceilf(redline / 1000.0)), redline / (ceilf(redline / 1000.0) * 1000.0) - 0.08)
	text_centered(car.gear, right + Vector2(0, 46), 34, Color("f4ad48"))
	text_centered("x1000", right + Vector2(0, 70), 24, Color(0.7, 0.8, 0.85))
