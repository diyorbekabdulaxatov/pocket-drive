extends Node3D

const CarScript = preload("res://scripts/car.gd")
const HudScript = preload("res://scripts/hud.gd")
const MinimapScript = preload("res://scripts/minimap.gd")

# A modern city in metres; north is -Z. Seven streets per axis plus a perimeter
# loop form the road grid, and the two central boulevards meet at a roundabout.
# Static geometry is merged per material and per 160 m chunk, so the phone
# draws a few hundred meshes at most and can still skip chunks out of view.
const HALF := 320.0
const RING := 288.0
const GRID := [-240.0, -160.0, -80.0, 0.0, 80.0, 160.0, 240.0]
const CHUNK := 160.0
const ROUNDABOUT := 22.0
const ISLAND := 8.0
const SIDEWALK := 4.0
const Y_ROAD := 0.02
const Y_MARK := 0.04
const Y_WALK := 0.06
const Y_DISC := 0.08
const FLAT := ["asphalt", "mark", "walk", "plaza", "lawn", "grass", "water"]
const WHITE := Color(1, 1, 1)
const YELLOW := Color(1.0, 0.78, 0.25)
const GLASS_TINTS := [Color(0.78, 0.9, 1.0), Color(0.72, 1.0, 0.95), Color(1.0, 0.9, 0.76), Color(0.92, 0.94, 0.97), Color(0.82, 0.86, 1.0)]
const OFFICE_TINTS := [Color(1, 1, 1), Color(0.93, 0.95, 1.0), Color(1.0, 0.96, 0.9)]
const HOME_TINTS := [Color(1, 1, 1), Color(1.0, 0.93, 0.82), Color(0.97, 0.82, 0.72), Color(0.86, 0.9, 0.93), Color(0.9, 0.96, 0.88)]

class Batch:
	var material: String
	var verts := PackedVector3Array()
	var normals := PackedVector3Array()
	var uvs := PackedVector2Array()
	var colors := PackedColorArray()
	var indices := PackedInt32Array()

	func poly(points: PackedVector3Array, normal: Vector3, coords: PackedVector2Array, color: Color) -> void:
		var base := verts.size()
		verts.append_array(points)
		uvs.append_array(coords)
		for k in points.size():
			normals.append(normal)
			colors.append(color)
		# Godot treats clockwise triangles as front faces.
		var counter_clockwise := (points[1] - points[0]).cross(points[2] - points[0]).dot(normal) > 0.0
		for k in range(1, points.size() - 1):
			indices.append(base)
			indices.append(base + (k + 1 if counter_clockwise else k))
			indices.append(base + (k if counter_clockwise else k + 1))

var car: DriveCar
var camera: Camera3D
var hud: CanvasLayer
var minimap: Control
var camera_ready := false
var random := RandomNumberGenerator.new()
var lines: Array[float] = []
var road_rects: Array[Rect2] = []
var buildings: Array[AABB] = []
var trees: Array[Vector3] = []
var tree_transforms: Array[Transform3D] = []
var tree_colors: Array[Color] = []
var batches := {}
var materials := {}
var colliders: StaticBody3D

# --- Geometry helpers -------------------------------------------------------

func batch(id: String, at: Vector3) -> Batch:
	var key := "%s/%d/%d" % [id, floori(at.x / CHUNK), floori(at.z / CHUNK)]
	if not batches.has(key):
		var b := Batch.new()
		b.material = id
		batches[key] = b
	return batches[key]

func flat(id: String, r: Rect2, y: float, color := WHITE) -> void:
	var p := PackedVector3Array([Vector3(r.position.x, y, r.position.y), Vector3(r.end.x, y, r.position.y), Vector3(r.end.x, y, r.end.y), Vector3(r.position.x, y, r.end.y)])
	var uv := PackedVector2Array()
	for v in p: uv.append(Vector2(v.x, v.z))
	batch(id, Vector3(r.get_center().x, y, r.get_center().y)).poly(p, Vector3.UP, uv, color)

func upright(id: String, a: Vector2, b: Vector2, y0: float, y1: float, normal: Vector3, color := WHITE) -> void:
	var p := PackedVector3Array([Vector3(a.x, y0, a.y), Vector3(b.x, y0, b.y), Vector3(b.x, y1, b.y), Vector3(a.x, y1, a.y)])
	var length := a.distance_to(b)
	var uv := PackedVector2Array([Vector2(0, -y0), Vector2(length, -y0), Vector2(length, -y1), Vector2(0, -y1)])
	batch(id, (p[0] + p[2]) / 2).poly(p, normal, uv, color)

