using System;
using Player;
using UnityEditor;
using UnityEngine;

// Editor-only real-time integration drive. Inputs go through the vehicle's usual
// FixedUpdate/WheelColliders, not through transform movement or simulated speed.
public static class CityUtilityVehicleVerification
{
    private static CityUtilityVehicle car;
    private static Rigidbody body;
    private static int phase, waypoint;
    private static float started, distance, phaseTime, coastSpeed;
    private static Vector3 previous, phaseStart;
    private static bool oldBackground;
    private static GameObject leftBlock, rightBlock;
    private static readonly Vector3[] RoadLoop =
    {
        new Vector3(-140,0,7), new Vector3(-152,0,12), new Vector3(-160,0,24),
        new Vector3(-160,0,80), new Vector3(-154,0,92), new Vector3(-140,0,96),
        new Vector3(-20,0,96), new Vector3(-7,0,90), new Vector3(0,0,76),
        new Vector3(0,0,12), new Vector3(-7,0,0), new Vector3(-32,0,0),
        new Vector3(-65,0,0), new Vector3(-83,0,7)
    };
    public static string Status { get; private set; } = "Not run";

    public static void Start()
    {
        if (!EditorApplication.isPlaying || Status == "RUNNING") throw new InvalidOperationException("Enter Play Mode once, then start verification.");
        car = UnityEngine.Object.FindAnyObjectByType<CityUtilityVehicle>();
        body = car.GetComponent<Rigidbody>();
        oldBackground = Application.runInBackground;
        Application.runInBackground = true;
        SetPose(new Vector3(-140, 0.4f, 96), 90);
        phase = waypoint = 0;
        phaseTime = Time.time;
        Status = "RUNNING";
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += Stopped;
    }

    private static void SetPose(Vector3 position, float yaw)
    {
        body.linearVelocity = body.angularVelocity = Vector3.zero;
        body.position = position;
        body.rotation = Quaternion.Euler(0, yaw, 0);
        Physics.SyncTransforms();
    }

    private static void Enter()
    {
        var player = GameObject.Find("City Player");
        var capsule = player.GetComponent<CharacterController>();
        capsule.enabled = false;
        player.transform.position = car.transform.TransformPoint(new Vector3(-1.55f, 0, 0.3f));
        player.transform.position = new Vector3(player.transform.position.x, 0.05f, player.transform.position.z);
        capsule.enabled = true;
        var camera = player.GetComponentInChildren<Camera>(true);
        camera.transform.position = player.transform.position + Vector3.up * 1.65f;
        Physics.SyncTransforms();
        if (!car.TryEnter()) throw new Exception("Enter failed");
        if (capsule.enabled || player.GetComponent<Player.Movement.PlayerController>().enabled)
            throw new Exception("Walking context remained active");
    }

    private static void Next(string report)
    {
        Debug.Log("VEHICLE CHECK: " + report);
        phase++;
        phaseTime = Time.time;
        phaseStart = body.position;
    }

