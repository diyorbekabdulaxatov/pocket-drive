using System.Collections.Generic;
using UnityEngine;

namespace PocketDrive
{
    // AI traffic on any street layout described by a RoadGraph. Same behaviour as the grid TrafficSystem:
    // right-hand lanes, timed lights at junctions of three or more roads, one crossing direction at a time,
    // braking for the player and other cars, and a small pool of cars kept near the player.
    public sealed class GraphTrafficSystem : MonoBehaviour
    {
        [SerializeField] RoadGraph graph;
        [SerializeField] GameObject carTemplate;
        [SerializeField] int carCount = 10;
        [SerializeField] float cruiseSpeed = 11f;
        [SerializeField] float turnSpeed = 5f;
        [SerializeField] float acceleration = 4f;
        [SerializeField] float braking = 9f;
        [SerializeField] float spawnRadius = 170f;
        [SerializeField] float despawnRadius = 230f;
        [SerializeField] float minSpawnDistance = 35f;
        [SerializeField] float greenTime = 12f;
        [SerializeField] float clearanceTime = 4f;
        [SerializeField] Color[] paints =
        {
            new(.02f, .02f, .025f), new(.8f, .8f, .82f), new(.3f, .31f, .33f), new(.02f, .06f, .22f),
            new(.55f, .02f, .02f), new(.9f, .9f, .9f), new(.05f, .12f, .06f), new(.45f, .28f, .08f)
        };

        public Transform player;
        public int ActiveCars => cars.Count;
        public RoadGraph Graph => graph;

        static readonly int BaseColorFactor = Shader.PropertyToID("baseColorFactor");
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        sealed class Car
        {
            public Transform transform;
            public Rigidbody body;
            public Collider collider;
            public readonly List<Vector3> path = new();
            public int pathIndex, stopIndex, turnEnd;
            public float speed;
            public int from, to;          // current edge, driving from -> to
            public int? inside;           // junction being crossed
            public int enteredFrom;
        }

        readonly List<Car> cars = new();
        readonly Dictionary<int, List<Car>> crossing = new();
        readonly RaycastHit[] hits = new RaycastHit[8];
        float clock;

        float StopLine => graph.laneOffset * 2f + 2.5f;

