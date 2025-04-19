using Player.Movement;
using UnityEngine;
using UnityEngine.Serialization;

public class PlayerJump : PlayerController
{
    [FormerlySerializedAs("_jumpForce")] [Header("Jump Settings")] [SerializeField]
    private float jumpForce;
}