func ring(id: String, centre: Vector2, r0: float, r1: float, y: float, segments: int, color := WHITE, dashed := false) -> void:
	for k in segments:
		if dashed and k % 2 == 1: continue
		var a := Vector2.from_angle(TAU * k / segments)
		var b := Vector2.from_angle(TAU * (k + 1) / segments)
		var flat_points := [centre + a * r0, centre + a * r1, centre + b * r1, centre + b * r0]
		var p := PackedVector3Array()
		var uv := PackedVector2Array()
		for q in flat_points:
			p.append(Vector3(q.x, y, q.y))
			uv.append(q)
		batch(id, p[1]).poly(p, Vector3.UP, uv, color)

# Sides use metres along the face and height above ground as UVs, so facade
# textures line up with floors and the edges of each building.
func box(center: Vector3, size: Vector3, side: String, top := "", color := WHITE, solid := false, turn := 0.0) -> void:
	var basis := Basis(Vector3.UP, turn)
	var h := size / 2.0
	for axes in [[Vector3.UP, Vector3.RIGHT, Vector3.BACK], [Vector3.RIGHT, Vector3.BACK, Vector3.UP], [Vector3.LEFT, Vector3.BACK, Vector3.UP], [Vector3.BACK, Vector3.RIGHT, Vector3.UP], [Vector3.FORWARD, Vector3.RIGHT, Vector3.UP]]:
		var n: Vector3 = axes[0]
		var u: Vector3 = axes[1]
		var v: Vector3 = axes[2]
		var eu := absf(u.dot(h))
		var ev := absf(v.dot(h))
		var mid := n * absf(n.dot(h))
		var p := PackedVector3Array()
		var uv := PackedVector2Array()
		for corner in [mid - u * eu - v * ev, mid + u * eu - v * ev, mid + u * eu + v * ev, mid - u * eu + v * ev]:
			var point: Vector3 = center + basis * corner
			p.append(point)
			uv.append(Vector2(point.x, point.z) if n == Vector3.UP else Vector2(corner.dot(u) + eu, -point.y))
		batch(top if n == Vector3.UP and top != "" else side, center).poly(p, basis * n, uv, color)
	if solid: add_collider(center, size, basis)

func add_collider(center: Vector3, size: Vector3, basis := Basis()) -> void:
	var shape := CollisionShape3D.new()
	var bounds := BoxShape3D.new()
	bounds.size = size
	shape.shape = bounds
	shape.transform = Transform3D(basis, center)
	colliders.add_child(shape)

func building(base: Vector3, size: Vector3, facade: String, color: Color) -> void:
	box(base + Vector3(0, size.y / 2, 0), size, facade, "roof", color, true)
	buildings.append(AABB(base - Vector3(size.x / 2, 0, size.z / 2), size))

func tree(at: Vector3, scale := 1.0) -> void:
	box(at + Vector3(0, 1.3 * scale, 0), Vector3(0.4, 2.6, 0.4) * scale, "bark", "", WHITE, true)
	var crown := scale * random.randf_range(0.85, 1.15)
	tree_transforms.append(Transform3D(Basis().scaled(Vector3.ONE * crown), at + Vector3(0, 4.3 * scale, 0)))
	tree_colors.append(Color(0.3, 0.52, 0.34).lerp(Color(0.52, 0.68, 0.34), random.randf()))
	trees.append(at)

# Road-relative coordinates: "across" is the road's cross axis, "along" its length.
func pt(vertical: bool, across: float, along: float, y: float) -> Vector3:
	return Vector3(across, y, along) if vertical else Vector3(along, y, across)

func span(vertical: bool, across: float, width: float, from: float, to: float) -> Rect2:
	return Rect2(across - width / 2, from, width, to - from) if vertical else Rect2(from, across - width / 2, to - from, width)

func split(r: Rect2, columns: int, rows: int, gap: float) -> Array[Rect2]:
	var out: Array[Rect2] = []
	var w := (r.size.x - gap * (columns - 1)) / columns
	var d := (r.size.y - gap * (rows - 1)) / rows
	for i in columns:
		for j in rows:
			out.append(Rect2(r.position.x + i * (w + gap), r.position.y + j * (d + gap), w, d))
	return out

# --- Queries used by the car, minimap and tests ------------------------------

