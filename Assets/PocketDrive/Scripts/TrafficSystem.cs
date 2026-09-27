using System.Collections.Generic;
using UnityEngine;

namespace PocketDrive
{
    // AI traffic on a square street grid: cars keep to the right-hand lane, turn at intersections,
    // obey timed traffic lights and brake for anything with a Rigidbody ahead (the player, other cars).
    // Only a small pool of cars exists; cars far from the player are moved to new spots nearby.
    public sealed class TrafficSystem : MonoBehaviour
    {
        [Header("Street grid")]
        [SerializeField] float gridMin = -300f;
        [SerializeField] float gridSpacing = 150f;
        [SerializeField] int gridCount = 5;
        [SerializeField] float roadHeight = .05f;
        [SerializeField] float laneOffset = 7f;
        [SerializeField] float stopLine = 19f;

        [Header("Cars")]
        [SerializeField] GameObject carTemplate;
        [SerializeField] int carCount = 12;
        [SerializeField] float cruiseSpeed = 12f;
        [SerializeField] float turnSpeed = 6f;
        [SerializeField] float acceleration = 4f;
        [SerializeField] float braking = 9f;
        [SerializeField] float spawnRadius = 220f;
        [SerializeField] float despawnRadius = 300f;
        [SerializeField] float minSpawnDistance = 45f;
        [SerializeField] Color[] paints =
        {
            new(.02f, .02f, .025f), new(.8f, .8f, .82f), new(.3f, .31f, .33f), new(.02f, .06f, .22f),
            new(.55f, .02f, .02f), new(.9f, .9f, .9f), new(.05f, .12f, .06f), new(.45f, .28f, .08f)
        };

        [Header("Traffic lights")]
        [SerializeField] float greenTime = 12f;
        [SerializeField] float clearanceTime = 3f;

        public Transform player;

        static readonly Vector3[] Directions = { Vector3.forward, Vector3.right, Vector3.back, Vector3.left };
        static readonly int BaseColorFactor = Shader.PropertyToID("baseColorFactor");
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        sealed class Car
        {
            public Transform transform;
            public Rigidbody body;
            public Collider collider;
            public readonly List<Vector3> path = new();
            public int pathIndex;
            public float speed;
            public Vector2Int from, to;
            public int direction;
            public int stopIndex; // path index of the stop line before the next intersection
            public int turnEnd;   // path indices below this are the curve through an intersection
        }

        readonly List<Car> cars = new();
        readonly RaycastHit[] hits = new RaycastHit[8];
        float clock; // advances with physics, so lights also cycle when the simulation is stepped by hand

        public int ActiveCars => cars.Count;

        void Start()
        {
            if (player == null)
            {
                var car = FindAnyObjectByType<ArcadeCar>();
                if (car != null) player = car.transform;
            }
            if (carTemplate == null || player == null) { enabled = false; return; }
            carTemplate.SetActive(false);
            var block = new MaterialPropertyBlock();
            for (int i = 0; i < carCount; i++)
            {
                var instance = Instantiate(carTemplate, transform);
                instance.name = $"Traffic car {i + 1}";
                instance.SetActive(true);
                Paint(instance, paints[i % paints.Length], block);
                var car = new Car
                {
                    transform = instance.transform,
                    body = instance.GetComponent<Rigidbody>(),
                    collider = instance.GetComponent<Collider>()
                };
                car.body.isKinematic = true;
                car.body.interpolation = RigidbodyInterpolation.Interpolate;
                cars.Add(car);
                Respawn(car, true);
            }
        }

        static void Paint(GameObject car, Color color, MaterialPropertyBlock block)
        {
            foreach (var renderer in car.GetComponentsInChildren<Renderer>())
            {
                var materials = renderer.sharedMaterials;
                for (int m = 0; m < materials.Length; m++)
                {
                    if (materials[m] == null || !materials[m].name.StartsWith("paint")) continue;
                    renderer.GetPropertyBlock(block, m);
                    block.SetColor(BaseColorFactor, color.linear);
                    block.SetColor(BaseColor, color);
                    renderer.SetPropertyBlock(block, m);
                }
            }
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            clock += dt;
            foreach (var car in cars)
            {
                if ((car.transform.position - player.position).sqrMagnitude > despawnRadius * despawnRadius)
                {
                    Respawn(car, false);
                    continue;
                }
                Drive(car, dt);
            }
        }

