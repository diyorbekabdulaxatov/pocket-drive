extends CanvasLayer

signal reset_requested
signal camera_requested
signal car_requested
signal pause_changed(paused: bool)
var car: DriveCar
var pads: Dictionary = {}
# Which control each finger (or the mouse, index -1) is holding.
var fingers: Dictionary = {}
var status_label: Label
var status_hint := "Tap D to drive"
var status_timer := 0.0
var switch_button: Button
var root: Control
var pause_panel: PanelContainer
var paused := false
var ui_steering := 0.0
var ui_throttle := 0.0
var ui_brake := 0.0
const Pad = preload("res://scripts/drive_pad.gd")
const Shifter = preload("res://scripts/gear_shifter.gd")
const GEAR_HINTS := {"P": "Tap D to drive", "R": "Reverse: GAS backs up", "N": "Neutral: tap D to drive", "D": "Hold BRAKE to stop"}
var shifter: Control
const CarAudio = preload("res://scripts/car_audio.gd")
var click: AudioStreamPlayer
var sound_button: Button
var status_chip: PanelContainer
var credits_dialog: AcceptDialog

# Sizes follow Material 3 minimums on phones, where the 720-unit-tall layout
# puts about two units in each dp: 48 dp touch targets, 14 sp button text and
# 12 sp labels. Text drawn over the 3D view gets a dark outline for contrast.
const TOUCH := 96
const GAP := 12
const EDGE := 34
const EDGE_TOP := 22
const LEVER_WIDTH := 110
const Icon = preload("res://scripts/hud_icon.gd")
const ICON_NAMES := {"pause": "Pause", "mode": "Driving mode", "headlights": "Headlights", "hazard": "Hazard lights", "horn": "Horn", "camera": "Change camera", "signal_left": "Left turn signal", "signal_right": "Right turn signal", "reset": "Reset car"}
const Cluster = preload("res://scripts/dash_cluster.gd")
var icons: Dictionary = {}
var cluster: Control
const TEXT := 28
const SMALL := 24

func label(text: String, font_size: int, color := Color("f1f5f1")) -> Label:
	var l := Label.new()
	l.text = text
	l.add_theme_font_size_override("font_size", font_size)
	l.add_theme_color_override("font_color", color)
	l.add_theme_color_override("font_outline_color", Color(0, 0, 0, 0.6))
	l.add_theme_constant_override("outline_size", 6)
	l.mouse_filter = Control.MOUSE_FILTER_IGNORE
	return l

func button(text: String, action: Callable) -> Button:
	var b := Button.new()
	b.text = text
	b.custom_minimum_size = Vector2(TOUCH, TOUCH)
	b.add_theme_font_size_override("font_size", TEXT)
	for state in ["normal", "hover", "pressed"]:
		var style := StyleBoxFlat.new()
		style.bg_color = Color("173b49") if state == "normal" else (Color("1f4f62") if state == "hover" else Color("f4ad48"))
		style.set_corner_radius_all(20)
		style.content_margin_left = 28
		style.content_margin_right = 28
		b.add_theme_stylebox_override(state, style)
	b.add_theme_color_override("font_pressed_color", Color("112c38"))
	b.pressed.connect(func(): click.play())
	b.pressed.connect(action)
	b.focus_mode = Control.FOCUS_NONE
	return b