func road_width(c: float) -> float:
	if c == 0.0: return 24.0
	if absf(c) == RING: return 16.0
	return 14.0

func is_on_road(p: Vector3) -> bool:
	var q := Vector2(p.x, p.z)
	if q.length() < ROUNDABOUT: return true
	for r in road_rects:
		if r.has_point(q): return true
	return false

# --- Materials --------------------------------------------------------------

func paint(painter: Callable, size := 64) -> ImageTexture:
	var image := Image.create_empty(size, size, false, Image.FORMAT_RGB8)
	for y in size:
		for x in size:
			image.set_pixel(x, y, painter.call(x, y))
	image.generate_mipmaps()
	return ImageTexture.create_from_image(image)

func noisy(base: Color, amount: float) -> Color:
	var d := random.randf_range(-amount, amount)
	return Color(base.r + d, base.g + d, base.b + d)

func glass_pixel(x: int, y: int) -> Color:
	if x < 2 or y < 2: return Color(0.84, 0.87, 0.9)
	if y > 50: return Color(0.2, 0.24, 0.28)
	return Color(0.64, 0.78, 0.88).lerp(Color(0.25, 0.37, 0.47), y / 50.0)

func office_pixel(x: int, y: int) -> Color:
	if y < 22: return noisy(Color(0.84, 0.84, 0.81), 0.015)
	if x % 32 < 2: return Color(0.7, 0.72, 0.74)
	return Color(0.42, 0.52, 0.6).lerp(Color(0.24, 0.32, 0.4), (y - 22) / 42.0)

func home_pixel(x: int, y: int) -> Color:
	if y >= 56: return Color(0.62, 0.6, 0.58)
	if x >= 14 and x < 50 and y >= 10 and y < 48:
		if x < 16 or x >= 48 or y < 12 or y >= 46: return Color(0.25, 0.25, 0.27)
		return Color(0.36, 0.46, 0.56).lerp(Color(0.5, 0.6, 0.68), (48 - y) / 38.0)
	return noisy(Color(0.88, 0.87, 0.83), 0.012)

func make_material(id: String, texture: Texture2D, tile: Vector2, albedo := WHITE, rough := 0.9, metal := 0.0, unshaded := false) -> void:
	var m := StandardMaterial3D.new()
	m.vertex_color_use_as_albedo = true
	m.vertex_color_is_srgb = true
	m.albedo_color = albedo
	m.roughness = rough
	m.metallic = metal
	# Matte surfaces reflect little sky; full-strength reflections washed the street out.
	if metal == 0.0: m.metallic_specular = 0.25
	if texture:
		m.albedo_texture = texture
		m.uv1_scale = Vector3(1.0 / tile.x, 1.0 / tile.y, 1)
		m.texture_filter = BaseMaterial3D.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS_ANISOTROPIC
	if unshaded: m.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	materials[id] = m

func build_materials() -> void:
	make_material("grass", paint(func(_x, _y): return noisy(Color(0.46, 0.62, 0.39), 0.05)), Vector2(6, 6))
	make_material("lawn", paint(func(_x, _y): return noisy(Color(0.42, 0.62, 0.37), 0.06)), Vector2(4, 4))
	make_material("asphalt", paint(func(_x, _y): return noisy(Color(0.26, 0.28, 0.3), 0.03)), Vector2(5, 5))
	make_material("roof", paint(func(_x, _y): return noisy(Color(0.58, 0.59, 0.6), 0.04)), Vector2(4, 4))
	make_material("walk", paint(func(x, y): return Color(0.5, 0.5, 0.5) if x % 32 == 0 or y % 32 == 0 else noisy(Color(0.62, 0.62, 0.61), 0.02)), Vector2(2, 2))
	make_material("plaza", paint(func(x, y): return Color(0.63, 0.59, 0.55) if x % 16 == 0 or y % 16 == 0 else noisy(Color(0.78, 0.74, 0.68), 0.02)), Vector2(3, 3))
	make_material("glass", paint(glass_pixel), Vector2(3.0, 3.6), WHITE, 0.15, 0.3)
	make_material("office", paint(office_pixel), Vector2(3.0, 3.5), WHITE, 0.4)
	make_material("residential", paint(home_pixel), Vector2(4.0, 3.0))
	make_material("concrete", null, Vector2.ONE, Color(0.8, 0.8, 0.78), 0.85)
	make_material("metal", null, Vector2.ONE, Color(0.24, 0.26, 0.28), 0.45, 0.5)
	make_material("bark", null, Vector2.ONE, Color(0.45, 0.35, 0.26))
	make_material("mark", null, Vector2.ONE, Color(0.93, 0.93, 0.9), 0.7)
	make_material("water", null, Vector2.ONE, Color(0.24, 0.44, 0.55), 0.08, 0.2)
	make_material("light", null, Vector2.ONE, WHITE, 1.0, 0.0, true)
	make_material("leaf", null, Vector2.ONE)