        void Drive(Car car, float dt)
        {
            Vector3 position = car.body.position;
            Vector3 target = car.path[car.pathIndex];
            Vector3 toTarget = target - position;
            toTarget.y = 0;
            Vector3 forward = toTarget.sqrMagnitude > .01f ? toTarget.normalized : car.transform.forward;

            float desired = cruiseSpeed;
            if (car.pathIndex < car.turnEnd) desired = turnSpeed;
            else
            {
                float toStop = Distance(car, position, car.stopIndex);
                desired = Mathf.Min(desired, Mathf.Sqrt(turnSpeed * turnSpeed + 2f * braking * .6f * toStop));
                if (!IsGreen(car.to, car.direction)) desired = Mathf.Min(desired, StoppingSpeed(toStop));
            }
            desired = Mathf.Min(desired, StoppingSpeed(ClearDistance(car, position, forward) - 6f));

            float rate = desired < car.speed ? braking : acceleration;
            car.speed = Mathf.MoveTowards(car.speed, desired, rate * dt);

            float step = car.speed * dt;
            while (step > 0 && car.pathIndex < car.path.Count)
            {
                Vector3 next = car.path[car.pathIndex];
                next.y = position.y;
                float remaining = Vector3.Distance(position, next);
                if (remaining > step) { position = Vector3.MoveTowards(position, next, step); break; }
                position = next;
                step -= remaining;
                car.pathIndex++;
                if (car.pathIndex >= car.path.Count) PlanNextLeg(car);
            }

            car.body.MovePosition(position);
            if (forward.sqrMagnitude > .01f)
                car.body.MoveRotation(Quaternion.Slerp(car.body.rotation, Quaternion.LookRotation(forward), 8f * dt));
        }

        float StoppingSpeed(float distance) => distance <= 0 ? 0 : Mathf.Sqrt(2f * braking * .6f * distance);

        float Distance(Car car, Vector3 position, int toIndex)
        {
            float d = 0;
            Vector3 p = position;
            for (int i = car.pathIndex; i <= toIndex && i < car.path.Count; i++)
            {
                d += Vector3.Distance(Flat(p), Flat(car.path[i]));
                p = car.path[i];
            }
            return d;
        }

        // Distance to the nearest Rigidbody (player, another car, a pedestrian) in the car's way.
        float ClearDistance(Car car, Vector3 position, Vector3 forward)
        {
            float look = 8f + car.speed * 2f;
            Vector3 origin = position + Vector3.up * .8f;
            int count = Physics.SphereCastNonAlloc(origin, 1.1f, forward, hits, look, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (hit.collider == car.collider || hit.rigidbody == null) continue;
                if (hit.distance > 0 && hit.distance < nearest) nearest = hit.distance;
            }
            return nearest;
        }

        bool IsGreen(Vector2Int node, int direction)
        {
            float cycle = 2f * (greenTime + clearanceTime);
            float offset = (node.x * 7 + node.y * 13) % 5 * 2.5f;
            float t = (clock + offset) % cycle;
            bool northSouth = direction % 2 == 0;
            return northSouth ? t < greenTime : t >= greenTime + clearanceTime && t < cycle - clearanceTime;
        }

        // Walkers may cross a road while that road's traffic has a red light.
        public bool PedestriansMayCross(Vector2Int node, bool acrossNorthSouthRoad) =>
            !IsGreen(node, acrossNorthSouthRoad ? 0 : 1);

        Vector3 NodePosition(Vector2Int n) =>
            new(gridMin + n.x * gridSpacing, roadHeight, gridMin + n.y * gridSpacing);

        bool InGrid(Vector2Int n) => n.x >= 0 && n.y >= 0 && n.x < gridCount && n.y < gridCount;

