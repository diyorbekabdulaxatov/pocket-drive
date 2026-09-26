extends CanvasLayer

signal reset_requested
signal pause_changed(paused: bool)
var car: DriveCar
var pads: Dictionary = {}
var fingers: Dictionary = {}
var mouse_pad := ""
var speed_label: Label
var gear_label: Label
var status_label: Label
var root: Control
var pause_panel: PanelContainer
var paused := false
var ui_steering := 0.0
var ui_throttle := 0.0
var ui_brake := 0.0
const Pad = preload("res://scripts/drive_pad.gd")

func label(text: String, font_size: int, color := Color("f1f5f1")) -> Label:
	var l := Label.new()
	l.text = text
	l.add_theme_font_size_override("font_size", font_size)
	l.add_theme_color_override("font_color", color)
	return l

func button(text: String, action: Callable) -> Button:
	var b := Button.new()
	b.text = text
	b.custom_minimum_size = Vector2(100, 52)
	b.add_theme_font_size_override("font_size", 20)
	var style := StyleBoxFlat.new()
	style.bg_color = Color("173b49")
	style.set_corner_radius_all(12)
	style.content_margin_left = 20
	style.content_margin_right = 20
	b.add_theme_stylebox_override("normal", style)
	b.pressed.connect(action)
	b.focus_mode = Control.FOCUS_NONE
	return b

func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	root = Control.new()
	root.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	root.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(root)
	var brand := VBoxContainer.new()
	brand.position = Vector2(34, 22)
	brand.mouse_filter = Control.MOUSE_FILTER_IGNORE
	root.add_child(brand)
	brand.add_child(label("POCKET DRIVE", 28))
	brand.add_child(label("FREE ROAM  /  PROTOTYPE", 14, Color("173b49")))
	var actions := HBoxContainer.new()
	actions.set_anchors_and_offsets_preset(Control.PRESET_TOP_RIGHT)
	actions.position = Vector2(-260, 22)
	actions.add_theme_constant_override("separation", 12)
	root.add_child(actions)
	actions.add_child(button("Reset", func(): clear_inputs(); reset_requested.emit()))
	actions.add_child(button("Pause", func(): set_paused(not paused)))
	var meter := VBoxContainer.new()
	meter.set_anchors_and_offsets_preset(Control.PRESET_CENTER_BOTTOM)
	meter.position = Vector2(-95, -148)
	meter.size.x = 190
	meter.mouse_filter = Control.MOUSE_FILTER_IGNORE
	root.add_child(meter)
	speed_label = label("0", 62)
	speed_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	meter.add_child(speed_label)
	gear_label = label("N   /   KM/H", 16)
	gear_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	meter.add_child(gear_label)
	status_label = label("Hold BRAKE to stop, then reverse", 16)
	status_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	status_label.set_anchors_and_offsets_preset(Control.PRESET_CENTER_TOP)
	status_label.position = Vector2(-250, 28)
	status_label.size = Vector2(500, 28)
	root.add_child(status_label)
	make_pad("left", "◀", Vector2(32, -156), Vector2(118, 120), false)
	make_pad("right", "▶", Vector2(166, -156), Vector2(118, 120), false)
	make_pad("brake", "BRAKE / R", Vector2(-316, -156), Vector2(132, 120), true)
	make_pad("gas", "GAS", Vector2(-164, -186), Vector2(132, 150), true)
	pause_panel = PanelContainer.new()
	pause_panel.set_anchors_and_offsets_preset(Control.PRESET_CENTER)
	pause_panel.position = Vector2(-210, -150)
	pause_panel.size = Vector2(420, 300)
	var box := StyleBoxFlat.new()
	box.bg_color = Color("102c38")
	box.set_corner_radius_all(24)
	box.set_content_margin_all(30)
	pause_panel.add_theme_stylebox_override("panel", box)
	root.add_child(pause_panel)
	var content := VBoxContainer.new()
	content.add_theme_constant_override("separation", 20)
	pause_panel.add_child(content)
	content.add_child(label("TAKE A BREAK", 28))
	content.add_child(label("Explore at your own pace.", 20))
	content.add_child(button("Resume driving", func(): set_paused(false)))
	content.add_child(button("Reset car", func(): reset_requested.emit(); set_paused(false)))
	content.add_child(button("Car model credits", show_car_credits))
	content.add_child(label("Godot Engine · godotengine.org/license", 14, Color("b5c8ce")))
	pause_panel.hide()

func make_pad(id: String, text: String, offset: Vector2, dimensions: Vector2, right: bool) -> void:
	var pad := Control.new()
	pad.set_script(Pad)
	pad.caption = text
	root.add_child(pad)
	pad.set_anchors_and_offsets_preset(Control.PRESET_BOTTOM_RIGHT if right else Control.PRESET_BOTTOM_LEFT)
	pad.offset_left = offset.x
	pad.offset_top = offset.y
	pad.offset_right = offset.x + dimensions.x
	pad.offset_bottom = offset.y + dimensions.y
	pads[id] = pad

func pad_at(point: Vector2) -> String:
	for id in pads:
		if pads[id].get_global_rect().has_point(point): return id
	return ""

func _input(event: InputEvent) -> void:
	if event is InputEventKey and event.pressed and not event.echo:
		if event.keycode == KEY_ESCAPE: set_paused(not paused)
		if event.keycode == KEY_R and not paused: clear_inputs(); reset_requested.emit()
	if paused: return
	if event is InputEventScreenTouch:
		if event.pressed: fingers[event.index] = pad_at(event.position)
		else: fingers.erase(event.index)
	elif event is InputEventScreenDrag:
		fingers[event.index] = pad_at(event.position)
	elif event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT:
		mouse_pad = pad_at(event.position) if event.pressed else ""
	elif event is InputEventMouseMotion and Input.is_mouse_button_pressed(MOUSE_BUTTON_LEFT):
		mouse_pad = pad_at(event.position)

func held(id: String) -> bool:
	return mouse_pad == id or id in fingers.values()

func _process(_delta: float) -> void:
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
	speed_label.text = str(roundi(absf(car.speed) * 3.6))
	gear_label.text = ("R" if car.speed < -0.2 else ("D" if car.speed > 0.2 else "N")) + "   /   KM/H"

func clear_inputs() -> void:
	fingers.clear()
	mouse_pad = ""
	if is_instance_valid(car):
		car.throttle = 0
		car.brake = 0
		car.steering = 0

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
		if is_instance_valid(pause_panel): set_paused(not paused)

func show_car_credits() -> void:
	var dialog := AcceptDialog.new()
	dialog.title = "Porsche model credits"
	dialog.dialog_text = "Based on (FREE) Porsche 911 Carrera 4S by Karol Miklas.\n\nSource: https://sketchfab.com/3d-models/\nfree-porsche-911-carrera-4s-d01b254483794de3819786d93e0e1ebf\nAuthor: https://sketchfab.com/karolmiklas\n\nModel and this adaptation: Creative Commons BY-SA 4.0\nhttps://creativecommons.org/licenses/by-sa/4.0/\n\nChanges: reduced polygons and textures, removed ground and\nclearcoat shell, separated wheels, adjusted scale and materials.\nVehicle design and trademarks belong to their respective owners."
	dialog.add_theme_font_size_override("font_size", 18)
	root.add_child(dialog)
	dialog.popup_centered(Vector2i(760, 440))
	dialog.confirmed.connect(dialog.queue_free)
	dialog.canceled.connect(dialog.queue_free)
