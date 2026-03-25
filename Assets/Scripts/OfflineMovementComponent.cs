using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OfflineMovementComponent : MonoBehaviour
{
    [SerializeField] private int _speed;
    [SerializeField] private Rigidbody rigid;
    private Vector3 _cachedInput;

    private void Update()
    {
        _cachedInput = new Vector3(Input.GetAxisRaw("Horizontal"), 0, Input.GetAxisRaw("Vertical"));
    }

    private void FixedUpdate()
    {
        rigid.velocity = _cachedInput * _speed;
    }
}
