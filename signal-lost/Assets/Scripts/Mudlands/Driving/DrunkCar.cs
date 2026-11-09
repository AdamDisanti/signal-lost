using System;
using System.Collections.Generic;
using SignalLost.Audio;
using SignalLost.Combat;
using SignalLost.Effects;
using SignalLost.Player;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace SignalLost.Mudlands
{
    /// <summary>
    /// The Drunk Driving car: a beaten-up muscle car with the mechanic at the wheel. An arcade raycast car (four
    /// suspension rays, speed-sensitive steering, a handbrake that kicks the rear out, low grip in mud), driven by
    /// <see cref="DrunkDriving"/>'s input. It runs over small creatures, smashes destructibles, scrapes walls at an
    /// angle and blows up when it hits something big or solid head-on, when its bottle runs dry, when it takes too
    /// much damage, or when the driver bails. Every ride ends in an explosion (<see cref="CarBlast"/>); the driver is
    /// thrown clear unhurt. Built entirely in code.
    /// </summary>
    public sealed class DrunkCar : MonoBehaviour
    {
        public enum EndReason { Crash, BottleEmpty, Wrecked, Bailed, Flipped, Stalled, Parked }

        // Tuning (first pass; see docs/mudlands.md).
        private const float Mass = 900f, WheelRadius = 0.38f, RestLength = 0.42f, Spring = 26000f, Damper = 2600f;
        private const float Accel = 10f, ReverseSpeed = 8f, Brake = 16f, RollDrag = 0.5f, AirDrag = 0.0045f;
        private const float MaxYawRate = 115f, SmashSpeed = 4f, RunOverSpeed = 6f, CrashSpeed = 9f, HeadOnDot = 0.55f;
        private static readonly Vector3[] WheelSpots =
        {
            new Vector3(-0.82f, 0.38f, 1.32f), new Vector3(0.82f, 0.38f, 1.32f),
            new Vector3(-0.82f, 0.38f, -1.28f), new Vector3(0.82f, 0.38f, -1.28f)
        };
        private static readonly Vector3 BodySize = new Vector3(1.85f, 0.85f, 4.2f);

        private Rigidbody body;
        private BoxCollider hull, cabin;
        private Transform visual, bodyPivot, seat;
        private readonly Transform[] wheels = new Transform[4];
        private readonly Transform[] steerPivots = new Transform[2];
        private readonly float[] compression = new float[4];
        private readonly bool[] contact = new bool[4];
        private readonly RaycastHit[] rayHits = new RaycastHit[8];
        private readonly Collider[] sweepHits = new Collider[24];
        private readonly Dictionary<Collider, float> ignoredUntil = new Dictionary<Collider, float>();
        private readonly HashSet<Health> handled = new HashSet<Health>();
        private DrunkDriving driver;
        private SfxLoop engine;
        private float throttle, steer, wheelSpin, bodyRoll, bodyPitch, flippedFor, stuckFor, waterFor, nextScrape, nextSkid;
        private bool handbrake, ended, landed;
        private Vector3 lastVelocity;
        private float hp, bottleLeft;

        public DrunkDriving Driver => driver;
        public Transform Seat => seat;
        public Rigidbody Body => body;
        public float Speed => body != null ? body.linearVelocity.magnitude : 0f;
        public float ForwardSpeed => body != null ? Vector3.Dot(body.linearVelocity, transform.forward) : 0f;
        public Vector3 Velocity => body != null ? body.linearVelocity : Vector3.zero;
        public float Health01 => Mathf.Clamp01(hp / Mathf.Max(1f, driver != null ? driver.CarHealth : 80f));
        public float Bottle01 => driver != null && driver.RideSeconds > 0f ? Mathf.Clamp01(bottleLeft / driver.RideSeconds) : 1f;
        public bool Grounded { get; private set; }
        public bool Ended => ended;
        /// <summary>Extra top speed (the boss chase).</summary>
        public float TopSpeed { get; set; } = 22f;
        /// <summary>When true the bottle never runs dry (the boss chase hands out free cars).</summary>
        public bool EndlessBottle { get; set; }

        public static event Action<DrunkCar> AnyCarSpawned;
        /// <summary>Raised when any ride ends, with why (before it blows up).</summary>
        public static event Action<DrunkCar, EndReason> AnyCarEnded;
        /// <summary>A car hit something. Argument: what it hit (a creature's Health may be null for scenery).</summary>
        public event Action<DrunkCar, Health> Rammed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            AnyCarSpawned = null;
            AnyCarEnded = null;
        }

        // ---------- Creation ----------

        public static DrunkCar Create(DrunkDriving owner, Vector3 position, Quaternion facing)
        {
            var root = new GameObject("Drunk Driving Car");
            root.transform.SetPositionAndRotation(position, facing);
            var car = root.AddComponent<DrunkCar>();
            car.driver = owner;
            car.Build();
            AnyCarSpawned?.Invoke(car);
            return car;
        }

        private void Build()
        {
            body = gameObject.AddComponent<Rigidbody>();
            body.mass = Mass;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearDamping = 0f;
            body.angularDamping = 1.2f;
            body.centerOfMass = new Vector3(0f, 0.45f, 0.05f);

            hull = gameObject.AddComponent<BoxCollider>();
            hull.center = new Vector3(0f, 0.85f, 0f);
            hull.size = BodySize;
            cabin = gameObject.AddComponent<BoxCollider>();
            cabin.center = new Vector3(0f, 1.45f, -0.35f);
            cabin.size = new Vector3(1.6f, 0.55f, 1.9f);
            var slick = new PhysicsMaterial("Car") { dynamicFriction = 0.1f, staticFriction = 0.1f, bounciness = 0.1f,
                frictionCombine = PhysicsMaterialCombine.Minimum };
            hull.sharedMaterial = slick;
            cabin.sharedMaterial = slick;

            visual = MudArt.Pivot(transform, "Visual", Vector3.zero);
            bodyPivot = MudArt.Pivot(visual, "Body", new Vector3(0f, 0.55f, 0f));
            CarModel.BuildBody(bodyPivot);
            seat = MudArt.Pivot(bodyPivot, "Seat", new Vector3(-0.38f, 0.32f, -0.25f));
            for (int i = 0; i < 4; i++)
            {
                Transform holder = visual;
                if (i < 2)
                {
                    steerPivots[i] = MudArt.Pivot(visual, i == 0 ? "Steer L" : "Steer R", WheelSpots[i]);
                    holder = steerPivots[i];
                }
                Vector3 at = i < 2 ? Vector3.zero : WheelSpots[i];
                wheels[i] = CarModel.Wheel(holder, "Wheel " + i, at, WheelSpots[i].x < 0f);
            }

            hp = driver != null ? driver.CarHealth : 80f;
            bottleLeft = driver != null ? driver.RideSeconds : 14f;
            engine = Sfx.AddLoop(gameObject, "car_engine", 0.5f, true, 3f, 45f);
        }


        // ---------- Driving ----------

        /// <summary>Set by the driver each frame.</summary>
        public void SetInput(float throttleInput, float steerInput, bool handbrakeInput)
        {
            throttle = Mathf.Clamp(throttleInput, -1f, 1f);
            steer = Mathf.Clamp(steerInput, -1f, 1f);
            handbrake = handbrakeInput;
        }

        public void Honk()
        {
            if (ended) return;
            Sfx.PlayAt("car_horn", transform.position, 0.9f, Random.Range(0.97f, 1.03f), 50f);
            // Everything nearby hears it.
            foreach (Collider collider in Physics.OverlapSphere(transform.position, 18f, ~0, QueryTriggerInteraction.Ignore))
                collider.GetComponentInParent<IVehicleReactive>()?.OnCarApproaching(this, 0.6f);
        }

        private void FixedUpdate()
        {
            if (ended) return;
            float dt = Time.fixedDeltaTime;
            Vector3 up = transform.up;
            int grounded = 0;
            Vector3 groundNormal = Vector3.zero;
            float mud = 0f, water = 0f;

            for (int i = 0; i < 4; i++)
            {
                Vector3 top = transform.TransformPoint(WheelSpots[i] + Vector3.up * RestLength);
                contact[i] = CastDown(top, -up, RestLength + WheelRadius, out RaycastHit hit);
                if (!contact[i])
                {
                    compression[i] = Mathf.MoveTowards(compression[i], 0f, dt * 4f);
                    continue;
                }
                grounded++;
                groundNormal += hit.normal;
                float c = Mathf.Clamp01(1f - (hit.distance - WheelRadius) / RestLength);
                compression[i] = c;
                Vector3 pointVelocity = body.GetPointVelocity(top);
                float force = c * Spring - Vector3.Dot(pointVelocity, up) * Damper;
                body.AddForceAtPosition(up * Mathf.Max(0f, force), top);
                (MudZone.Kind kind, float strength, _) = MudZone.At(hit.point);
                if (strength > 0f)
                {
                    if (kind == MudZone.Kind.Mud) mud = Mathf.Max(mud, strength);
                    else water = Mathf.Max(water, strength);
                }
            }

            Grounded = grounded >= 2;
            Vector3 velocity = body.linearVelocity;
            if (Grounded)
            {
                Vector3 normal = groundNormal.normalized;
                Vector3 forward = Vector3.ProjectOnPlane(transform.forward, normal).normalized;
                Vector3 right = Vector3.Cross(normal, forward);
                float forwardSpeed = Vector3.Dot(velocity, forward);
                float lateralSpeed = Vector3.Dot(velocity, right);
                bool mudTires = driver != null && driver.MudTires;
                float gripScale = mudTires ? 1.3f : 1f;
                float mudEffect = mudTires ? mud * 0.35f : mud;

                // Throttle, brake and reverse.
                float top = TopSpeed * (1f - 0.35f * mudEffect) * (1f - 0.6f * water);
                float drive = 0f;
                if (throttle > 0.05f)
                    drive = forwardSpeed < -0.5f ? Brake : Accel * throttle * Mathf.Sqrt(Mathf.Clamp01(1f - forwardSpeed / Mathf.Max(1f, top)));
                else if (throttle < -0.05f)
                    drive = forwardSpeed > 0.5f ? -Brake : -Accel * 0.7f * -throttle * Mathf.Clamp01(1f + forwardSpeed / ReverseSpeed);
                if (handbrake) drive -= Mathf.Sign(forwardSpeed) * Mathf.Min(Mathf.Abs(forwardSpeed) / dt, 5f);
                body.AddForce(forward * drive, ForceMode.Acceleration);

                // Rolling resistance, extra drag in mud and water.
                float drag = RollDrag + mudEffect * 5f + water * 12f;
                if (Mathf.Abs(throttle) < 0.05f) drag += 1.5f;
                float slow = Mathf.Min(Mathf.Abs(forwardSpeed), drag * dt) * Mathf.Sign(forwardSpeed);
                body.AddForce(-forward * slow, ForceMode.VelocityChange);

                // Sideways grip: high on dirt, lower on grass, low in mud, very low with the handbrake on.
                float grip = 9f * gripScale * (1f - 0.7f * mudEffect);
                if (handbrake) grip = 1.4f * gripScale;
                body.AddForce(-right * lateralSpeed * Mathf.Clamp01(grip * dt), ForceMode.VelocityChange);

                // Steering: a turn rate that grows with speed, a little wilder with drink and the handbrake.
                float drunk = driver != null ? driver.Drunkenness : 0f;
                float input = steer + Mathf.Sin(Time.time * 1.7f) * 0.16f * drunk + Mathf.Sin(Time.time * 4.3f) * 0.05f * drunk;
                float speedFactor = Mathf.Clamp01(Mathf.Abs(forwardSpeed) / 5f) / (1f + Mathf.Abs(forwardSpeed) / 40f);
                float yaw = input * MaxYawRate * speedFactor * Mathf.Sign(forwardSpeed == 0f ? 1f : forwardSpeed) * (handbrake ? 1.45f : 1f);
                Vector3 local = transform.InverseTransformDirection(body.angularVelocity);
                local.y = Mathf.MoveTowards(local.y, yaw * Mathf.Deg2Rad, (handbrake ? 9f : 6f) * dt * (1f - 0.5f * mudEffect));
                local.x *= 1f - 4f * dt; // keep it planted: damp roll and pitch
                local.z *= 1f - 4f * dt;
                body.angularVelocity = transform.TransformDirection(local);

                // Tyre effects.
                if ((handbrake || Mathf.Abs(lateralSpeed) > 4f) && Mathf.Abs(forwardSpeed) > 6f && Time.time >= nextSkid)
                {
                    nextSkid = Time.time + 0.5f;
                    Sfx.PlayAt("car_skid", transform.position, 0.45f, Random.Range(0.9f, 1.1f), 35f);
                }
                if (Speed > 3f && Random.value < (mud > 0f || water > 0f ? 0.6f : 0.12f))
                {
                    Vector3 rear = transform.TransformPoint(new Vector3(Random.value < 0.5f ? -0.8f : 0.8f, 0.1f, -1.4f));
                    if (water > 0.2f) ParticleBurst.Spawn(ParticleBurst.Kind.Water, rear, -forward + Vector3.up, 4, 0.25f, 4f, 30f);
                    else ParticleBurst.Spawn(ParticleBurst.Kind.Sand, rear, -forward + Vector3.up * 0.7f, mud > 0f ? 4 : 1, 0.35f, 3f, 35f,
                        mud > 0f ? new Color(0.25f, 0.18f, 0.12f, 0.9f) : new Color(0.55f, 0.43f, 0.3f, 0.5f));
                }
                if (!landed)
                {
                    landed = true;
                    Sfx.PlayAt("car_drop", transform.position, 0.9f, 1f, 40f);
                    ParticleBurst.Spawn(ParticleBurst.Kind.Sand, transform.position, Vector3.up, 30, 0.8f, 4f, 80f, new Color(0.45f, 0.36f, 0.26f, 0.7f));
                    CameraShake.Add(0.25f);
                }
            }
            else
            {
                // In the air: slowly level out so jumps land on the wheels.
                Vector3 axis = Vector3.Cross(transform.up, Vector3.up);
                body.AddTorque(axis * 6f, ForceMode.Acceleration);
            }
            body.AddForce(-velocity * velocity.magnitude * AirDrag, ForceMode.Acceleration);

            SweepAhead(dt);
            lastVelocity = body.linearVelocity;
            UpdateFailsafes(dt, water);
        }

        private bool CastDown(Vector3 from, Vector3 direction, float length, out RaycastHit best)
        {
            best = default;
            int count = Physics.RaycastNonAlloc(from, direction, rayHits, length, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                Collider collider = rayHits[i].collider;
                if (collider.transform.IsChildOf(transform) || collider.GetComponent<Debris>() != null) continue;
                if (collider.GetComponentInParent<Health>() != null && collider.GetComponentInParent<Destructible>() == null) continue; // creatures are not road
                if (rayHits[i].distance >= nearest) continue;
                nearest = rayHits[i].distance;
                best = rayHits[i];
                found = true;
            }
            return found;
        }

        // ---------- Hitting things ----------

        // A box just ahead of the bumper: creatures and destructibles are dealt with here, before the physics
        // engine can stop the car on them.
        private void SweepAhead(float dt)
        {
            float speed = Speed;
            if (speed < 1f) return;
            Vector3 travel = body.linearVelocity.normalized;
            float reach = 0.3f + speed * dt * 2.5f;
            float along = Vector3.Dot(travel, transform.forward) >= 0f ? 1f : -1f;
            Vector3 centre = transform.TransformPoint(new Vector3(0f, 0.95f, along * (BodySize.z * 0.5f + reach * 0.5f)));
            var half = new Vector3(BodySize.x * 0.5f + 0.15f, 0.75f, reach * 0.5f + 0.2f);
            int count = Physics.OverlapBoxNonAlloc(centre, half, sweepHits, transform.rotation, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count && !ended; i++) Touch(sweepHits[i], speed, travel);
        }

        private void Touch(Collider collider, float speed, Vector3 travel)
        {
            if (collider == null || collider.transform.IsChildOf(transform) || Ignoring(collider)) return;
            if (driver != null && collider.transform.IsChildOf(driver.transform)) return;

            var prop = collider.GetComponentInParent<Destructible>();
            if (prop != null && !prop.IsBroken)
            {
                if (speed < prop.CarSmashSpeed) return; // a solid bump; physics handles it
                prop.SmashByCar(this, body.linearVelocity);
                body.linearVelocity *= 1f - prop.CarSpeedLoss;
                CameraShake.Add(0.18f);
                Ignore(prop.gameObject, 2f);
                Rammed?.Invoke(this, null);
                return;
            }

            Health target = collider.GetComponentInParent<Health>();
            if (target == null || target.IsDead || handled.Contains(target)) return;
            if (!CarBlast.IsEnemy(target.gameObject) && target.GetComponentInParent<IVehicleReactive>() == null) return;

            Vector3 flat = travel;
            flat.y = 0f;
            Vector3 toCar = transform.position - target.transform.position;
            toCar.y = 0f;
            var hit = new VehicleHit
            {
                Car = this,
                Speed = speed,
                Point = collider.ClosestPoint(transform.position),
                Direction = flat.sqrMagnitude > 0.001f ? flat.normalized : transform.forward,
                Damage = (30f + speed * 2f) * (driver != null ? driver.BumperMultiplier : 1f),
                HeadOn = Vector3.Dot(target.transform.forward, toCar.normalized) > 0.4f,
                Reinforced = driver != null && driver.Reinforced
            };

            var reactive = target.GetComponentInParent<IVehicleReactive>();
            VehicleResponse response = reactive != null ? reactive.OnVehicleHit(hit) : DefaultResponse(target, hit);
            if (response == VehicleResponse.RunOver && speed < RunOverSpeed) response = VehicleResponse.Bounce;

            switch (response)
            {
                case VehicleResponse.Explode:
                    Rammed?.Invoke(this, target);
                    End(EndReason.Crash, target);
                    return;
                case VehicleResponse.RunOver:
                    if (reactive == null) DefaultRunOver(target, hit);
                    body.linearVelocity *= 0.86f;
                    CameraShake.Add(0.22f);
                    Sfx.PlayAt("car_crash", hit.Point, 0.5f, Random.Range(1.2f, 1.4f), 40f);
                    break;
                case VehicleResponse.Bounce:
                    body.linearVelocity *= 0.45f;
                    CameraShake.Add(0.12f);
                    var agent = target.GetComponent<NavMeshAgent>();
                    if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh) agent.Move(hit.Direction * 1.6f);
                    break;
            }
            Rammed?.Invoke(this, target);
            handled.Add(target);
            Ignore(target.gameObject, response == VehicleResponse.Bounce ? 0.6f : 1.5f);
        }

        private static VehicleResponse DefaultResponse(Health target, VehicleHit hit) =>
            CarBlast.IsLarge(target) ? VehicleResponse.Explode : VehicleResponse.RunOver;

        // Creatures that don't know about cars (desert fodder, crabs, frogs): hurt, shoved and stunned.
        private void DefaultRunOver(Health target, VehicleHit hit)
        {
            target.TakeDamage(hit.Damage, driver != null ? driver.gameObject : null);
            if (target.IsDead) return;
            target.GetComponentInParent<IStunnable>()?.Stun(1f);
            var agent = target.GetComponent<NavMeshAgent>();
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh) agent.Move(hit.Direction * 3f);
        }

        private bool Ignoring(Collider collider) => ignoredUntil.TryGetValue(collider, out float until) && Time.time < until;

        private void Ignore(GameObject other, float seconds)
        {
            foreach (Collider collider in other.GetComponentsInChildren<Collider>())
            {
                ignoredUntil[collider] = Time.time + seconds;
                Physics.IgnoreCollision(hull, collider, true);
                Physics.IgnoreCollision(cabin, collider, true);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (ended) return;
            Collider other = collision.collider;
            Health creature = other.GetComponentInParent<Health>();
            bool handledElsewhere = other.GetComponentInParent<Destructible>() != null ||
                                    (creature != null && (CarBlast.IsEnemy(creature.gameObject) || creature.GetComponentInParent<IVehicleReactive>() != null));
            if (handledElsewhere)
            {
                Touch(other, lastVelocity.magnitude, lastVelocity.normalized);
                return;
            }
            Vector3 normal = Vector3.zero;
            for (int i = 0; i < collision.contactCount; i++) normal += collision.GetContact(i).normal;
            normal.Normalize();
            if (normal.y > 0.6f) return; // the ground
            Vector3 travel = lastVelocity;
            travel.y = 0f;
            float speed = travel.magnitude;
            if (speed < 0.5f) return;
            Vector3 flatNormal = new Vector3(normal.x, 0f, normal.z).normalized;
            float headOn = Vector3.Dot(-flatNormal, travel / speed);
            Vector3 point = collision.GetContact(0).point;
            if (headOn > HeadOnDot && speed * headOn > CrashSpeed)
            {
                Rammed?.Invoke(this, null);
                End(EndReason.Crash, null);
                return;
            }
            // A scrape: sparks, a bang, keep going.
            if (Time.time >= nextScrape)
            {
                nextScrape = Time.time + 0.25f;
                ParticleBurst.Spawn(ParticleBurst.Kind.Sparks, point, normal + Vector3.up * 0.3f, 18, 0.08f, 6f, 50f);
                Sfx.PlayAt("car_crash", point, Mathf.Clamp01(speed / 20f) * 0.6f, Random.Range(1.1f, 1.3f), 40f);
                CameraShake.Add(Mathf.Clamp01(speed / 25f) * 0.25f);
                hp -= speed * headOn * 0.6f;
            }
        }

        // ---------- Damage, failsafes, the end ----------

        /// <summary>The driver was hit (their Health refuses damage while driving): the car takes it instead.</summary>
        public void TakeHit(float amount)
        {
            if (ended) return;
            hp -= amount;
            CameraShake.Add(Mathf.Clamp01(amount / 40f) * 0.3f);
            if (hp <= 0f) End(EndReason.Wrecked, null);
        }

        private void UpdateFailsafes(float dt, float water)
        {
            if (!EndlessBottle) bottleLeft -= dt;
            if (bottleLeft <= 0f) { End(EndReason.BottleEmpty, null); return; }
            if (hp <= 0f) { End(EndReason.Wrecked, null); return; }
            flippedFor = transform.up.y < 0.25f ? flippedFor + dt : 0f;
            if (flippedFor > 1.6f) { End(EndReason.Flipped, null); return; }
            bool pushing = Mathf.Abs(throttle) > 0.5f && Speed < 0.6f && landed;
            stuckFor = pushing ? stuckFor + dt : 0f;
            if (stuckFor > 3f) { End(EndReason.Stalled, null); return; }
            waterFor = water > 0.6f && Speed < 4f ? waterFor + dt : 0f;
            if (waterFor > 1.2f) { End(EndReason.Stalled, null); return; }
            if (transform.position.y < -40f) End(EndReason.Flipped, null);
        }

        private void Update()
        {
            if (ended) return;
            // Wheels: suspension travel, steering, spin.
            wheelSpin += ForwardSpeed / WheelRadius * Mathf.Rad2Deg * Time.deltaTime;
            for (int i = 0; i < 4; i++)
            {
                if (wheels[i] == null) continue;
                Vector3 rest = i < 2 ? Vector3.zero : WheelSpots[i];
                wheels[i].localPosition = rest + Vector3.up * compression[i] * RestLength;
                wheels[i].localRotation = Quaternion.Euler(wheelSpin, 0f, 0f);
            }
            for (int i = 0; i < 2; i++)
                if (steerPivots[i] != null) steerPivots[i].localRotation = Quaternion.Euler(0f, steer * 28f, 0f);

            // Body lean: roll into turns, squat under throttle, dive under braking.
            Vector3 local = transform.InverseTransformDirection(body.linearVelocity);
            float lateral = transform.InverseTransformDirection(body.angularVelocity).y * local.z;
            bodyRoll = Mathf.Lerp(bodyRoll, Mathf.Clamp(-lateral * 0.35f, -7f, 7f), Time.deltaTime * 6f);
            bodyPitch = Mathf.Lerp(bodyPitch, Mathf.Clamp(-throttle * 2.5f + (throttle < 0f && local.z > 1f ? 4f : 0f), -4f, 5f), Time.deltaTime * 5f);
            bodyPivot.localRotation = Quaternion.Euler(bodyPitch, 0f, bodyRoll);

            if (engine != null && engine.Source != null) engine.Source.pitch = 0.7f + Mathf.Clamp01(Mathf.Abs(ForwardSpeed) / TopSpeed) * 1.1f + Mathf.Abs(throttle) * 0.12f;
        }

        /// <summary>Ends the ride. Everything but <see cref="EndReason.Parked"/> blows the car up.</summary>
        public void End(EndReason reason, Health rammed)
        {
            if (ended) return;
            ended = true;
            AnyCarEnded?.Invoke(this, reason);
            Vector3 centre = transform.position + Vector3.up * 0.8f;
            Vector3 travel = lastVelocity;
            if (engine != null) engine.Level = 0f;
            if (reason == EndReason.Parked)
            {
                ParticleBurst.Spawn(ParticleBurst.Kind.Smoke, centre, Vector3.up, 40, 1.4f, 2.5f, 80f, new Color(0.8f, 0.8f, 0.8f, 0.7f));
                driver?.OnCarEnded(this, reason, centre, travel);
                Destroy(gameObject);
                return;
            }
            if (reason == EndReason.BottleEmpty || reason == EndReason.Stalled)
            {
                ParticleBurst.Spawn(ParticleBurst.Kind.Smoke, centre, Vector3.up, 25, 1f, 3f, 40f, new Color(0.25f, 0.25f, 0.25f, 0.8f));
                Sfx.PlayAt("laser_fizzle", centre, 0.8f, 0.6f, 40f);
            }

            float radius = driver != null ? driver.BlastRadius : 6f;
            float damage = driver != null ? driver.BlastDamage : 60f;
            float stun = driver != null ? driver.StunSeconds : 2.5f;
            // The driver's own Health stays safe: it is still parented under the car until OnCarEnded.
            driver?.OnCarExploding(this);
            CarBlast.Explode(centre, radius, damage, stun, driver != null ? driver.gameObject : null, rammed);
            FireEffect.Spawn(transform.position, 1.1f, 0f, 3.5f);
            ThrowParts(travel);
            driver?.OnCarEnded(this, reason, centre, travel);
            Destroy(gameObject);
        }

        // The wheels, doors and bonnet fly off in all directions.
        private void ThrowParts(Vector3 travel)
        {
            var pieces = new List<Transform>();
            foreach (Transform wheel in wheels) if (wheel != null) pieces.Add(wheel);
            foreach (Transform part in bodyPivot) if (part.GetComponent<MeshRenderer>() != null) pieces.Add(part);
            foreach (Transform piece in pieces)
            {
                piece.SetParent(null, true);
                var rb = piece.gameObject.AddComponent<Rigidbody>();
                rb.mass = 20f;
                piece.gameObject.AddComponent<BoxCollider>();
                rb.linearVelocity = travel * 0.4f + (Random.insideUnitSphere + Vector3.up * 1.4f) * Random.Range(5f, 11f);
                rb.angularVelocity = Random.insideUnitSphere * 12f;
                piece.gameObject.AddComponent<Debris>().Begin(Random.Range(3f, 4.5f));
            }
        }

        private void OnDestroy()
        {
            if (!ended) driver?.OnCarEnded(this, EndReason.Parked, transform.position, Vector3.zero);
        }
    }
}