func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	click = AudioStreamPlayer.new()
	click.stream = CarAudio.click_sound()
	add_child(click)
	root = Control.new()
	root.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	root.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(root)
	# Left panel of driver controls, two columns of 48 dp tiles.
	var layout := [["pause", "mode"], ["headlights", "hazard"], ["horn", "camera"], ["signal_left", "signal_right"]]
	for row in layout.size():
		for col in 2:
			make_icon(layout[row][col], Vector2(EDGE + col * (TOUCH + GAP), EDGE_TOP + row * (TOUCH + GAP)), false)
	icons.mode.set_text("SPORT")
	# Right side: reset, then the gear lever running down from the top corner.
	make_icon("reset", Vector2(-EDGE - LEVER_WIDTH - GAP - TOUCH, EDGE_TOP), true)
	shifter = Control.new()
	shifter.set_script(Shifter)
	root.add_child(shifter)
	shifter.set_anchors_and_offsets_preset(Control.PRESET_TOP_RIGHT)
	shifter.offset_left = -EDGE - LEVER_WIDTH
	shifter.offset_right = -EDGE
	shifter.offset_top = EDGE_TOP
	shifter.offset_bottom = EDGE_TOP + TOUCH * 4
	if "accessibility_name" in shifter: shifter.accessibility_name = "Gear lever"
	# The hint sits on a dark chip at the top so it stays readable over bright sky.
	status_chip = PanelContainer.new()
	status_chip.set_anchors_and_offsets_preset(Control.PRESET_CENTER_TOP)
	status_chip.grow_horizontal = Control.GROW_DIRECTION_BOTH
	status_chip.offset_top = EDGE_TOP
	status_chip.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var chip := StyleBoxFlat.new()
	chip.bg_color = Color(0.04, 0.10, 0.14, 0.78)
	chip.set_corner_radius_all(18)
	chip.content_margin_left = 22
	chip.content_margin_right = 22
	chip.content_margin_top = 6
	chip.content_margin_bottom = 6
	status_chip.add_theme_stylebox_override("panel", chip)
	root.add_child(status_chip)
	status_label = label(status_hint, 26)
	status_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	status_chip.add_child(status_label)
	make_pad("left", Vector2(32, -156), Vector2(118, 120), false)
	make_pad("right", Vector2(166, -156), Vector2(118, 120), false)
	make_pad("gas", Vector2(-EDGE - 120, -36 - 200), Vector2(120, 200), true)
	make_pad("brake", Vector2(-EDGE - 120 - 16 - 150, -36 - 140), Vector2(150, 140), true)
	cluster = Control.new()
	cluster.set_script(Cluster)
	cluster.car = car
	root.add_child(cluster)
	cluster.set_anchors_and_offsets_preset(Control.PRESET_CENTER_BOTTOM)
	cluster.offset_left = -280
	cluster.offset_right = 280
	cluster.offset_top = -18 - 190
	cluster.offset_bottom = -18
	pause_panel = PanelContainer.new()
	pause_panel.set_anchors_and_offsets_preset(Control.PRESET_CENTER)
	pause_panel.grow_horizontal = Control.GROW_DIRECTION_BOTH
	pause_panel.grow_vertical = Control.GROW_DIRECTION_BOTH
	var box := StyleBoxFlat.new()
	box.bg_color = Color("102c38")
	box.set_corner_radius_all(24)
	box.set_content_margin_all(30)
	pause_panel.add_theme_stylebox_override("panel", box)
	root.add_child(pause_panel)
	var content := VBoxContainer.new()
	content.add_theme_constant_override("separation", 18)
	pause_panel.add_child(content)
	content.add_child(label("TAKE A BREAK", 34))
	content.add_child(label("Explore at your own pace.", TEXT, Color("d9e6e8")))
	# Two columns keep five full-size buttons within a landscape phone's height.
	var grid := GridContainer.new()
	grid.columns = 2
	grid.add_theme_constant_override("h_separation", 16)
	grid.add_theme_constant_override("v_separation", 16)
	content.add_child(grid)
	var resume := button("Resume driving", func(): set_paused(false))
	grid.add_child(resume)
	grid.add_child(button("Reset car", func(): reset_requested.emit(); set_paused(false)))
	switch_button = button("Switch car", func(): car_requested.emit())
	switch_button.visible = DriveCar.available_models().size() > 1
	grid.add_child(switch_button)
	sound_button = button("Sound: On", toggle_sound)
	grid.add_child(sound_button)
	grid.add_child(button("Car model credits", show_car_credits))
	for b in grid.get_children(): b.custom_minimum_size.x = 300
	content.add_child(label("Godot Engine · godotengine.org/license", SMALL, Color("b5c8ce")))
	pause_panel.hide()
	get_viewport().size_changed.connect(apply_safe_area)
	apply_safe_area()

# Keeps controls out of the camera cutout and rounded corners on phones.
func apply_safe_area() -> void:
	if not OS.has_feature("mobile"): return
	var window := Vector2(DisplayServer.window_get_size())
	if window.x <= 0 or window.y <= 0: return
	var safe := Rect2(DisplayServer.get_display_safe_area())
	var units := root.get_viewport_rect().size / window
	root.offset_left = maxf(safe.position.x, 0) * units.x
	root.offset_top = maxf(safe.position.y, 0) * units.y
	root.offset_right = -maxf(window.x - safe.end.x, 0) * units.x
	root.offset_bottom = -maxf(window.y - safe.end.y, 0) * units.y

