extends Control

# A held driving control: a steering arrow tile or a metal pedal. The HUD decides
# which finger holds it; this control only draws its idle and pressed looks.
var kind := "gas"  # "left", "right", "brake" or "gas"
var active := false

func _ready() -> void:
	mouse_filter = Control.MOUSE_FILTER_IGNORE

func set_active(value: bool) -> void:
	if value != active:
		active = value
		queue_redraw()

func rounded_polygon(r: Rect2, radius: float) -> PackedVector2Array:
	var points := PackedVector2Array()
	var corners := [r.position + Vector2(r.size.x - radius, radius), r.end - Vector2(radius, radius), Vector2(r.position.x + radius, r.end.y - radius), r.position + Vector2(radius, radius)]
	for c in 4:
		for k in 7:
			var angle := -PI / 2 + (c + k / 6.0) * PI / 2
			points.append(corners[c] + Vector2.from_angle(angle) * radius)
	return points

func _draw() -> void:
	if kind == "left" or kind == "right": draw_arrow()
	else: draw_pedal()

func draw_arrow() -> void:
	var style := StyleBoxFlat.new()
	style.bg_color = Color(0.85, 0.9, 0.92, 0.42) if active else Color(0.55, 0.6, 0.62, 0.28)
	style.border_color = Color(1, 1, 1, 0.55 if active else 0.3)
	style.set_border_width_all(2)
	style.set_corner_radius_all(14)
	draw_style_box(style, Rect2(Vector2.ZERO, size))
	var c := size / 2
	var s := minf(size.x, size.y) * 0.22
	var tip := -1.0 if kind == "left" else 1.0
	draw_colored_polygon(PackedVector2Array([c + Vector2(tip * s, 0), c + Vector2(-tip * s * 0.7, -s), c + Vector2(-tip * s * 0.7, s)]), Color(1, 1, 1, 0.95 if active else 0.8))

# Brushed-metal pedal with rubber grips; pressed, it sinks slightly and darkens.
func draw_pedal() -> void:
	var inset := 0.05 if active else 0.0
	var r := Rect2(size * inset, size * (1.0 - inset * 2))
	var shade := 0.78 if active else 1.0
	var outline := rounded_polygon(r.grow(3), 22)
	draw_colored_polygon(outline, Color(0.08, 0.09, 0.1, 0.9))
	var plate := rounded_polygon(r, 20)
	var colors := PackedColorArray()
	for p in plate:
		var t := (p.y - r.position.y) / r.size.y
		colors.append(Color(0.86, 0.88, 0.9).lerp(Color(0.46, 0.49, 0.52), t) * Color(shade, shade, shade))
	draw_polygon(plate, colors)
	draw_polyline(rounded_polygon(r.grow(-4), 17), Color(1, 1, 1, 0.35 * shade), 2.0, true)
	var grip := Color(0.12, 0.13, 0.15)
	var inner := r.grow(-r.size.x * 0.16)
	if kind == "brake":
		# Three slanted rubber bars.
		for k in 3:
			var y := inner.position.y + inner.size.y * (0.2 + k * 0.3)
			var slant := inner.size.y * 0.1
			draw_colored_polygon(PackedVector2Array([Vector2(inner.position.x, y + slant), Vector2(inner.end.x - 6, y - slant), Vector2(inner.end.x, y - slant + 12), Vector2(inner.position.x + 6, y + slant + 12)]), grip)
	else:
		# Rows of rounded rubber studs.
		for row in 6:
			for col in 3:
				var cell := Vector2(inner.size.x / 3, inner.size.y / 6)
				var stud := Rect2(inner.position + Vector2(col, row) * cell + cell * 0.18, cell * 0.64)
				draw_colored_polygon(rounded_polygon(stud, minf(stud.size.x, stud.size.y) * 0.45), grip)