        static Vector2Int Step(int direction) => direction switch
        {
            0 => new Vector2Int(0, 1), 1 => new Vector2Int(1, 0), 2 => new Vector2Int(0, -1), _ => new Vector2Int(-1, 0)
        };

        static Vector3 Right(int direction) => Vector3.Cross(Vector3.up, Directions[direction]);

        Vector3 LaneStart(Vector2Int node, int direction) =>
            NodePosition(node) + Directions[direction] * stopLine + Right(direction) * laneOffset;

        Vector3 LaneEnd(Vector2Int node, int direction) =>
            NodePosition(node) - Directions[direction] * stopLine + Right(direction) * laneOffset;

        // Called when a car reaches the end of its path: turn through the intersection, then drive the next block.
        void PlanNextLeg(Car car)
        {
            int next = ChooseTurn(car.to, car.direction);
            Vector3 exit = LaneEnd(car.to, car.direction);
            Vector3 entry = LaneStart(car.to, next);
            Vector3 corner = NodePosition(car.to) + Right(car.direction) * laneOffset + Right(next) * laneOffset;
            if (next == car.direction) corner = (exit + entry) * .5f;

            car.path.Clear();
            car.pathIndex = 0;
            for (int i = 1; i <= 8; i++)
            {
                float t = i / 8f, u = 1 - t;
                car.path.Add(u * u * exit + 2 * u * t * corner + t * t * entry);
            }
            car.from = car.to;
            car.direction = next;
            car.to = car.from + Step(next);
            car.turnEnd = car.path.Count;
            car.path.Add(LaneEnd(car.to, next));
            car.stopIndex = car.path.Count - 1;
        }

        int ChooseTurn(Vector2Int node, int direction)
        {
            int straight = direction, right = (direction + 1) % 4, left = (direction + 3) % 4;
            var options = new List<int>(3);
            foreach (int d in new[] { straight, straight, right, left })
                if (InGrid(node + Step(d))) options.Add(d);
            return options.Count > 0 ? options[Random.Range(0, options.Count)] : (direction + 2) % 4;
        }

        void Respawn(Car car, bool initial)
        {
            for (int attempt = 0; attempt < 40; attempt++)
            {
                var from = new Vector2Int(Random.Range(0, gridCount), Random.Range(0, gridCount));
                int direction = Random.Range(0, 4);
                var to = from + Step(direction);
                if (!InGrid(to)) continue;
                Vector3 start = LaneStart(from, direction), end = LaneEnd(to, direction);
                Vector3 spot = Vector3.Lerp(start, end, Random.Range(.1f, .9f));
                float distance = Vector3.Distance(Flat(spot), Flat(player.position));
                if (distance > spawnRadius || distance < minSpawnDistance) continue;
                if (!initial && IsVisible(spot)) continue;
                if (Occupied(spot, car) || Vector3.Distance(Flat(spot), Flat(player.position)) < minSpawnDistance) continue;

                car.from = from;
                car.to = to;
                car.direction = direction;
                car.path.Clear();
                car.path.Add(end);
                car.pathIndex = 0;
                car.stopIndex = 0;
                car.turnEnd = 0;
                car.speed = cruiseSpeed * .8f;
                var rotation = Quaternion.LookRotation(Directions[direction]);
                car.body.position = spot;
                car.body.rotation = rotation;
                car.transform.SetPositionAndRotation(spot, rotation);
                return;
            }
        }

        bool Occupied(Vector3 spot, Car self)
        {
            foreach (var other in cars)
                if (other != self && (other.body.position - spot).sqrMagnitude < 12f * 12f)
                    return true;
            return false;
        }

        static bool IsVisible(Vector3 spot)
        {
            var camera = Camera.main;
            if (camera == null) return false;
            Vector3 v = camera.WorldToViewportPoint(spot);
            return v.z > 0 && v.x > -.1f && v.x < 1.1f && v.y > -.1f && v.y < 1.1f && v.z < 150f;
        }

        static Vector3 Flat(Vector3 v) => new(v.x, 0, v.z);
    }
}