# --- Roads ------------------------------------------------------------------

func road_end(c: float, cross: float) -> float:
	return ROUNDABOUT if c == 0.0 and cross == 0.0 else road_width(cross) / 2

func build_roads() -> void:
	for c in lines:
		var w := road_width(c)
		road_rects.append(Rect2(c - w / 2, -RING - 8, w, 2 * RING + 16))
		road_rects.append(Rect2(-RING - 8, c - w / 2, 2 * RING + 16, w))
	for vertical in [true, false]:
		for c in lines:
			for k in lines.size() - 1:
				road_segment(vertical, c, lines[k] + road_end(c, lines[k]), lines[k + 1] - road_end(c, lines[k + 1]))
	for cx in lines:
		for cz in lines:
			if cx == 0.0 and cz == 0.0: continue
			flat("asphalt", Rect2(cx - road_width(cx) / 2, cz - road_width(cz) / 2, road_width(cx), road_width(cz)), Y_ROAD)
			if cx in GRID and cz in GRID: traffic_lights(cx, cz)
	roundabout()

func road_segment(vertical: bool, c: float, from: float, to: float) -> void:
	var w := road_width(c)
	flat("asphalt", span(vertical, c, w, from, to), Y_ROAD)
	for s in [-1.0, 1.0]:
		flat("mark", span(vertical, c + s * (w / 2 - 0.45), 0.15, from + 5, to - 5), Y_MARK)
		if c == 0.0:
			for lane in [1.0, 2.0]: dashes(vertical, c + s * (2.0 + lane * 10.0 / 3.0), from, to)
		else:
			flat("mark", span(vertical, c + s * 0.18, 0.12, from + 5, to - 5), Y_MARK, YELLOW)
			dashes(vertical, c + s * w / 4, from, to)
	if c == 0.0:
		# Planted median: a low kerb the car cannot cross, with a row of trees.
		var length := to - from
		box(pt(vertical, c, (from + to) / 2, 0.1), Vector3(4, 0.2, length) if vertical else Vector3(length, 0.2, 4), "concrete", "lawn", WHITE, true)
		var spot := from + 10.0
		while spot < to - 8.0:
			tree(pt(vertical, c, spot, 0.2), 0.8)
			spot += 20.0
	for edge in [[from + 1.0, 1.0], [to - 1.0, -1.0]]:
		if c == 0.0 and absf(edge[0]) < ROUNDABOUT + 2: continue
		zebra(vertical, c, w, edge[0], edge[1])
	var along := from + 12.0
	while along < to - 10.0:
		for s in [-1.0, 1.0]: lamp(vertical, c + s * (w / 2 + 1.2), along, -s)
		along += 30.0

func dashes(vertical: bool, across: float, from: float, to: float) -> void:
	var at := from + 6.0
	while at + 3.0 < to - 6.0:
		flat("mark", span(vertical, across, 0.14, at, at + 3.0), Y_MARK)
		at += 9.0

func zebra(vertical: bool, c: float, w: float, edge: float, direction: float) -> void:
	var a := edge if direction > 0 else edge - 3.0
	var across := c - w / 2 + 1.0
	while across + 0.5 < c + w / 2 - 0.7:
		if c != 0.0 or absf(across + 0.25 - c) > 2.4:
			flat("mark", span(vertical, across + 0.25, 0.5, a, a + 3.0), Y_MARK)
		across += 1.0

func lamp(vertical: bool, across: float, along: float, facing: float) -> void:
	var base := pt(vertical, across, along, 0.0)
	var reach := pt(vertical, facing * 1.1, 0.0, 0.0)
	box(base + Vector3(0, 4, 0), Vector3(0.22, 8, 0.22), "metal", "", WHITE, true)
	box(base + reach + Vector3(0, 7.9, 0), Vector3(2.2, 0.12, 0.14) if vertical else Vector3(0.14, 0.12, 2.2), "metal")
	box(base + reach * 1.8 + Vector3(0, 7.8, 0), Vector3(0.9, 0.1, 0.32) if vertical else Vector3(0.32, 0.1, 0.9), "light", "", Color(1.0, 0.96, 0.85))

