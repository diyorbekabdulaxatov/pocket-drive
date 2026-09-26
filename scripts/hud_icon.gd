extends Control

# A square HUD icon button (48 dp). Glyphs are drawn with simple shapes so they
# stay crisp at any screen density. The HUD routes touches; this only draws.
var glyph := "pause"
var active := false
var text := ""  # used by the drive-mode tile
const LIGHT := Color(0.93, 0.96, 0.97)
const AMBER := Color("f4ad48")

func _ready() -> void:
	mouse_filter = Control.MOUSE_FILTER_IGNORE

func set_active(value: bool) -> void:
	if value != active:
		active = value
		queue_redraw()

func set_text(value: String) -> void:
	if value != text:
		text = value
		queue_redraw()

func _draw() -> void:
	var style := StyleBoxFlat.new()
	style.bg_color = Color(0.03, 0.08, 0.11, 0.72)
	style.border_color = AMBER if active else Color(0.75, 0.86, 0.88, 0.35)
	style.set_border_width_all(3 if active else 2)
	style.set_corner_radius_all(16)
	draw_style_box(style, Rect2(Vector2.ZERO, size))
	var c := size / 2
	var s := minf(size.x, size.y) * 0.26
	var ink := AMBER if active else LIGHT
	match glyph:
		"pause":
			for x in [-0.45, 0.45]:
				draw_rect(Rect2(c + Vector2(x * s - s * 0.2, -s * 0.8), Vector2(s * 0.4, s * 1.6)), ink)
		"reset":
			draw_arc(c, s * 0.85, -PI * 0.35, PI * 1.35, 24, ink, s * 0.26, true)
			var head := c + Vector2.from_angle(-PI * 0.35) * s * 0.85
			draw_colored_polygon(PackedVector2Array([head + Vector2(-s * 0.1, -s * 0.5), head + Vector2(s * 0.5, s * 0.1), head + Vector2(-s * 0.35, s * 0.3)]), ink)
		"camera":
			draw_rect(Rect2(c + Vector2(-s, -s * 0.55), Vector2(s * 2, s * 1.3)), ink, false, s * 0.18)
			draw_rect(Rect2(c + Vector2(-s * 0.35, -s * 0.85), Vector2(s * 0.7, s * 0.3)), ink)
			draw_circle(c + Vector2(0, s * 0.1), s * 0.38, ink)
		"headlights":
			# Lamp dome with three beams to the left.
			var dome := PackedVector2Array()
			for k in 13: dome.append(c + Vector2(s * 0.15, 0) + Vector2.from_angle(-PI / 2 + PI * k / 12.0) * s * 0.75)
			draw_colored_polygon(dome, ink)
			for y in [-0.5, 0.0, 0.5]:
				draw_line(c + Vector2(-s * 0.2, y * s), c + Vector2(-s * 1.05, y * s * 1.2), ink, s * 0.16, true)
		"hazard":
			var tri := PackedVector2Array([c + Vector2(0, -s * 0.95), c + Vector2(s, s * 0.75), c + Vector2(-s, s * 0.75), c + Vector2(0, -s * 0.95)])
			draw_polyline(tri, Color(0.95, 0.2, 0.18) if not active else AMBER, s * 0.2, true)
			var small := PackedVector2Array([c + Vector2(0, -s * 0.3), c + Vector2(s * 0.42, s * 0.42), c + Vector2(-s * 0.42, s * 0.42), c + Vector2(0, -s * 0.3)])
			draw_polyline(small, Color(0.95, 0.2, 0.18) if not active else AMBER, s * 0.12, true)
		"horn":
			draw_colored_polygon(PackedVector2Array([c + Vector2(-s * 0.9, -s * 0.25), c + Vector2(-s * 0.3, -s * 0.25), c + Vector2(s * 0.45, -s * 0.8), c + Vector2(s * 0.45, s * 0.8), c + Vector2(-s * 0.3, s * 0.25), c + Vector2(-s * 0.9, s * 0.25)]), ink)
			for k in 2:
				draw_arc(c + Vector2(s * 0.45, 0), s * (0.45 + k * 0.35), -0.7, 0.7, 10, ink, s * 0.12, true)
		"left", "right":
			var dir := -1.0 if glyph == "left" else 1.0
			draw_colored_polygon(PackedVector2Array([c + Vector2(dir * s, 0), c + Vector2(0, -s * 0.8), c + Vector2(0, -s * 0.32), c + Vector2(-dir * s * 0.9, -s * 0.32), c + Vector2(-dir * s * 0.9, s * 0.32), c + Vector2(0, s * 0.32), c + Vector2(0, s * 0.8)]), AMBER if active else LIGHT)
		"mode":
			var font := ThemeDB.fallback_font
			var w := font.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1, 24).x
			draw_string(font, Vector2((size.x - w) / 2, c.y + 4), text, HORIZONTAL_ALIGNMENT_LEFT, -1, 24, LIGHT)
			draw_rect(Rect2(Vector2(size.x * 0.22, c.y + 16), Vector2(size.x * 0.56, 7)), Color(0.35, 0.85, 0.45) if text == "SPORT" else Color(0.4, 0.7, 0.95))
