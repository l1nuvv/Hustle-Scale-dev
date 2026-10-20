using System;
using System.Collections.Generic;
using Player.Movement;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CityGreyboxVerification
{
    private static readonly Vector3[][] Routes =
    {
        Points(-320,96, 0,96, 0,-288, -320,-288, -320,96),
        Points(0,288, 320,288, 320,-288, 0,-288, 0,288),
        Points(-65,25, -65,0, 65,0, 65,35, 65,0, -80,0, -80,-65, -65,-65, -65,-40,
            -65,-65, -80,-65, -80,0, -65,0, -65,25),
        Points(-480,96, 480,96),
        Points(-240,96, -240,192, -320,192, -320,288, -240,288),
        Points(160,192, 240,192, 240,288, 320,288),
        Points(-320,-192, -240,-192, -240,-288, -160,-288),
        Points(160,-192, 240,-192, 240,-288, 240,-384, 320,-384, 320,-288),
    };
    private static readonly string[] Names = { "West loop", "East loop", "Three interiors return", "City crossing", "Residential courtyard", "Supplier courtyard", "Garage courtyard", "Park and outskirts" };
    private static readonly Vector3[][] ReviewRoutes =
    {
        Points(-65,25, -65,0, -240,192, -240,96, -160,96, -160,0, -65,0, -65,25),
        Points(-65,25, -65,0, 65,0, 240,192, 160,192, 160,96, 0,96, 0,0, -65,0, -65,25),
        Points(-65,25, -65,0, -240,-192, -240,-96, -160,-96, -160,0, -65,0, -65,25),
        Points(-65,25, -65,0, 65,0, 240,-192, 160,-192, 160,-96, 0,-96, 0,0, -65,0, -65,25),
        Routes[2],
    };
    private static readonly string[] ReviewNames = { "Homes shortcut and street return", "Supplier shortcut and street return", "Garages shortcut and street return", "Park edge shortcut and street return", "Three interiors return" };
    private static readonly Vector3[][] LocalRoutes =
    {
        Points(-65,25, -65,10, -90,10, -130,30, -130,60, -130,72.5f, -100,72.5f,
            -100,54, -80,54, -80,10, -65,10, -65,25),
        Points(-65,25, -65,0, -42,0, -15,-7, 35,-7, 65,0, 65,35,
            65,0, 35,-12, -42,-12, -65,0, -65,25),
        Points(-65,25, -65,0, -90,0, -115,-12, -115,-65, -90,-65, -65,-65,
            -65,-40, -65,-65, -80,-65, -80,0, -65,0, -65,25),
        Points(-65,25, -65,10, -90,10, -140,35, -140,90, -140,110,
            -160,110, -160,10, -90,10, -65,10, -65,25),
        Points(-65,25, -65,0, -90,0, -115,-12, -160,-12, -160,-96,
            -115,-96, -115,-65, -80,-65, -80,0, -65,0, -65,25),
        Points(-65,25, -65,0, -42,0, -15,-7, 10,-7, 10,-70, 40,-70,
            80,-70, 80,-45, 80,-20, 65,0, -15,-7, -42,0, -65,0, -65,25),
        Points(-150.5f,-86.5f, -150.5f,86.5f, -20,86.5f),
    };
    private static readonly string[] LocalNames = { "Local court loop", "Local market and battery", "Local loading and buyback", "Court west gate and old north street", "Loading west gate and old south street", "Market canopy and old east street", "Preserved west and north sidewalks" };
    private static Vector3[][] _routes = Routes;
    private static string[] _names = Names;
    private static PlayerController _player;
    private static Player.PlayerInput _input;
    private static CharacterController _body;
    private static Player.CityGreyboxWalk _walk;
    private static int _route, _point;
    private static double _started;
    private static float _gameStarted, _lastProgressTime, _routeDistance, _oldScale;
    private static Vector3 _lastPosition;
    private static bool _oldBackground;
    public static string Status { get; private set; } = "Not run";

    private static Vector3[] Points(params float[] coordinates)
    {
        var points = new Vector3[coordinates.Length / 2];
        for (var i = 0; i < points.Length; i++) points[i] = new Vector3(coordinates[i * 2], 0.05f, coordinates[i * 2 + 1]);
        return points;
    }

    private static void RequireCity()
    {
        if (SceneManager.GetActiveScene().path != CityGreyboxBuilder.ScenePath || !EditorApplication.isPlaying)
            throw new InvalidOperationException("Open CityGreybox and enter Play Mode first.");
        _player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
        _input = _player.GetComponent<Player.PlayerInput>();
        _body = _player.GetComponent<CharacterController>();
        _walk = _player.GetComponent<Player.CityGreyboxWalk>();
    }

    public static string CollisionRoutes() => CheckCollisions(Routes, Names, true);

    public static string ReviewCollision(int route) => CheckCollisions(new[] { ReviewRoutes[route] }, new[] { ReviewNames[route] }, false);

    public static string LocalCollision(int route) => CheckCollisions(new[] { LocalRoutes[route] }, new[] { LocalNames[route] }, false);

    public static void WalkLocal() => StartWalkAt(0, LocalRoutes, LocalNames, 1);

    public static void WalkLocalRoute(int route) => StartWalkAt(0, new[] { LocalRoutes[route] }, new[] { LocalNames[route] }, 1);

    private static string CheckCollisions(Vector3[][] routes, string[] names, bool boundaries)
    {
        RequireCity();
        var original = _player.transform.position;
        var rotation = _player.transform.rotation;
        var report = new List<string>();
        Physics.SyncTransforms();
        try
        {
            foreach (var route in routes)
            {
                Teleport(route[0]);
                var length = 0f;
                for (var i = 1; i < route.Length; i++)
                {
                    var delta = route[i] - route[i - 1];
                    var distance = delta.magnitude;
                    var direction = delta.normalized;
                    var orientation = Quaternion.LookRotation(direction);
                    // Ignore only the player, so a crate sweep still sees every world collider.
                    var oldLayer = _player.gameObject.layer;
                    _player.gameObject.layer = 2;
                    try
                    {
                        if (Physics.CheckBox(route[i - 1] + Vector3.up, new Vector3(0.8f, 0.4f, 0.5f), orientation, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ||
                            Physics.BoxCast(route[i - 1] + Vector3.up, new Vector3(0.8f, 0.4f, 0.5f), direction, out var hit, orientation, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                            throw new Exception("Crate clearance blocked: " + route[i - 1] + " -> " + route[i]);
                    }
                    finally { _player.gameObject.layer = oldLayer; }
                    for (var remaining = distance; remaining > 0;)
                    {
                        var step = Mathf.Min(4f / 60, remaining);
                        _body.Move(direction * step + Vector3.down * (2f / 60));
                        remaining -= step;
                        if (_body.transform.position.y < -0.1f || _body.transform.position.y > 0.35f)
                            throw new Exception("Ground continuity failed " + _body.transform.position);
                    }
                    if (Vector2.Distance(new Vector2(_body.transform.position.x, _body.transform.position.z), new Vector2(route[i].x, route[i].z)) > 0.2f)
                        throw new Exception("Character blocked before " + route[i] + " actual " + _body.transform.position);
                    length += distance;
                }
                report.Add(names[Array.IndexOf(routes, route)] + ": " + length.ToString("F1") + "m; model " + (length / 4).ToString("F2") + "s at 4m/s");
            }
            // Walls must stop the actual capsule on all four outer roads.
            foreach (var direction in boundaries ? new[] { Vector3.left, Vector3.right, Vector3.forward, Vector3.back } : Array.Empty<Vector3>())
            {
                Teleport(direction * 490 + Vector3.up * 0.05f);
                for (var i = 0; i < 240; i++) _body.Move(direction * 0.1f + Vector3.down * 0.03f);
                if (Mathf.Abs(_body.transform.position.x) > 496 || Mathf.Abs(_body.transform.position.z) > 496)
                    throw new Exception("Boundary failed " + direction);
            }
            Status = "COLLISION PASS | " + string.Join(" | ", report);
            Debug.Log(Status);
            return Status;
        }
        finally { Teleport(original); _player.transform.rotation = rotation; }
    }

    [MenuItem("Tools/City Greybox/Verify Collision Routes in Play Mode")]
    private static void CollisionMenu() => CollisionRoutes();

    private static void Teleport(Vector3 position)
    {
        _body.enabled = false;
        _player.transform.position = position;
        _body.enabled = true;
        Physics.SyncTransforms();
    }

    [MenuItem("Tools/City Greybox/Walk All Routes Accelerated")]
    public static void StartWalk() => StartWalkAt(0, Routes, Names);

    public static void WalkPark() => StartWalkAt(Routes.Length - 1, Routes, Names);

    public static void WalkReview() => StartWalkAt(0, ReviewRoutes, ReviewNames);

    public static void WalkReviewFrom(int route) => StartWalkAt(route, ReviewRoutes, ReviewNames);

    private static void StartWalkAt(int firstRoute, Vector3[][] routes, string[] names, float scale = 20)
    {
        RequireCity();
        if (Status == "WALK RUNNING") throw new InvalidOperationException("Already walking.");
        _routes = routes;
        _names = names;
        _oldScale = Time.timeScale;
        _oldBackground = Application.runInBackground;
        Application.runInBackground = true;
        _input.enabled = false;
        _walk.SetPaused(false);
        Time.timeScale = scale;
        _route = firstRoute;
        BeginRoute();
        Status = "WALK RUNNING";
        EditorApplication.update += WalkTick;
        EditorApplication.playModeStateChanged += Stopped;
    }

    private static void BeginRoute()
    {
        Teleport(_routes[_route][0]);
        _point = 1;
        _routeDistance = 0;
        _lastPosition = _player.transform.position;
        _started = EditorApplication.timeSinceStartup;
        _gameStarted = Time.time;
        _lastProgressTime = Time.realtimeSinceStartup;
        _player.transform.rotation = Quaternion.LookRotation(_routes[_route][1] - _routes[_route][0]);
        _input.MoveInput = Vector2.up;
    }

    private static void WalkTick()
    {
        if (!EditorApplication.isPlaying) { Finish("WALK INTERRUPTED"); return; }
        // MCP captures can move Editor focus. The harness owns input during its walk;
        // normal focus/pause behavior is verified separately with the harness stopped.
        if (!_player.CanInteract) _walk.SetPaused(false);
        var current = _player.transform.position;
        var delta = _routes[_route][_point] - current;
        delta.y = 0;
        var moved = Vector3.Distance(current, _lastPosition);
        if (moved > 0.01f) _lastProgressTime = Time.realtimeSinceStartup;
        _routeDistance += moved;
        _lastPosition = current;
        if (Time.realtimeSinceStartup - _lastProgressTime > 8 || current.y < -0.1f)
        {
            Finish("WALK FAILED " + _names[_route] + " at " + current);
            return;
        }
        // Clamp the last frame's requested movement to land on corners without cutting them.
        if (delta.magnitude < 0.18f)
        {
            _point++;
            if (_point >= _routes[_route].Length)
            {
                Debug.Log("CITY WALK: " + _names[_route] + " distance=" + _routeDistance.ToString("F2") +
                    "m gameSeconds=" + (Time.time - _gameStarted).ToString("F2") +
                    " wallSeconds=" + (EditorApplication.timeSinceStartup - _started).ToString("F2") + " scale=" + Time.timeScale);
                _route++;
                if (_route == _routes.Length) { Finish("WALK PASS: requested routes using existing PlayerController.Update at timeScale " + Time.timeScale); return; }
                BeginRoute();
            }
            delta = _routes[_route][_point] - _player.transform.position;
            delta.y = 0;
        }
        _player.transform.rotation = Quaternion.LookRotation(delta);
        _input.MoveInput = Vector2.up * Mathf.Min(1, delta.magnitude / Mathf.Max(0.01f, 4 * Time.deltaTime));
    }

    private static void Stopped(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode) Finish("WALK INTERRUPTED");
    }

    private static void Finish(string result)
    {
        EditorApplication.update -= WalkTick;
        EditorApplication.playModeStateChanged -= Stopped;
        Time.timeScale = _oldScale;
        Application.runInBackground = _oldBackground;
        if (_input != null) { _input.MoveInput = Vector2.zero; _input.enabled = true; }
        Status = result;
        Debug.Log(result);
    }
}