func traffic_lights(cx: float, cz: float) -> void:
	var signal_colors := [Color(0.5, 0.08, 0.06), Color(0.55, 0.4, 0.08), Color(0.3, 1.0, 0.55)]
	for s in [-1.0, 1.0]:
		var base := Vector3(cx + s * (road_width(cx) / 2 + 1.6), 0, cz + s * (road_width(cz) / 2 + 1.6))
		box(base + Vector3(0, 2.6, 0), Vector3(0.2, 5.2, 0.2), "metal", "", WHITE, true)
		box(base + Vector3(0, 5.6, 0), Vector3(0.44, 1.3, 0.44), "metal")
		for k in 3: box(base + Vector3(0, 6.0 - k * 0.4, 0), Vector3(0.5, 0.26, 0.5), "light", "", signal_colors[k])

func roundabout() -> void:
	ring("asphalt", Vector2.ZERO, 0.0, ROUNDABOUT, Y_DISC, 64)
	ring("mark", Vector2.ZERO, ROUNDABOUT - 0.6, ROUNDABOUT - 0.45, Y_DISC + 0.02, 64)
	ring("mark", Vector2.ZERO, 14.9, 15.05, Y_DISC + 0.02, 64, WHITE, true)
	ring("lawn", Vector2.ZERO, 0.0, ISLAND, 0.4, 48)
	for k in 48:
		var a := Vector2.from_angle(TAU * k / 48) * ISLAND
		var b := Vector2.from_angle(TAU * (k + 1) / 48) * ISLAND
		var out := (a + b).normalized()
		upright("concrete", a, b, 0.0, 0.4, Vector3(out.x, 0, out.y))
	var shape := CollisionShape3D.new()
	var cylinder := CylinderShape3D.new()
	cylinder.radius = ISLAND
	cylinder.height = 1.2
	shape.shape = cylinder
	shape.position.y = 0.6
	colliders.add_child(shape)
	# A twisting glass sculpture marks the city centre.
	for k in 10:
		box(Vector3(0, 1.1 + k * 1.45, 0), Vector3(5.6 - k * 0.32, 1.3, 1.2), "glass", "roof", Color(0.85, 0.95, 1.0), false, k * 0.2)

# --- City blocks ------------------------------------------------------------

func build_blocks() -> void:
	var last := lines.size() - 2
	for i in lines.size() - 1:
		for j in lines.size() - 1:
			var x0: float = lines[i] + road_width(lines[i]) / 2
			var x1: float = lines[i + 1] - road_width(lines[i + 1]) / 2
			var z0: float = lines[j] + road_width(lines[j]) / 2
			var z1: float = lines[j + 1] - road_width(lines[j + 1]) / 2
			var r := Rect2(x0, z0, x1 - x0, z1 - z0)
			var distance := maxf(absf(r.get_center().x), absf(r.get_center().y))
			if i == 0 or j == 0 or i == last or j == last:
				if i == 0 and j == 4: drift_lot(r)
				else: green_belt(r)
			elif i == 2 and j == 5: park(r)
			elif i == 6 and j == 1: mall(r)
			elif distance < 80: downtown(r)
			elif distance < 160: midtown(r)
			else: residential(r)

func sidewalk(r: Rect2, inner_id: String) -> Rect2:
	var inner := r.grow(-SIDEWALK)
	flat("walk", Rect2(r.position.x, r.position.y, r.size.x, SIDEWALK), Y_WALK)
	flat("walk", Rect2(r.position.x, inner.end.y, r.size.x, SIDEWALK), Y_WALK)
	flat("walk", Rect2(r.position.x, inner.position.y, SIDEWALK, inner.size.y), Y_WALK)
	flat("walk", Rect2(inner.end.x, inner.position.y, SIDEWALK, inner.size.y), Y_WALK)
	flat(inner_id, inner, Y_WALK)
	var a := r.position
	var b := Vector2(r.end.x, r.position.y)
	var c := r.end
	var d := Vector2(r.position.x, r.end.y)
	upright("concrete", a, b, 0.0, Y_WALK, Vector3.FORWARD)
	upright("concrete", b, c, 0.0, Y_WALK, Vector3.RIGHT)
	upright("concrete", c, d, 0.0, Y_WALK, Vector3.BACK)
	upright("concrete", d, a, 0.0, Y_WALK, Vector3.LEFT)
	return inner