func make_pad(id: String, offset: Vector2, dimensions: Vector2, right: bool) -> void:
	var pad := Control.new()
	pad.set_script(Pad)
	pad.kind = id
	root.add_child(pad)
	pad.set_anchors_and_offsets_preset(Control.PRESET_BOTTOM_RIGHT if right else Control.PRESET_BOTTOM_LEFT)
	pad.offset_left = offset.x
	pad.offset_top = offset.y
	pad.offset_right = offset.x + dimensions.x
	pad.offset_bottom = offset.y + dimensions.y
	if "accessibility_name" in pad: pad.accessibility_name = {"left": "Steer left", "right": "Steer right", "brake": "Brake", "gas": "Accelerate"}[id]
	pads[id] = pad

func make_icon(id: String, offset: Vector2, right: bool) -> void:
	var icon := Control.new()
	icon.set_script(Icon)
	# Signal buttons draw arrows; their ids differ from the steering pads' "left"/"right".
	icon.glyph = id.trim_prefix("signal_")
	root.add_child(icon)
	icon.set_anchors_and_offsets_preset(Control.PRESET_TOP_RIGHT if right else Control.PRESET_TOP_LEFT)
	icon.offset_left = offset.x
	icon.offset_top = offset.y
	icon.offset_right = offset.x + TOUCH
	icon.offset_bottom = offset.y + TOUCH
	if "accessibility_name" in icon: icon.accessibility_name = ICON_NAMES[id]
	icons[id] = icon

func pad_at(point: Vector2) -> String:
	for id in pads:
		if pads[id].get_global_rect().has_point(point): return id
	return ""

func control_at(point: Vector2) -> String:
	var pad := pad_at(point)
	if pad != "": return pad
	for id in icons:
		if icons[id].get_global_rect().has_point(point): return id
	if shifter.get_global_rect().has_point(point): return "lever"
	return ""

# Every touch goes through here, so a finger holding a pedal never blocks
# another finger from steering, shifting or pressing a button.
func press(index: int, point: Vector2) -> void:
	var id := control_at(point)
	if id == "": return
	fingers[index] = id
	if id == "lever": request_shift(shifter.gear_at(point))
	elif icons.has(id) and id != "horn": tap_icon(id)

func drag(index: int, point: Vector2) -> void:
	var id: String = fingers.get(index, "")
	if id == "lever":
		var gear: String = shifter.gear_nearest(point)
		if gear != car.gear: request_shift(gear)
	elif id == "" or pads.has(id):
		# Sliding a thumb between pedals or steering arrows follows the thumb.
		var pad := pad_at(point)
		if pad != "": fingers[index] = pad
		else: fingers.erase(index)

func release(index: int) -> void:
	fingers.erase(index)

func tap_icon(id: String) -> void:
	click.play()
	match id:
		"pause": set_paused(true)
		"reset": clear_inputs(); reset_requested.emit()
		"camera": camera_requested.emit()
		"headlights": car.set_headlights(not car.headlights_on)
		"hazard": car.toggle_indicator("hazard")
		"signal_left", "signal_right": car.toggle_indicator(id.trim_prefix("signal_"))
		"mode":
			car.sport = not car.sport
			icons.mode.set_text("SPORT" if car.sport else "CITY")
			show_status("Driving mode: " + ("SPORT" if car.sport else "CITY"))

func _input(event: InputEvent) -> void:
	if event is InputEventKey and event.pressed and not event.echo:
		if event.keycode == KEY_ESCAPE: set_paused(not paused)
		if event.keycode == KEY_V: car_requested.emit()
		if not paused:
			match event.keycode:
				KEY_R: tap_icon("reset")
				KEY_C: tap_icon("camera")
				KEY_H: tap_icon("headlights")
				KEY_Q: tap_icon("signal_left")
				KEY_E: tap_icon("signal_right")
				KEY_X: tap_icon("hazard")
				KEY_M: tap_icon("mode")
			if event.keycode >= KEY_1 and event.keycode <= KEY_4: request_shift(Shifter.GEARS[event.keycode - KEY_1])
	if paused: return
	if event is InputEventScreenTouch:
		if event.pressed: press(event.index, event.position)
		else: release(event.index)
	elif event is InputEventScreenDrag:
		drag(event.index, event.position)
	# Touches also arrive as emulated mouse events; only real mice are handled here.
	elif event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT and event.device != InputEvent.DEVICE_ID_EMULATION:
		if event.pressed: press(-1, event.position)
		else: release(-1)
	elif event is InputEventMouseMotion and event.device != InputEvent.DEVICE_ID_EMULATION and Input.is_mouse_button_pressed(MOUSE_BUTTON_LEFT):
		drag(-1, event.position)

