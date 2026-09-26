extends Node

# Engine, tyre, wind and crash sounds, synthesised once at start-up so the game
# ships no audio files. At runtime the loops are only re-pitched and re-mixed,
# which costs the phone almost nothing.
const RATE := 22050
const GEARS := 6
const IDLE := 900.0
const LOOP_RPM := 3000.0  # the engine loop is synthesised at this crank speed

var car: DriveCar
var engine: AudioStreamPlayer
var screech: AudioStreamPlayer
var wind: AudioStreamPlayer
var impact: AudioStreamPlayer
var horn: AudioStreamPlayer
var tick: AudioStreamPlayer
var engine_filter: AudioEffectLowPassFilter
var rpm := IDLE
var gear := 1
var engine_load := 0.0
var screech_level := 0.0

static func to_wav(samples: PackedFloat32Array, loop: bool) -> AudioStreamWAV:
	var data := PackedByteArray()
	data.resize(samples.size() * 2)
	for i in samples.size():
		data.encode_s16(i * 2, int(clampf(samples[i], -1.0, 1.0) * 32767.0))
	var wav := AudioStreamWAV.new()
	wav.format = AudioStreamWAV.FORMAT_16_BITS
	wav.mix_rate = RATE
	wav.data = data
	if loop:
		wav.loop_mode = AudioStreamWAV.LOOP_FORWARD
		wav.loop_end = samples.size()
	return wav

static func normalized(samples: PackedFloat32Array, peak: float) -> PackedFloat32Array:
	var mean := 0.0
	for v in samples: mean += v
	mean /= samples.size()
	var top := 0.0001
	for i in samples.size():
		samples[i] -= mean
		top = maxf(top, absf(samples[i]))
	for i in samples.size(): samples[i] *= peak / top
	return samples

# Blends the tail into the head so a noisy recording loops without a click.
static func looped(samples: PackedFloat32Array, fade: int) -> PackedFloat32Array:
	var n := samples.size() - fade
	var out := samples.slice(0, n)
	for i in fade:
		var w := float(i) / fade
		out[i] = samples[n + i] * (1.0 - w) + samples[i] * w
	return out

# Crank harmonics plus a noisy burst per firing. A flat-six fires three times per
# turn; slightly unequal cylinders give the rasp. 0.2 s holds exactly ten turns
# and thirty firings, so the loop is seamless.
static func engine_loop() -> AudioStreamWAV:
	var rng := RandomNumberGenerator.new()
	rng.seed = 7
	var n := int(RATE * 0.2)
	var turn := LOOP_RPM / 60.0
	var samples := PackedFloat32Array()
	samples.resize(n)
	for i in n:
		var t := float(i) / RATE
		var v := 0.0
		for k in range(1, 19):
			v += (1.0 if k % 3 == 0 else 0.35) / pow(k, 0.7) * sin(TAU * k * turn * t + k * 1.7)
		samples[i] = v * 0.35
	var cylinders := [1.0, 0.86, 0.95, 0.9, 1.0, 0.82]
	var period := float(RATE) / (turn * 3.0)
	for p in 30:
		var start := int(p * period)
		for j in int(period):
			if start + j >= n: break
			# A short attack avoids a click at the start of each firing.
			var envelope := (1.0 - exp(-j / 6.0)) * exp(-j / (period * 0.2))
			samples[start + j] += (rng.randf_range(-1, 1) * 0.5 + 0.7) * envelope * cylinders[p % 6]
	return to_wav(normalized(samples, 0.85), true)

static func screech_loop() -> AudioStreamWAV:
	var rng := RandomNumberGenerator.new()
	rng.seed = 11
	var fade := int(RATE * 0.2)
	var n := int(RATE * 1.2)
	var samples := PackedFloat32Array()
	samples.resize(n)
	var pitch := 950.0
	var phase := 0.0
	var hiss := 0.0
	for i in n:
		var t := float(i) / RATE
		pitch = clampf(pitch + rng.randf_range(-4, 4), 820, 1120)
		phase += TAU * pitch * (1.0 + 0.035 * sin(TAU * 9.0 * t)) / RATE
		var white := rng.randf_range(-1, 1)
		hiss = white - hiss * 0.6
		samples[i] = (0.55 * sin(phase) + 0.22 * sin(2.0 * phase + 0.3) + 0.3 * hiss) * (0.75 + 0.25 * sin(TAU * 13.0 * t))
	return to_wav(normalized(looped(samples, fade), 0.8), true)

static func wind_loop() -> AudioStreamWAV:
	var rng := RandomNumberGenerator.new()
	rng.seed = 5
	var fade := int(RATE * 0.4)
	var n := int(RATE * 2.4)
	var samples := PackedFloat32Array()
	samples.resize(n)
	var brown := 0.0
	var slow := 0.0
	for i in n:
		var white := rng.randf_range(-1, 1)
		brown = brown * 0.995 + white * 0.05
		slow = slow * 0.9995 + brown * 0.0005
		samples[i] = brown - slow + white * 0.03
	return to_wav(normalized(looped(samples, fade), 0.8), true)

static func impact_sound() -> AudioStreamWAV:
	var rng := RandomNumberGenerator.new()
	rng.seed = 3
	var n := int(RATE * 0.45)
	var samples := PackedFloat32Array()
	samples.resize(n)
	var phase := 0.0
	for i in n:
		var t := float(i) / RATE
		phase += TAU * (35.0 + 110.0 * exp(-t * 6.0)) / RATE
		samples[i] = sin(phase) * exp(-t * 9.0) + rng.randf_range(-1, 1) * 0.7 * exp(-t * 18.0) + sin(TAU * 430.0 * t) * 0.2 * exp(-t * 12.0)
	return to_wav(normalized(samples, 0.95), false)