func tower(base: Vector3, w: float, d: float, h: float) -> void:
	var tint: Color = GLASS_TINTS[random.randi() % GLASS_TINTS.size()]
	building(base, Vector3(w, h, d), "glass", tint)
	for sx in [-1.0, 1.0]:
		for sz in [-1.0, 1.0]:
			box(base + Vector3(sx * w / 2, h / 2 + 0.3, sz * d / 2), Vector3(0.7, h + 0.6, 0.7), "concrete", "", Color(0.95, 0.96, 0.98))
	box(base + Vector3(0, h + 3, 0), Vector3(w * 0.72, 6, d * 0.72), "glass", "roof", tint.darkened(0.15))
	box(base + Vector3(w * 0.15, h + 7, -d * 0.1), Vector3(4, 2.5, 3), "concrete", "roof")
	if h > 120: box(base + Vector3(0, h + 14, 0), Vector3(0.4, 16, 0.4), "metal")

func downtown(r: Rect2) -> void:
	var inner := sidewalk(r, "plaza")
	var c := inner.get_center()
	var podium := minf(inner.size.x, inner.size.y) - 14.0
	var podium_height := random.randf_range(6.0, 10.0)
	building(Vector3(c.x, 0, c.y), Vector3(podium, podium_height, podium), "office", Color(0.95, 0.95, 0.93))
	var w := random.randf_range(20.0, 26.0)
	tower(Vector3(c.x, podium_height, c.y), w, w * random.randf_range(0.8, 1.0), random.randf_range(85.0, 150.0))
	for sx in [-1.0, 1.0]:
		for sz in [-1.0, 1.0]:
			tree(Vector3(c.x + sx * (podium / 2 + 3.5), Y_WALK, c.y + sz * (podium / 2 + 3.5)))

func midtown(r: Rect2) -> void:
	var inner := sidewalk(r, "plaza")
	for lot in split(inner, 2, 2, 3.0):
		var c := lot.get_center()
		if random.randf() < 0.18:
			tree(Vector3(c.x - 4, Y_WALK, c.y), 0.9)
			tree(Vector3(c.x + 4, Y_WALK, c.y), 0.9)
			continue
		var size := Vector3(lot.size.x - random.randf_range(3.0, 7.0), random.randf_range(18.0, 64.0), lot.size.y - random.randf_range(3.0, 7.0))
		if random.randf() < 0.55: building(Vector3(c.x, 0, c.y), size, "glass", GLASS_TINTS[random.randi() % GLASS_TINTS.size()])
		else: building(Vector3(c.x, 0, c.y), size, "office", OFFICE_TINTS[random.randi() % OFFICE_TINTS.size()])
		box(Vector3(c.x, size.y + 1.2, c.y), Vector3(size.x * 0.4, 2.4, size.z * 0.35), "concrete", "roof")

func residential(r: Rect2) -> void:
	var inner := sidewalk(r, "lawn")
	for lot in split(inner, 2, 2, 6.0):
		var c := lot.get_center()
		var floors := random.randi_range(3, 9)
		var size := Vector3(minf(lot.size.x - 6.0, random.randf_range(14.0, 22.0)), floors * 3.0, minf(lot.size.y - 6.0, random.randf_range(14.0, 22.0)))
		building(Vector3(c.x, 0, c.y), size, "residential", HOME_TINTS[random.randi() % HOME_TINTS.size()])
		box(Vector3(c.x, size.y + 0.8, c.y), Vector3(size.x * 0.3, 1.6, size.z * 0.3), "concrete", "roof")
		tree(Vector3(lot.position.x + 2.0, Y_WALK, lot.position.y + 2.0), 0.8)
		tree(Vector3(lot.end.x - 2.0, Y_WALK, lot.end.y - 2.0), 0.8)