func request_shift(to: String) -> void:
	var reason: String = car.shift_block_reason(to)
	if reason != "":
		show_status(reason)
		return
	car.shift(to)
	shifter.select(to)
	click.play()

func held(id: String) -> bool:
	return id in fingers.values()

# Shows a short message in place of the driving hint for two seconds.
func show_status(text: String) -> void:
	status_label.text = text
	status_timer = 2.0

func _process(delta: float) -> void:
	if status_timer > 0:
		status_timer -= delta
		if status_timer <= 0: status_label.text = status_hint
	if is_instance_valid(car):
		shifter.select(car.gear)
		status_hint = GEAR_HINTS[car.gear]
		if status_timer <= 0: status_label.text = status_hint
	if not is_instance_valid(car): return
	var left := held("left") or Input.is_physical_key_pressed(KEY_A) or Input.is_physical_key_pressed(KEY_LEFT)
	var right := held("right") or Input.is_physical_key_pressed(KEY_D) or Input.is_physical_key_pressed(KEY_RIGHT)
	var gas := held("gas") or Input.is_physical_key_pressed(KEY_W) or Input.is_physical_key_pressed(KEY_UP)
	var brake_pressed := held("brake") or Input.is_physical_key_pressed(KEY_S) or Input.is_physical_key_pressed(KEY_DOWN) or Input.is_physical_key_pressed(KEY_SPACE)
	ui_steering = float(right) - float(left) if not paused else 0.0
	ui_throttle = float(gas) if not paused else 0.0
	ui_brake = float(brake_pressed) if not paused else 0.0
	car.steering = ui_steering
	car.throttle = ui_throttle
	car.brake = ui_brake
	pads.left.set_active(left and not paused)
	pads.right.set_active(right and not paused)
	pads.gas.set_active(gas and not paused)
	pads.brake.set_active(brake_pressed and not paused)
	car.horn = (held("horn") or Input.is_physical_key_pressed(KEY_B)) and not paused
	icons.horn.set_active(car.horn)
	icons.headlights.set_active(car.headlights_on)
	icons.hazard.set_active(car.indicator == "hazard")
	for side in ["left", "right"]:
		icons["signal_" + side].set_active(car.indicator_lit and car.indicator in [side, "hazard"])

func clear_inputs() -> void:
	fingers.clear()
	if is_instance_valid(car):
		car.throttle = 0
		car.brake = 0
		car.steering = 0
		car.horn = false

func set_paused(value: bool) -> void:
	paused = value
	clear_inputs()
	pause_panel.visible = value
	get_tree().paused = value
	pause_changed.emit(value)

func _notification(what: int) -> void:
	if what == NOTIFICATION_APPLICATION_FOCUS_OUT or what == NOTIFICATION_APPLICATION_PAUSED:
		if is_instance_valid(pause_panel): set_paused(true)
	elif what == NOTIFICATION_WM_GO_BACK_REQUEST:
		# Back closes the topmost thing first: the credits dialog, then the pause menu.
		if is_instance_valid(credits_dialog): credits_dialog.queue_free()
		elif is_instance_valid(pause_panel): set_paused(not paused)

func toggle_sound() -> void:
	var master := AudioServer.get_bus_index("Master")
	AudioServer.set_bus_mute(master, not AudioServer.is_bus_mute(master))
	sound_button.text = "Sound: Off" if AudioServer.is_bus_mute(master) else "Sound: On"

func show_car_credits() -> void:
	var dialog := AcceptDialog.new()
	credits_dialog = dialog
	dialog.title = car.info().name + " model credits"
	dialog.dialog_text = car.info().credits
	dialog.add_theme_font_size_override("font_size", SMALL)
	dialog.get_label().add_theme_font_size_override("font_size", SMALL)
	dialog.get_ok_button().custom_minimum_size = Vector2(160, TOUCH)
	dialog.get_ok_button().add_theme_font_size_override("font_size", TEXT)
	root.add_child(dialog)
	dialog.popup_centered(Vector2i(980, 640))
	dialog.confirmed.connect(dialog.queue_free)
	dialog.canceled.connect(dialog.queue_free)