# Two-tone car horn. Both notes complete whole cycles in 0.2 s, so it loops cleanly.
static func horn_loop() -> AudioStreamWAV:
	var n := int(RATE * 0.2)
	var samples := PackedFloat32Array()
	samples.resize(n)
	for i in n:
		var t := float(i) / RATE
		var v := 0.0
		for note in [420.0, 525.0]:
			for k in range(1, 6):
				v += sin(TAU * note * k * t) / (k * 1.4)
		samples[i] = tanh(v * 0.9)
	return to_wav(normalized(samples, 0.7), true)

# Relay click of the indicator: a short, dull tick.
static func tick_sound() -> AudioStreamWAV:
	var n := int(RATE * 0.04)
	var samples := PackedFloat32Array()
	samples.resize(n)
	for i in n:
		var t := float(i) / RATE
		samples[i] = (sin(TAU * 900.0 * t) * 0.6 + sin(TAU * 2600.0 * t) * 0.4) * exp(-t * 160.0)
	return to_wav(normalized(samples, 0.6), false)

static func click_sound() -> AudioStreamWAV:
	var n := int(RATE * 0.03)
	var samples := PackedFloat32Array()
	samples.resize(n)
	for i in n:
		var t := float(i) / RATE
		samples[i] = sin(TAU * 2100.0 * t) * exp(-t * 180.0)
	return to_wav(normalized(samples, 0.5), false)

func player(label: String, stream: AudioStream, bus := "Master") -> AudioStreamPlayer:
	var p := AudioStreamPlayer.new()
	p.name = label
	p.stream = stream
	p.bus = bus
	add_child(p)
	return p

func _ready() -> void:
	var bus := AudioServer.get_bus_index("Engine")
	if bus == -1:
		bus = AudioServer.bus_count
		AudioServer.add_bus(bus)
		AudioServer.set_bus_name(bus, "Engine")
		AudioServer.set_bus_send(bus, "Master")
		AudioServer.add_bus_effect(bus, AudioEffectLowPassFilter.new())
	engine_filter = AudioServer.get_bus_effect(bus, 0)
	engine = player("Engine", engine_loop(), "Engine")
	screech = player("Screech", screech_loop())
	wind = player("Wind", wind_loop())
	impact = player("Impact", impact_sound())
	horn = player("Horn", horn_loop())
	tick = player("Tick", tick_sound())
	screech.volume_db = -80
	wind.volume_db = -80
	engine.play()
	screech.play()
	wind.play()
	car.crashed.connect(on_crash)
	car.indicator_tick.connect(func(lit: bool): tick.pitch_scale = 1.0 if lit else 0.8; tick.play())

func on_crash(impact_speed: float) -> void:
	impact.volume_db = linear_to_db(clampf(impact_speed / 15.0, 0.3, 1.0))
	impact.pitch_scale = randf_range(0.9, 1.1)
	impact.play()

# Revs follow an automatic gearbox: each gear covers an equal share of the car's
# top speed, climbing from about 58% of the redline to the redline, then shifting.
func target_rpm(redline: float) -> float:
	var speed := absf(car.speed)
	# Out of gear the engine revs freely with the throttle.
	if car.gear in ["P", "N"]: return IDLE + engine_load * (redline * 0.75 - IDLE)
	if speed < 0.5: return IDLE + engine_load * (redline * 0.5 - IDLE)
	if car.speed < 0: return lerpf(1600.0, 4800.0, clampf(speed / 7.0, 0, 1))
	var band: float = car.top_speed / GEARS
	gear = clampi(int(speed / band) + 1, 1, GEARS)
	var within := clampf((speed - (gear - 1) * band) / band, 0, 1)
	return lerpf(1500.0 if gear == 1 else redline * 0.58, redline, within)

func _process(delta: float) -> void:
	if not is_instance_valid(car): return
	if car.horn and not horn.playing: horn.play()
	elif not car.horn and horn.playing: horn.stop()
	var spec := car.info()
	var redline: float = spec.get("redline", 7500.0)
	var speed := absf(car.speed)
	engine_load = move_toward(engine_load, car.throttle, delta * 4.0)
	var target := target_rpm(redline)
	# Revs drop quickly on an upshift and rise more gently.
	rpm = lerpf(rpm, target, 1.0 - exp(-delta * (14.0 if target < rpm else 7.0)))
	var rev := clampf(rpm / redline, 0, 1)
	engine.pitch_scale = rpm / LOOP_RPM * spec.get("engine_pitch", 1.0)
	engine.volume_db = linear_to_db(0.3 + 0.45 * engine_load + 0.25 * rev)
	engine_filter.cutoff_hz = 900.0 + 5500.0 * engine_load + 1500.0 * rev
	var slip := absf(car.steering) * clampf((speed - 9.0) / 12.0, 0, 1)
	if car.brake > 0 and car.speed > 7.0: slip = maxf(slip, 0.8)
	screech_level = move_toward(screech_level, slip, delta * 6.0)
	screech.volume_db = linear_to_db(maxf(screech_level * 0.55, 0.0001))
	screech.pitch_scale = 0.9 + 0.2 * screech_level
	var rush := clampf(speed / 30.0, 0, 1)
	wind.volume_db = linear_to_db(maxf(rush * rush * 0.6, 0.0001))
	wind.pitch_scale = 0.7 + 0.5 * rush