        void Start()
        {
            if (player == null)
            {
                var car = FindAnyObjectByType<ArcadeCar>();
                if (car != null) player = car.transform;
            }
            if (graph == null || graph.edges.Length == 0 || carTemplate == null || player == null) { enabled = false; return; }
            carTemplate.SetActive(false);
            var block = new MaterialPropertyBlock();
            for (int i = 0; i < carCount; i++)
            {
                var instance = Instantiate(carTemplate, transform);
                instance.name = $"Traffic car {i + 1}";
                instance.SetActive(true);
                Paint(instance, paints[i % paints.Length], block);
                var car = new Car { transform = instance.transform, body = instance.GetComponent<Rigidbody>(), collider = instance.GetComponent<Collider>() };
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
                if (car.path.Count == 0 || (car.transform.position - player.position).sqrMagnitude > despawnRadius * despawnRadius)
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
            Vector3 toTarget = Flat(car.path[car.pathIndex] - position);
            Vector3 forward = toTarget.sqrMagnitude > .01f ? toTarget.normalized : car.transform.forward;

            float desired = cruiseSpeed;
            if (car.pathIndex < car.turnEnd) desired = turnSpeed;
            else
            {
                float toStop = Distance(car, position, car.stopIndex);
                desired = Mathf.Min(desired, Mathf.Sqrt(turnSpeed * turnSpeed + 2f * braking * .6f * toStop));
                if (!IsGreen(car.to, car.from) || Blocked(car)) desired = Mathf.Min(desired, StoppingSpeed(toStop));
            }
            desired = Mathf.Min(desired, StoppingSpeed(ClearDistance(car, position, forward) - 5f));
            if (car.inside.HasValue && car.pathIndex >= car.turnEnd) Leave(car);
            car.speed = Mathf.MoveTowards(car.speed, desired, (desired < car.speed ? braking : acceleration) * dt);

            float step = car.speed * dt;
            while (step > 0 && car.pathIndex < car.path.Count)
            {
                Vector3 next = car.path[car.pathIndex];
                next.y = position.y;
                float remaining = Vector3.Distance(position, next);
                if (remaining > step) { position = Vector3.MoveTowards(position, next, step); break; }
                position = next;
                step -= remaining;
                if (car.pathIndex == car.path.Count - 1 && Blocked(car)) { car.speed = 0; break; }
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

        float ClearDistance(Car car, Vector3 position, Vector3 forward)
        {
            float look = 8f + car.speed * 2f, nearest = float.MaxValue;
            void Consider(Vector3 other)
            {
                Vector3 d = Flat(other - position);
                float ahead = Vector3.Dot(d, forward);
                if (ahead <= 0 || ahead > look + 4f) return;
                if (Mathf.Abs(Vector3.Cross(forward, d).y) < 2.2f) nearest = Mathf.Min(nearest, ahead - 2.4f);
            }
            foreach (var other in cars) if (other != car && other.path.Count > 0) Consider(other.body.position);
            Consider(player.position);
            int count = Physics.SphereCastNonAlloc(position + Vector3.up * .8f, .9f, forward, hits, look, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (hits[i].collider != car.collider && hits[i].rigidbody != null && hits[i].distance > 0)
                    nearest = Mathf.Min(nearest, hits[i].distance);
            return nearest;
        }

        // Two phases per junction: roads running along x, then roads running along z. Minor junctions have no lights.
        bool IsGreen(int node, int comingFrom)
        {
            if (graph.Degree(node) < 3) return true;
            float cycle = 2f * (greenTime + clearanceTime);
            float t = (clock + node * 3.7f) % cycle;
            Vector3 d = graph.nodes[node] - graph.nodes[comingFrom];
            bool alongX = Mathf.Abs(d.x) > Mathf.Abs(d.z);
            return alongX ? t < greenTime : t >= greenTime + clearanceTime && t < cycle - clearanceTime;
        }

        bool Blocked(Car car)
        {
            if (!crossing.TryGetValue(car.to, out var inside)) return false;
            foreach (var other in inside) if (other != car && other.enteredFrom != car.from) return true;
            return false;
        }

        void Enter(Car car)
        {
            if (!crossing.TryGetValue(car.to, out var inside)) crossing[car.to] = inside = new List<Car>();
            inside.Add(car);
            car.inside = car.to;
            car.enteredFrom = car.from;
        }

        void Leave(Car car)
        {
            if (car.inside.HasValue && crossing.TryGetValue(car.inside.Value, out var inside)) inside.Remove(car);
            car.inside = null;
        }

        Vector3 Dir(int a, int b) => Flat(graph.nodes[b] - graph.nodes[a]).normalized;
        static Vector3 Right(Vector3 dir) => Vector3.Cross(Vector3.up, dir);

        Vector3 LaneStart(int a, int b)
        {
            Vector3 d = Dir(a, b);
            float stop = Mathf.Min(StopLine, EdgeLength(a, b) * .3f);
            return graph.nodes[a] + d * stop + Right(d) * graph.laneOffset;
        }

        Vector3 LaneEnd(int a, int b)
        {
            Vector3 d = Dir(a, b);
            float stop = Mathf.Min(StopLine, EdgeLength(a, b) * .3f);
            return graph.nodes[b] - d * stop + Right(d) * graph.laneOffset;
        }

        float EdgeLength(int a, int b) => Vector3.Distance(graph.nodes[a], graph.nodes[b]);

        void PlanNextLeg(Car car)
        {
            Enter(car);
            int node = car.to, next = ChooseNext(node, car.from);
            Vector3 exit = LaneEnd(car.from, node), entry = LaneStart(node, next);
            Vector3 d1 = Dir(car.from, node), d2 = Dir(node, next);
            Vector3 corner = Mathf.Abs(Vector3.Dot(d1, d2)) > .7f
                ? (next == car.from ? graph.nodes[node] : (exit + entry) * .5f)
                : graph.nodes[node] + (Right(d1) + Right(d2)) * graph.laneOffset;

            car.path.Clear();
            car.pathIndex = 0;
            for (int i = 1; i <= 8; i++)
            {
                float t = i / 8f, u = 1 - t;
                car.path.Add(u * u * exit + 2 * u * t * corner + t * t * entry);
            }
            car.turnEnd = car.path.Count;
            car.from = node;
            car.to = next;
            car.path.Add(LaneEnd(node, next));
            car.stopIndex = car.path.Count - 1;
        }

        // Prefer going straight on; turn right or left otherwise; U-turn only at dead ends.
        int ChooseNext(int node, int from)
        {
            var options = new List<int>();
            Vector3 incoming = Dir(from, node);
            foreach (int n in graph.Neighbours(node))
            {
                if (n == from) continue;
                options.Add(n);
                if (Vector3.Dot(incoming, Dir(node, n)) > .7f) options.Add(n); // straight on counts twice
            }
            return options.Count > 0 ? options[Random.Range(0, options.Count)] : from;
        }

        void Respawn(Car car, bool initial)
        {
            for (int attempt = 0; attempt < 80; attempt++)
            {
                var e = graph.edges[Random.Range(0, graph.edges.Length)];
                bool forward = Random.value < .5f;
                int a = forward ? e.x : e.y, b = forward ? e.y : e.x;
                if (EdgeLength(a, b) < 20f) continue;
                Vector3 spot = Vector3.Lerp(LaneStart(a, b), LaneEnd(a, b), Random.Range(.15f, .85f));
                float distance = Vector3.Distance(Flat(spot), Flat(player.position));
                if (distance > spawnRadius || distance < minSpawnDistance) continue;
                if (!initial && IsVisible(spot)) continue;
                if (Occupied(spot, car)) continue;
                Leave(car);
                car.from = a;
                car.to = b;
                car.path.Clear();
                car.path.Add(LaneEnd(a, b));
                car.pathIndex = car.stopIndex = car.turnEnd = 0;
                car.speed = cruiseSpeed * .7f;
                var rotation = Quaternion.LookRotation(Dir(a, b));
                car.body.position = spot;
                car.body.rotation = rotation;
                car.transform.SetPositionAndRotation(spot, rotation);
                return;
            }
        }

        bool Occupied(Vector3 spot, Car self)
        {
            foreach (var other in cars)
                if (other != self && other.path.Count > 0 && (other.body.position - spot).sqrMagnitude < 12f * 12f) return true;
            return false;
        }

        static bool IsVisible(Vector3 spot)
        {
            var camera = Camera.main;
            if (camera == null) return false;
            Vector3 v = camera.WorldToViewportPoint(spot);
            return v.z > 0 && v.z < 150f && v.x > -.1f && v.x < 1.1f && v.y > -.1f && v.y < 1.1f;
        }

        static Vector3 Flat(Vector3 v) => new(v.x, 0, v.z);
    }
}
