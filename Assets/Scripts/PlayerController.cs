using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private GameObject playerPrefab;
    private GameObject player;
    public static Action<GameObject> OnPlayerSpawned;

    private void Awake()
    {
        player = Instantiate(playerPrefab, transform.position,Quaternion.identity);
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        OnPlayerSpawned?.Invoke(player);
    }
}