func park(r: Rect2) -> void:
	var inner := sidewalk(r, "lawn")
	var c := inner.get_center()
	flat("plaza", Rect2(c.x - 2, inner.position.y, 4, inner.size.y), Y_WALK + 0.02)
	flat("plaza", Rect2(inner.position.x, c.y - 2, inner.size.x, 4), Y_WALK + 0.02)
	var pond := Vector2(c.x + inner.size.x / 4, c.y - inner.size.y / 4)
	ring("water", pond, 0.0, 9.0, Y_WALK + 0.02, 40)
	var placed := 0
	while placed < 36:
		var p := Vector2(random.randf_range(inner.position.x + 3, inner.end.x - 3), random.randf_range(inner.position.y + 3, inner.end.y - 3))
		if absf(p.x - c.x) < 5 or absf(p.y - c.y) < 5 or p.distance_to(pond) < 12: continue
		tree(Vector3(p.x, Y_WALK, p.y), random.randf_range(0.8, 1.2))
		placed += 1

func mall(r: Rect2) -> void:
	var inner := sidewalk(r, "plaza")
	var hall := Rect2(inner.position.x + 3, inner.position.y + 3, inner.size.x - 6, inner.size.y * 0.42)
	var hall_centre := hall.get_center()
	building(Vector3(hall_centre.x, 0, hall_centre.y), Vector3(hall.size.x, 13, hall.size.y), "glass", Color(1.0, 0.92, 0.82))
	box(Vector3(hall_centre.x, 5, hall.end.y + 2), Vector3(hall.size.x * 0.5, 0.4, 4), "concrete", "roof")
	var lot := Rect2(inner.position.x + 2, hall.end.y + 5, inner.size.x - 4, inner.end.y - hall.end.y - 7)
	flat("asphalt", lot, Y_WALK + 0.02)
	road_rects.append(lot)
	for row in [lot.position.y + 1.0, lot.end.y - 6.0]:
		var x := lot.position.x + 2.0
		while x < lot.end.x - 2.0:
			flat("mark", Rect2(x, row, 0.12, 5.0), Y_WALK + 0.04)
			x += 2.7

func drift_lot(r: Rect2) -> void:
	var lot := r.grow(-2.0)
	flat("asphalt", lot, Y_ROAD)
	road_rects.append(lot)
	for k in 2:
		var centre := Vector2(lot.get_center().x, lot.position.y + lot.size.y * (0.27 + k * 0.46))
		ring("mark", centre, 9.8, 10.0, Y_MARK, 48)
		for n in 8:
			var p := centre + Vector2.from_angle(TAU * n / 8) * 5.0
			box(Vector3(p.x, 0.3, p.y), Vector3(0.35, 0.6, 0.35), "concrete", "", Color(1.0, 0.45, 0.15))

func green_belt(r: Rect2) -> void:
	var x := r.position.x + 6.0
	while x < r.end.x - 4.0:
		var z := r.position.y + 6.0
		while z < r.end.y - 4.0:
			if random.randf() < 0.55:
				tree(Vector3(x + random.randf_range(-2.5, 2.5), 0, z + random.randf_range(-2.5, 2.5)), random.randf_range(0.8, 1.3))
			z += 12.0
		x += 12.0

func perimeter() -> void:
	for s in [-1.0, 1.0]:
		box(Vector3(0, 0.6, s * HALF), Vector3(2 * HALF + 0.8, 1.2, 0.8), "concrete", "", WHITE, true)
		box(Vector3(s * HALF, 0.6, 0), Vector3(0.8, 1.2, 2 * HALF + 0.8), "concrete", "", WHITE, true)
		var at := -HALF + 12.0
		while at < HALF - 10.0:
			if random.randf() < 0.8: tree(Vector3(at, 0, s * 308.0), random.randf_range(0.9, 1.3))
			if random.randf() < 0.8: tree(Vector3(s * 308.0, 0, at), random.randf_range(0.9, 1.3))
			at += 16.0

# Unreachable towers beyond the wall so the city appears to continue into the haze.
func skyline() -> void:
	for n in 40:
		var p := Vector2.from_angle(random.randf() * TAU) * random.randf_range(380.0, 560.0)
		var size := Vector3(random.randf_range(20, 40), random.randf_range(40, 170), random.randf_range(20, 40))
		box(Vector3(p.x, size.y / 2, p.y), size, "glass", "roof", GLASS_TINTS[n % GLASS_TINTS.size()])

