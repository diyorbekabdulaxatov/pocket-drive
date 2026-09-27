using System.Collections.Generic;
using UnityEngine;

namespace PocketDrive
{
    // Pedestrians walking the pavement ring around each city block. At a block corner they either
    // carry on round the block or cross to the next block on the crosswalk when the traffic light allows.
    // Each walker has a kinematic Rigidbody so traffic cars brake for them.
    public sealed class PedestrianSystem : MonoBehaviour
    {
        [SerializeField] GameObject[] walkerTemplates;
        [SerializeField] TrafficSystem traffic;
        [SerializeField] int walkerCount = 24;
        [SerializeField] float walkSpeed = 1.35f;
        [SerializeField] float blockMin = -225f;
        [SerializeField] float blockSpacing = 150f;
        [SerializeField] int blockCount = 4;
        [SerializeField] float pavementOffset = 58f;
        [SerializeField] float pavementHeight = .22f;
        [SerializeField] float roadHeight = .05f;
        [SerializeField] float spawnRadius = 160f;
        [SerializeField] float despawnRadius = 220f;
        [SerializeField] float gridMin = -300f;

        public Transform player;

        sealed class Walker
        {
            public Transform transform;
            public Rigidbody body;
            public Animator animator;
            public Vector2Int block;
            public int corner;          // 0..3, counter-clockwise from south-west
            public bool clockwise;
            public readonly Queue<Vector3> route = new();
            public Vector2Int waitNode;
            public bool waitAcrossNorthSouth;
            public bool waiting;
            public float speedJitter;
        }

        readonly List<Walker> walkers = new();

        public int ActiveWalkers => walkers.Count;

        void Start()
        {
            if (player == null)
            {
                var car = FindAnyObjectByType<ArcadeCar>();
                if (car != null) player = car.transform;
            }
            if (walkerTemplates == null || walkerTemplates.Length == 0 || player == null) { enabled = false; return; }
            foreach (var template in walkerTemplates) template.SetActive(false);
            for (int i = 0; i < walkerCount; i++)
            {
                var instance = Instantiate(walkerTemplates[i % walkerTemplates.Length], transform);
                instance.name = $"Pedestrian {i + 1}";
                instance.SetActive(true);
                var body = instance.GetComponent<Rigidbody>();
                if (body == null) body = instance.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                var walker = new Walker
                {
                    transform = instance.transform,
                    body = body,
                    animator = instance.GetComponentInChildren<Animator>(),
                    speedJitter = Random.Range(.85f, 1.15f)
                };
                walkers.Add(walker);
                Respawn(walker);
            }
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            foreach (var w in walkers)
            {
                if ((w.body.position - player.position).sqrMagnitude > despawnRadius * despawnRadius) { Respawn(w); continue; }
                Walk(w, dt);
            }
        }

        void Walk(Walker w, float dt)
        {
            if (w.route.Count == 0) PlanFromCorner(w);
            if (w.waiting)
            {
                if (traffic != null && !traffic.PedestriansMayCross(w.waitNode, w.waitAcrossNorthSouth)) { SetMoving(w, false); return; }
                w.waiting = false;
            }
            Vector3 position = w.body.position;
            Vector3 target = w.route.Peek();
            Vector3 flat = new(target.x - position.x, 0, target.z - position.z);
            float step = walkSpeed * w.speedJitter * dt;
            if (flat.magnitude <= step)
            {
                position = target;
                w.route.Dequeue();
            }
            else position += flat.normalized * step;
            position.y = Mathf.MoveTowards(position.y, target.y, dt);
            w.body.MovePosition(position);
            if (flat.sqrMagnitude > .0001f)
                w.body.MoveRotation(Quaternion.Slerp(w.body.rotation, Quaternion.LookRotation(flat), 10f * dt));
            SetMoving(w, true);
        }

        static void SetMoving(Walker w, bool moving)
        {
            if (w.animator != null) w.animator.speed = moving ? 1f : 0f;
        }

        Vector3 BlockCentre(Vector2Int b) => new(blockMin + b.x * blockSpacing, pavementHeight, blockMin + b.y * blockSpacing);

        static readonly Vector2[] CornerSigns = { new(-1, -1), new(1, -1), new(1, 1), new(-1, 1) };

        Vector3 CornerPoint(Vector2Int block, int corner)
        {
            Vector2 s = CornerSigns[corner];
            return BlockCentre(block) + new Vector3(s.x * pavementOffset, 0, s.y * pavementOffset);
        }

        bool InBlocks(Vector2Int b) => b.x >= 0 && b.y >= 0 && b.x < blockCount && b.y < blockCount;

        // At a corner: usually walk to the next corner of this block; sometimes cross the road to a neighbour.
        void PlanFromCorner(Walker w)
        {
            Vector2 s = CornerSigns[w.corner];
            if (Random.value < .35f)
            {
                bool crossEastWest = Random.value < .5f; // walk along x, across a north-south road
                var neighbour = w.block + (crossEastWest ? new Vector2Int((int)s.x, 0) : new Vector2Int(0, (int)s.y));
                if (InBlocks(neighbour))
                {
                    int newCorner = System.Array.FindIndex(CornerSigns,
                        c => crossEastWest ? c.x == -s.x && c.y == s.y : c.x == s.x && c.y == -s.y);
                    Vector3 from = CornerPoint(w.block, w.corner), to = CornerPoint(neighbour, newCorner);
                    Vector3 kerbA = Vector3.Lerp(from, to, .06f), kerbB = Vector3.Lerp(from, to, .94f);
                    kerbA.y = kerbB.y = roadHeight;
                    // The intersection diagonally beyond this corner controls the crosswalk.
                    w.waitNode = new Vector2Int(Mathf.RoundToInt((from.x + s.x * 17f - gridMin) / blockSpacing),
                                                Mathf.RoundToInt((from.z + s.y * 17f - gridMin) / blockSpacing));
                    w.waitAcrossNorthSouth = crossEastWest;
                    w.waiting = true;
                    w.route.Enqueue(kerbA);
                    w.route.Enqueue(kerbB);
                    w.route.Enqueue(to);
                    w.block = neighbour;
                    w.corner = newCorner;
                    return;
                }
            }
            w.corner = (w.corner + (w.clockwise ? 3 : 1)) % 4;
            w.route.Enqueue(CornerPoint(w.block, w.corner));
        }

        void Respawn(Walker w)
        {
            for (int attempt = 0; attempt < 30; attempt++)
            {
                var block = new Vector2Int(Random.Range(0, blockCount), Random.Range(0, blockCount));
                int corner = Random.Range(0, 4);
                Vector3 a = CornerPoint(block, corner), b = CornerPoint(block, (corner + 1) % 4);
                Vector3 spot = Vector3.Lerp(a, b, Random.value);
                if ((spot - player.position).sqrMagnitude > spawnRadius * spawnRadius) continue;
                w.block = block;
                w.clockwise = Random.value < .5f;
                w.corner = w.clockwise ? corner : (corner + 1) % 4;
                w.route.Clear();
                w.route.Enqueue(CornerPoint(block, w.corner));
                w.waiting = false;
                Vector3 look = w.route.Peek() - spot;
                look.y = 0;
                var rotation = look.sqrMagnitude > .01f ? Quaternion.LookRotation(look) : Quaternion.identity;
                w.body.position = spot;
                w.body.rotation = rotation;
                w.transform.SetPositionAndRotation(spot, rotation);
                return;
            }
        }
    }
}
