extends Control

var caption := ""
var active := false
var accent := Color("f4ad48")

func _ready() -> void:
	mouse_filter = Control.MOUSE_FILTER_IGNORE

func _draw() -> void:
	var style := StyleBoxFlat.new()
	style.bg_color = Color(0.95, 0.68, 0.28, 0.95) if active else Color(0.04, 0.10, 0.14, 0.84)
	style.border_color = accent if active else Color(0.7, 0.84, 0.86, 0.40)
	style.set_border_width_all(2)
	style.set_corner_radius_all(22)
	draw_style_box(style, Rect2(Vector2.ZERO, size))
	var font := ThemeDB.fallback_font
	var width := font.get_string_size(caption, HORIZONTAL_ALIGNMENT_LEFT, -1, 22).x
	draw_string(font, Vector2((size.x - width) / 2, size.y / 2 + 8), caption, HORIZONTAL_ALIGNMENT_LEFT, -1, 22, Color("112c38") if active else Color("f0f6f4"))

func set_active(value: bool) -> void:
	if value != active:
		active = value
		queue_redraw()
