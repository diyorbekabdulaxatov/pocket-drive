extends Control

# Vertical automatic gear lever: P at the top, D at the bottom. Tapping a letter
# or dragging along the gate selects that gear. The HUD routes touches (so it
# never interferes with the pedals); this control draws itself and maps points
# to gears.
const GEARS := ["P", "R", "N", "D"]
const FONT_SIZE := 30
var selected := "P"

func _ready() -> void:
	mouse_filter = Control.MOUSE_FILTER_IGNORE

func segment_rect(index: int) -> Rect2:
	var h := size.y / GEARS.size()
	return Rect2(0, index * h, size.x, h)

func gear_at(global_point: Vector2) -> String:
	var local := global_point - global_position
	for i in GEARS.size():
		if segment_rect(i).has_point(local): return GEARS[i]
	return ""

# While dragging, the finger may leave the lever sideways; follow its height.
func gear_nearest(global_point: Vector2) -> String:
	var local_y := global_point.y - global_position.y
	return GEARS[clampi(int(local_y / (size.y / GEARS.size())), 0, GEARS.size() - 1)]

func select(gear: String) -> void:
	if gear != selected:
		selected = gear
		queue_redraw()

func _draw() -> void:
	var panel := StyleBoxFlat.new()
	panel.bg_color = Color(0.03, 0.08, 0.11, 0.72)
	panel.border_color = Color(0.75, 0.86, 0.88, 0.35)
	panel.set_border_width_all(2)
	panel.set_corner_radius_all(18)
	draw_style_box(panel, Rect2(Vector2.ZERO, size))
	var font := ThemeDB.fallback_font
	var gate_x := size.x * 0.68
	var first := segment_rect(0).get_center().y
	var last := segment_rect(GEARS.size() - 1).get_center().y
	draw_line(Vector2(gate_x, first), Vector2(gate_x, last), Color(0, 0, 0, 0.6), 14, true)
	draw_line(Vector2(gate_x, first), Vector2(gate_x, last), Color(0.35, 0.4, 0.43), 6, true)
	for i in GEARS.size():
		var r := segment_rect(i)
		var is_selected: bool = GEARS[i] == selected
		var colour := Color("f4ad48") if is_selected else Color(0.93, 0.96, 0.97, 0.75)
		draw_string(font, Vector2(size.x * 0.14, r.get_center().y + FONT_SIZE * 0.36), GEARS[i], HORIZONTAL_ALIGNMENT_LEFT, -1, FONT_SIZE, colour)
		draw_circle(Vector2(gate_x, r.get_center().y), 4, Color(0.6, 0.65, 0.68))
	# Lever handle: a chrome knob with an amber band at the selected gear.
	var knob := Vector2(gate_x, segment_rect(GEARS.find(selected)).get_center().y)
	draw_circle(knob + Vector2(2, 3), 22, Color(0, 0, 0, 0.45))
	draw_circle(knob, 22, Color(0.72, 0.75, 0.78))
	draw_circle(knob + Vector2(-5, -6), 12, Color(0.93, 0.95, 0.97))
	draw_arc(knob, 22, 0, TAU, 32, Color("f4ad48"), 4, true)