    private static void Tick()
    {
        try
        {
            if (!EditorApplication.isPlaying) { Finish("INTERRUPTED"); return; }
            if (car.IsPaused) car.SetPaused(false); // Harness owns input while it runs.
            var elapsed = Time.time - phaseTime;
            if (body.position.y < -0.2f || Vector3.Dot(car.transform.up, Vector3.up) < 0.35f)
                throw new Exception("Fell or overturned at " + body.position);
            switch (phase)
            {
                case 0:
                    if (elapsed < 1.5f) return;
                    if (car.GroundedWheels != 4) throw new Exception("Not four grounded wheels: " + car.GroundedWheels);
                    Enter(); car.VerifyControls(1, 0, false); Next("parked; four contacts; entered"); break;
                case 1:
                    if (elapsed < 6) return;
                    if (car.Speed < 7 || car.Speed > 12.5f) throw new Exception("Acceleration speed=" + car.Speed);
                    coastSpeed = car.Speed;
                    Next("6s acceleration speed=" + (car.Speed * 3.6f).ToString("F1") + "km/h distance=" + Vector3.Distance(phaseStart, body.position).ToString("F2") + "m");
                    car.VerifyControls(0, 0, false); break;
                case 2:
                    if (elapsed < 1.5f) return;
                    if (car.Speed < coastSpeed * 0.55f) throw new Exception("Coast stopped instantly");
                    Next("coast 1.5s speed=" + (car.Speed * 3.6f).ToString("F1") + "km/h");
                    car.VerifyControls(0, 0, true); break;
                case 3:
                    if (elapsed < 2) return;
                    if (car.Speed > 0.5f) throw new Exception("Brake failed " + car.Speed);
                    Next("brake 2s distance=" + Vector3.Distance(phaseStart, body.position).ToString("F2") + "m speed=" + car.Speed.ToString("F2"));
                    car.VerifyControls(-1, 0, false); break;
                case 4:
                    if (elapsed < 2) return;
                    if (car.SignedSpeed > -1 || car.TryExit()) throw new Exception("Reverse or moving exit guard failed");
                    Next("reverse=" + car.SignedSpeed.ToString("F2") + "m/s; moving exit refused");
                    car.VerifyControls(0, 0, true); break;
                case 5:
                    if (elapsed < 1.5f) return;
                    leftBlock = Block(-1); rightBlock = Block(1);
                    Physics.SyncTransforms();
                    if (car.TryExit()) throw new Exception("Exited into blocked doors");
                    UnityEngine.Object.DestroyImmediate(rightBlock);
                    if (!car.TryExit()) throw new Exception("Alternate door exit failed");
                    UnityEngine.Object.DestroyImmediate(leftBlock);
                    Next("blocked exits refused; clear alternate door used");
                    SetPose(new Vector3(-87, 0.4f, 7), 270); break;
                case 6:
                    if (elapsed < 1) return;
                    Enter(); car.VerifyControls(0, 0, true);
                    car.SetPaused(true);
                    if (!body.isKinematic) throw new Exception("Pause did not freeze vehicle");
                    Next("pause freezes body; resume checked on road loop");
                    started = Time.time; distance = 0; previous = body.position; break;
                case 7:
                    if (Time.time - started > 160) throw new Exception("Road loop timeout at " + body.position);
                    distance += Vector3.Distance(previous, body.position); previous = body.position;
                    var delta = RoadLoop[waypoint] - body.position; delta.y = 0;
                    if (delta.magnitude < 4 && waypoint < RoadLoop.Length - 1) { waypoint++; delta = RoadLoop[waypoint] - body.position; delta.y = 0; }
                    var angle = Vector3.SignedAngle(car.transform.forward, delta, Vector3.up);
                    var target = Mathf.Abs(angle) > 18 ? 3.8f : 8.5f;
                    var pedal = Mathf.Clamp((target - car.SignedSpeed) * 0.5f, -1, 1);
                    car.VerifyControls(pedal, Mathf.Clamp(angle / 28, -1, 1), false);
                    if (waypoint == RoadLoop.Length - 1 && delta.magnitude < 5)
                    {
                        Next("road loop distance=" + distance.ToString("F2") + "m gameSeconds=" + (Time.time - started).ToString("F2") + " steering and four street turns");
                        car.VerifyControls(0, 0, true);
                    }
                    break;
                case 8:
                    if (elapsed < 2) return;
                    if (!car.TryExit()) throw new Exception("Final exit failed");
                    car.EndVerification();
                    Finish("PASS: physics, seating, exits, pause, road loop"); break;
            }
        }
        catch (Exception ex) { Finish("FAIL: " + ex.Message); }
    }

    private static GameObject Block(int side)
    {
        var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = "Temporary exit test obstacle";
        block.transform.position = car.transform.TransformPoint(new Vector3(side * 1.55f, 1, 0.3f));
        block.transform.localScale = new Vector3(1, 2.5f, 1);
        return block;
    }

    private static void Stopped(PlayModeStateChange state) { if (state == PlayModeStateChange.ExitingPlayMode) Finish("INTERRUPTED"); }
    private static void Finish(string result)
    {
        EditorApplication.update -= Tick;
        EditorApplication.playModeStateChanged -= Stopped;
        Application.runInBackground = oldBackground;
        if (car != null) car.EndVerification();
        if (leftBlock != null) UnityEngine.Object.DestroyImmediate(leftBlock);
        if (rightBlock != null) UnityEngine.Object.DestroyImmediate(rightBlock);
        Status = result;
        Debug.Log("VEHICLE VERIFICATION " + result);
    }
}