func build_meshes() -> void:
	for key in batches:
		var b: Batch = batches[key]
		var arrays := []
		arrays.resize(Mesh.ARRAY_MAX)
		arrays[Mesh.ARRAY_VERTEX] = b.verts
		arrays[Mesh.ARRAY_NORMAL] = b.normals
		arrays[Mesh.ARRAY_TEX_UV] = b.uvs
		arrays[Mesh.ARRAY_COLOR] = b.colors
		arrays[Mesh.ARRAY_INDEX] = b.indices
		var mesh := ArrayMesh.new()
		mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)
		mesh.surface_set_material(0, materials[b.material])
		var node := MeshInstance3D.new()
		node.name = String(key).replace("/", "_")
		node.mesh = mesh
		if b.material in FLAT: node.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		add_child(node)
	batches.clear()
	var crown := SphereMesh.new()
	crown.radius = 2.2
	crown.height = 4.6
	crown.radial_segments = 8
	crown.rings = 4
	crown.material = materials.leaf
	var multi := MultiMesh.new()
	multi.transform_format = MultiMesh.TRANSFORM_3D
	multi.use_colors = true
	multi.mesh = crown
	multi.instance_count = tree_transforms.size()
	for k in tree_transforms.size():
		multi.set_instance_transform(k, tree_transforms[k])
		multi.set_instance_color(k, tree_colors[k])
	var canopies := MultiMeshInstance3D.new()
	canopies.name = "TreeCanopies"
	canopies.multimesh = multi
	add_child(canopies)

func _ready() -> void:
	random.seed = 1234
	var environment := WorldEnvironment.new()
	var env := Environment.new()
	env.background_mode = Environment.BG_SKY
	var sky := Sky.new()
	var sky_mat := ProceduralSkyMaterial.new()
	sky_mat.sky_top_color = Color("5f9fc6")
	sky_mat.sky_horizon_color = Color("cfe2e6")
	sky_mat.ground_horizon_color = Color("cfe2e6")
	sky_mat.ground_bottom_color = Color("b9cdc9")
	sky.sky_material = sky_mat
	env.sky = sky
	env.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	env.ambient_light_color = Color("dae7ee")
	env.ambient_light_energy = 0.45
	env.tonemap_mode = Environment.TONE_MAPPER_AGX
	env.fog_enabled = true
	env.fog_light_color = Color("cfe2e6")
	env.fog_density = 0.0018
	env.fog_sky_affect = 0.0
	environment.environment = env
	add_child(environment)
	var sun := DirectionalLight3D.new()
	sun.rotation_degrees = Vector3(-48, -28, 0)
	sun.light_color = Color("fff0d7")
	sun.light_energy = 1.1
	sun.shadow_enabled = true
	sun.directional_shadow_max_distance = 80
	add_child(sun)
	build_materials()
	colliders = StaticBody3D.new()
	colliders.name = "CityColliders"
	add_child(colliders)
	add_collider(Vector3(0, -0.5, 0), Vector3(1400, 1, 1400))
	flat("grass", Rect2(-700, -700, 1400, 1400), 0.0)
	lines.append(-RING)
	for g in GRID: lines.append(g)
	lines.append(RING)
	build_roads()
	build_blocks()
	perimeter()
	skyline()
	build_meshes()
	car = CarScript.new()
	car.name = "Car"
	car.spawn = Vector3(7, 0.08, 64)
	car.road_check = is_on_road
	car.world_limit = HALF + 20
	add_child(car)
	camera = Camera3D.new()
	camera.name = "FollowCamera"
	camera.fov = 65
	camera.near = 0.2
	camera.far = 600
	camera.current = true
	add_child(camera)
	hud = CanvasLayer.new()
	hud.set_script(HudScript)
	hud.car = car
	add_child(hud)
	hud.reset_requested.connect(func(): car.reset_car(); camera_ready = false)
	minimap = Control.new()
	minimap.set_script(MinimapScript)
	minimap.world = self
	minimap.position = Vector2(34, 92)
	minimap.size = Vector2(170, 170)
	hud.root.add_child(minimap)

func _physics_process(delta: float) -> void:
	var target := car.global_position + Vector3(0, 1.3, 0)
	var desired: Vector3 = target + car.global_basis.z * (5.3 + absf(car.speed) * 0.035) + Vector3(0, 2.6, 0)
	var query := PhysicsRayQueryParameters3D.create(target, desired)
	query.exclude = [car.get_rid()]
	var hit := get_world_3d().direct_space_state.intersect_ray(query)
	if not hit.is_empty(): desired = hit.position + hit.normal * 0.4
	if not camera_ready:
		camera.global_position = desired
		camera_ready = true
	else: camera.global_position = camera.global_position.lerp(desired, 1 - exp(-7 * delta))
	camera.look_at(target + -car.global_basis.z * 2.8, Vector3.UP)
